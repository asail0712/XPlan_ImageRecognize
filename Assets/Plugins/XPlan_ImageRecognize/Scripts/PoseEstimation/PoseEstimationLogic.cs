using Google.Protobuf.WellKnownTypes;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using XPlan.Observe;
using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.ImageRecognize
{    
    public class PoseLandListMsg : MessageBase
    {
        public List<Vector3> landmarkList;
        public bool bIsMirror;

        public PoseLandListMsg(List<Vector3> landmarkList, bool bIsMirror)
        {
            this.landmarkList   = landmarkList;
            this.bIsMirror      = bIsMirror;
        }
    }

    public class PoseWorldLandListMsg : MessageBase
    {
        public List<Vector3> landmarkList;
        public bool bIsMirror;

        public PoseWorldLandListMsg(List<Vector3> landmarkList, bool bIsMirror)
        {
            this.landmarkList   = landmarkList;
            this.bIsMirror      = bIsMirror;
        }
    }

    public class MediapipeLandmarkListMsg : MessageBase
    {
        public LandmarkList landmarkList;
        public bool bIsMirror;

        public MediapipeLandmarkListMsg(LandmarkList landmarkList, bool bIsMirror)
        {
            this.landmarkList   = landmarkList;
            this.bIsMirror      = bIsMirror;

            Send();
        }
    }

    public class PoseEstimationLogic : LogicComponent
    {
        private PoseEstimationGraph graphRunner;
        private RunningMode runningMode;
        private ImageSource imageSource;

        private float tickTime  = 0f;
        private bool bMirror    = false;

        public PoseEstimationLogic(float tickTime)
        {
            this.graphRunner    = null;
            this.runningMode    = RunningMode.Async;
            this.imageSource    = null;
            this.tickTime       = tickTime;
            this.bMirror        = false;


            RegisterNotify<GraphRunnerPrepareMsg>((msg) => 
            {
                runningMode = msg.runningMode;
                graphRunner = (PoseEstimationGraph)msg.graphRunner;

                StartRun();
            });

            RegisterNotify<TexturePrepareMsg>((msg) =>
            {
                imageSource     = msg.imageSource;

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
            bMirror                     = imageSource.isFrontFacing;

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

                    ProcessPoseLandmark(result.poseLandmarks);
                    ProcessPoseWorldLandmark(result.poseWorldLandmarks);
                }
            }
        }

        private void OnPoseLandmarksOutput(object stream, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(NormalizedLandmarkList.Parser);

            ProcessPoseLandmark((NormalizedLandmarkList)value);
        }

        private void OnPoseWorldLandmarksOutput(object stream, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? default : packet.Get(LandmarkList.Parser);

            ProcessPoseWorldLandmark((LandmarkList)value);
        }

        private void ProcessPoseLandmark(NormalizedLandmarkList poseLandmarkList)
        {
            if (poseLandmarkList == null)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<Vector3>(), bMirror);
                return;
            }

            List<Vector3> posLost                           = new List<Vector3>();
            IReadOnlyList<NormalizedLandmark> landmarkList  = poseLandmarkList.Landmark;

            for (int i = 0; i < landmarkList.Count; ++i)
            {
                Vector3 p = new Vector3(landmarkList[i].X, landmarkList[i].Y, 0f);
                posLost.Add(p);
            }

            SendGlobalMsg<PoseLandListMsg>(posLost, bMirror);
        }

        private void ProcessPoseWorldLandmark(LandmarkList poseWorldLandmarkList)
        {
            new MediapipeLandmarkListMsg(poseWorldLandmarkList, bMirror);

            if (poseWorldLandmarkList == null)
            {
                SendGlobalMsg<PoseWorldLandListMsg>(new List<Vector3>(), bMirror);
                return;
            }

            List<Vector3> posLost                   = new List<Vector3>();
            IReadOnlyList<Landmark> landmarkList    = poseWorldLandmarkList.Landmark;

            for (int i = 0; i < landmarkList.Count; ++i)
            {
                Vector3 p = new Vector3(landmarkList[i].X, landmarkList[i].Y, landmarkList[i].Z);
                posLost.Add(p);
            }

            SendGlobalMsg<PoseWorldLandListMsg>(posLost, bMirror);
        }
    }
}
