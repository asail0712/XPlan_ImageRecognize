using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Google.Protobuf.Collections;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.Holistic;

using XPlan.Observe;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.ImageRecognize
{    
    public class PoseWorldLandmarkMsg : MessageBase
    {
        public LandmarkList landmarkList;
        public PoseWorldLandmarkMsg(LandmarkList landmarkList)
        {
            this.landmarkList = landmarkList;
        }
    }

    public class HipPositionMsg : MessageBase
    {
        public Vector3 hipPos;

        public HipPositionMsg(Vector3 pos)
        {
            this.hipPos = pos;
        }
    }

    public class PoseLandmarkMsg : MessageBase
    {
        public NormalizedLandmarkList landmarkList;

        public PoseLandmarkMsg(NormalizedLandmarkList landmarkList)
        {
            this.landmarkList = landmarkList;
        }
    }

    public class FittingRoomLogic : LogicComponent
    {
        HolisticTrackingGraph graphRunner;
        RunningMode runningMode;
        ImageSource imageSource;

        public FittingRoomLogic()
        {
            RegisterNotify<GraphRunnerPrepareMsg>((msg) =>
            {
                runningMode = msg.runningMode;
                graphRunner = (HolisticTrackingGraph)msg.graphRunner;

                StartRun();
            });

            RegisterNotify<TexturePrepareMsg>((msg) =>
            {
                imageSource = msg.imageSource;

                StartRun();
            });
        }

        private void StartRun()
        {
            if (graphRunner == null || imageSource == null)
            {
                return;
            }

            StartCoroutine(Run(graphRunner, runningMode, imageSource));
        }

        protected IEnumerator Run(HolisticTrackingGraph graphRunner, RunningMode runningMode, ImageSource imageSource)
        {
            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            TextureFramePool textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            if (!runningMode.IsSynchronous())
            {
                graphRunner.OnPoseWorldLandmarksOutput  += OnPoseWorldLandmarksOutput;
                graphRunner.OnPoseLandmarksOutput       += OnPoseLandmarksOutput;
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
                        break;
                    }
                }

                graphRunner.AddTextureFrameToInputStream(textureFrame, glContext);

                if (runningMode.IsSynchronous())
                {
                    var task = graphRunner.WaitNextAsync();

                    yield return new WaitUntil(() => task.IsCompleted);

                    var result = task.Result;
                    
                    SendMsg<PoseWorldLandmarkMsg>(result.poseWorldLandmarks);
                }
            }
        }


        private void OnPoseWorldLandmarksOutput(object stream, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(LandmarkList.Parser);
            
            if(value == null)
            {
                return;
            }    

            SendGlobalMsg<PoseWorldLandmarkMsg>(value);
        }

        private void OnPoseLandmarksOutput(object stream, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(NormalizedLandmarkList.Parser);

            if (value == null)
            {
                return;
            }

            SendGlobalMsg<PoseLandmarkMsg>(value);

            NormalizedLandmarkList nlmList              = value;
            RepeatedField<NormalizedLandmark> lmList    = nlmList.Landmark;

            NormalizedLandmark leftHip                  = lmList[23];
            NormalizedLandmark rightHip                 = lmList[24];
            
            SendGlobalMsg<HipPositionMsg>(new Vector3((leftHip.X + rightHip.X) / 2f, 1 - (leftHip.Y + rightHip.Y) / 2f, (leftHip.Z + rightHip.Z) / 2f));
        }
    }
}
