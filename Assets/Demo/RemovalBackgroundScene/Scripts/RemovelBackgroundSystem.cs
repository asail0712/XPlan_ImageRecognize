using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

using XPlan;
using XPlan.UI;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace asail0712.Test
{
    public class RemovelBackgroundSystem : MonoBehaviour
    {
        private static readonly string _BootstrapName = nameof(Bootstrap);

        [SerializeField] private GameObject _bootstrapPrefab;
        [SerializeField] protected RemovalBackgroundGraph graphRunner;

        protected Bootstrap bootstrap;
        private TextureFramePool _textureFramePool;

        private IEnumerator Start()
        {
            bootstrap = FindBootstrap();

            yield return new WaitUntil(() => bootstrap.isFinished);
            yield return Run();
        }

        private IEnumerator Run()
        {
            WaitForResult graphInitRequest  = graphRunner.WaitForInit(RunningMode.Sync);
            ImageSource imageSource         = new WebCamTextureSource();

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            _textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            // NOTE: The screen will be resized later, keeping the aspect ratio.
            UISystem.DirectCall<ImageSource>(UICommand.InitScreen, imageSource);

            yield return graphInitRequest;
            if (graphInitRequest.isError)
            {
                Debug.LogError(graphInitRequest.error);
                yield break;
            }

            graphRunner.StartRun(imageSource);

            AsyncGPUReadbackRequest req = default;
            WaitUntil waitUntilReqDone  = new WaitUntil(() => req.done);

            while (true)
            {
                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture());
                yield return waitUntilReqDone;

                if (req.hasError)
                {
                    Debug.LogError($"Failed to read texture from the image source, exiting...");
                    break;
                }

                graphRunner.AddTextureFrameToInputStream(textureFrame);

                Task<RemovalBackgroundResult> task = graphRunner.WaitNextAsync();
                yield return new WaitUntil(() => task.IsCompleted);

                RemovalBackgroundResult result = task.Result;
                UISystem.DirectCall<ImageFrame>(UICommand.UpdateMask, result.segmentationMask);
                result.segmentationMask?.Dispose();
            }
        }

        private Bootstrap FindBootstrap()
        {
            var bootstrapObj = GameObject.Find("Bootstrap");

            if (bootstrapObj == null)
            {
                Debug.Log("Initializing the Bootstrap GameObject");
                bootstrapObj        = Instantiate(_bootstrapPrefab);
                bootstrapObj.name   = _BootstrapName;
                DontDestroyOnLoad(bootstrapObj);
            }

            return bootstrapObj.GetComponent<Bootstrap>();
        }
    }
}
