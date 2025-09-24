using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

using XPlan.Observe;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.ImageRecognize
{    
    public class PoseEstimationLogic : LogicComponent
    {
        private PoseEstimationGraph graphRunner;
        private RunningMode runningMode;
        private ImageSource imageSource;

        private float tickTime = 0f;

        public PoseEstimationLogic(float tickTime)
        {
            this.graphRunner    = null;
            this.runningMode    = RunningMode.Async;
            this.imageSource    = null;
            this.tickTime       = tickTime;
        
            RegisterNotify<GraphRunnerPrepareMsg>((msg) => 
            {
                runningMode = msg.runningMode;
                graphRunner = (PoseEstimationGraph)msg.graphRunner;

                StartRun();
            });

            RegisterNotify<TexturePrepareMsg>((msg) =>
            {
                imageSource     = msg.imageSource;
                //maskArray       = new float[imageSource.textureWidth * imageSource.textureHeight];
                //maskColorArray  = new Color32[imageSource.textureWidth * imageSource.textureHeight];

                StartRun();
            });
        }

        private void StartRun()
        {
            if(graphRunner == null || imageSource == null)
            {
                return;
            }

            StartCoroutine(Run(graphRunner, runningMode, imageSource));
        }

        protected IEnumerator Run(PoseEstimationGraph graphRunner, RunningMode runningMode, ImageSource imageSource)
        {
            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            TextureFramePool textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            if (!runningMode.IsSynchronous())
            {
                graphRunner.OnPoseLandmarksOutput       += OnPoseLandmarksOutput;
                graphRunner.OnPoseWorldLandmarksOutput  += OnPoseWorldLandmarksOutput;
            }

            graphRunner.StartRun(imageSource);

            AsyncGPUReadbackRequest req = default;
            WaitUntil waitUntilReqDone  = new WaitUntil(() => req.done);

            // NOTE: we can share the GL context of the render thread with MediaPipe (for now, only on Android)
            bool bCanUseGpuImage        = graphRunner.configType == GraphRunner.ConfigType.OpenGLES && GpuManager.GpuResources != null;
            using GlContext glContext   = bCanUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (!textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // Copy current image to TextureFrame
                if (bCanUseGpuImage)
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
                        //break;
                        continue;
                    }
                }

                if (tickTime > 0f)
                {
                    yield return new WaitForSeconds(tickTime);
                }

                graphRunner.AddTextureFrameToInputStream(textureFrame, glContext);

                if (runningMode.IsSynchronous())
                {
                    Task<PoseEstimationResult> task  = graphRunner.WaitNextAsync();

                    yield return new WaitUntil(() => task.IsCompleted);

                    PoseEstimationResult result      = task.Result;
                    //ProcessImageFrame(result.poseDetection, result.poseLandmarks, result.poseWorldLandmarks, result.poseRoi);                    
                }
            }
        }

        private void OnPoseLandmarksOutput(object stream, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? default : packet.Get(NormalizedLandmarkList.Parser);
            //_holisticAnnotationController.DrawPoseLandmarkListLater(value);
        }

        private void OnPoseWorldLandmarksOutput(object stream, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? default : packet.Get(LandmarkList.Parser);
            //_poseWorldLandmarksAnnotationController.DrawLater(value);
        }
    }
}
