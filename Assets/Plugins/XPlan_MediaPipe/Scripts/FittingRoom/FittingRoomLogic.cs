using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Google.Protobuf.Collections;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.Holistic;

using XPlan.Observe;
using XPlan.Interface;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.MediaPipe.FittingRoom
{    
    public class FittingRoomLogic : LogicComponent, ITickable
    {
        private AvatarScaler avatarScaler;
        private AvatarFitting avatarFitting;
        private GameObject fittingAvatarGO;
        private Vector3 keyPoint;

        public FittingRoomLogic(GameObject fittingAvatarGO)
        {
            this.fittingAvatarGO    = fittingAvatarGO;
            this.avatarScaler       = fittingAvatarGO.GetComponent<AvatarScaler>();
            this.avatarFitting      = fittingAvatarGO.GetComponent<AvatarFitting>();

            RegisterNotify<PrepareFinishMsg>((msg) => 
            {
                RunningMode runningMode             = msg.runningMode;
                HolisticTrackingGraph graphRunner  = (HolisticTrackingGraph)msg.graphRunner;
                ImageSource imageSource             = msg.imageSource;

                StartCoroutine(Run(graphRunner, runningMode, imageSource));
            });
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

                    avatarFitting.Refresh(result.poseWorldLandmarks);
                }
            }
        }


        private void OnPoseWorldLandmarksOutput(object stream, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(LandmarkList.Parser);

            avatarFitting.Refresh(value);
        }

        private void OnPoseLandmarksOutput(object stream, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? default : packet.Get(NormalizedLandmarkList.Parser);

            if (value == null)
            {
                return;
            }

            NormalizedLandmarkList nlmList = value;
            RepeatedField<NormalizedLandmark> lmList = nlmList.Landmark;

            NormalizedLandmark leftHip = lmList[23];
            NormalizedLandmark rightHip = lmList[24];

            keyPoint = new Vector3((leftHip.X + rightHip.X) / 2f, 1 - (leftHip.Y + rightHip.Y) / 2f, (leftHip.Z + rightHip.Z) / 2f);

            avatarScaler.SetLandmark(lmList);
        }

        public void Tick(float deltaTime)
        {
            if (keyPoint == null || fittingAvatarGO == null)
            {
                return;
            }

            // Screen 0,0 在左下角
            Vector3 hipScreenPos = new Vector3(keyPoint.x * UnityEngine.Screen.width, keyPoint.y * UnityEngine.Screen.height, 10f);
            Vector3 hipWorldPos = Camera.main.ScreenToWorldPoint(hipScreenPos);

            fittingAvatarGO.transform.position = hipWorldPos;
        }
    }
}
