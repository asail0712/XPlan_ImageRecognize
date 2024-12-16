using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

namespace asail0712.Test
{
    public class RemovalBackgroundSolution : LegacySolutionRunner<RemovalBackgroundGraph>
    {
        [SerializeField] private MaskAnnotationController _segmentationMaskAnnotationController;

        private Mediapipe.Unity.Experimental.TextureFramePool _textureFramePool;

        public bool enableSegmentation
        {
            get => graphRunner.enableSegmentation;
            set => graphRunner.enableSegmentation = value;
        }

        public bool smoothSegmentation
        {
            get => graphRunner.smoothSegmentation;
            set => graphRunner.smoothSegmentation = value;
        }


        protected override IEnumerator Run()
        {
            var graphInitRequest = graphRunner.WaitForInit(runningMode);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            _textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            // NOTE: The screen will be resized later, keeping the aspect ratio.
            screen.Initialize(imageSource);
            
            yield return graphInitRequest;
            if (graphInitRequest.isError)
            {
                Debug.LogError(graphInitRequest.error);
                yield break;
            }

            if (!runningMode.IsSynchronous())
            {
                graphRunner.OnSegmentationMaskOutput += OnSegmentationMaskOutput;
            }

            SetupAnnotationController(_segmentationMaskAnnotationController, imageSource);
            _segmentationMaskAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);

            graphRunner.StartRun(imageSource);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);

            // NOTE: we can share the GL context of the render thread with MediaPipe (for now, only on Android)
            var canUseGpuImage = graphRunner.configType == GraphRunner.ConfigType.OpenGLES && GpuManager.GpuResources != null;
            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // Copy current image to TextureFrame
                if (canUseGpuImage)
                {
                    yield return new WaitForEndOfFrame();
                    textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture());
                }
                else
                {
                    req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture());
                    yield return waitUntilReqDone;

                    if (req.hasError)
                    {
                        Debug.LogError($"Failed to read texture from the image source, exiting...");
                        break;
                    }
                }

                graphRunner.AddTextureFrameToInputStream(textureFrame, glContext);

                if (runningMode.IsSynchronous())
                {
                    screen.ReadSync(textureFrame);

                    var task = graphRunner.WaitNextAsync();
                    yield return new WaitUntil(() => task.IsCompleted);

                    var result = task.Result;
                    _segmentationMaskAnnotationController.DrawNow(result.segmentationMask);

                    result.segmentationMask?.Dispose();
                }
            }
        }
        private void OnSegmentationMaskOutput(object stream, OutputStream<ImageFrame>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? default : packet.Get();
            _segmentationMaskAnnotationController.DrawLater(value);
            value?.Dispose();
        }
    }
}
