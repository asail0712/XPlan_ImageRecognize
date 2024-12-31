using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Google.Protobuf.Collections;

namespace Mediapipe.Unity.Sample.Holistic
{
    public class Mediapipe2UnitySkeletonSolution : LegacySolutionRunner<HolisticTrackingGraph>
    {
        [SerializeField] private RectTransform _worldAnnotationArea;
        [SerializeField] private DetectionAnnotationController _poseDetectionAnnotationController;
        [SerializeField] private HolisticLandmarkListAnnotationController _holisticAnnotationController;
        [SerializeField] private MaskAnnotationController _segmentationMaskAnnotationController;
        [SerializeField] private NormalizedRectAnnotationController _poseRoiAnnotationController;
    
        [SerializeField] private PoseWorldLandmarkListAnnotationController _poseWorldLandmarksAnnotationController;
        [SerializeField] private Mediapipe2UnitySkeletonController _mediapipe2UnitySkeletonController;

        [SerializeField] private GameObject fittingAvatar;
        [SerializeField] private AvatarScaler avatarScaler;

        private Experimental.TextureFramePool _textureFramePool;
        private Vector3 keyPoint;
        //private GameObject hipAnchor;

        public HolisticTrackingGraph.ModelComplexity modelComplexity
        {
            get => graphRunner.modelComplexity;
            set => graphRunner.modelComplexity = value;
        }

        public bool smoothLandmarks
        {
            get => graphRunner.smoothLandmarks;
            set => graphRunner.smoothLandmarks = value;
        }

        public bool refineFaceLandmarks
        {
            get => graphRunner.refineFaceLandmarks;
            set => graphRunner.refineFaceLandmarks = value;
        }

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

        public float minDetectionConfidence
        {
            get => graphRunner.minDetectionConfidence;
            set => graphRunner.minDetectionConfidence = value;
        }

        public float minTrackingConfidence
        {
            get => graphRunner.minTrackingConfidence;
            set => graphRunner.minTrackingConfidence = value;
        }

        protected override IEnumerator Run()
        {
            //hipAnchor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //hipAnchor.transform.Rotate(new Vector3(90, 0, 0));

            var graphInitRequest    = graphRunner.WaitForInit(runningMode);
            var imageSource         = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Logger.LogError(TAG, "Failed to start ImageSource, exiting...");
                yield break;
            }

            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32);

            screen.Initialize(imageSource);
            _worldAnnotationArea.localEulerAngles = imageSource.rotation.Reverse().GetEulerAngles();

            yield return graphInitRequest;
            if (graphInitRequest.isError)
            {
                Logger.LogError(TAG, graphInitRequest.error);
                yield break;
            }

            if (!runningMode.IsSynchronous())
            {
                graphRunner.OnPoseWorldLandmarksOutput  += OnPoseWorldLandmarksOutput;
                graphRunner.OnPoseLandmarksOutput       += OnPoseLandmarksOutput;
            }

            SetupAnnotationController(_poseWorldLandmarksAnnotationController, imageSource);

            graphRunner.StartRun(imageSource);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone        = new WaitUntil(() => req.done);

            // NOTE: we can share the GL context of the render thread with MediaPipe (for now, only on Android)
            var canUseGpuImage  = graphRunner.configType == GraphRunner.ConfigType.OpenGLES && GpuManager.GpuResources != null;
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

                graphRunner.AddTextureFrameToInputStream(textureFrame);

                if (runningMode.IsSynchronous())
                {
                    screen.ReadSync(textureFrame);

                    var task = graphRunner.WaitNextAsync();

                    yield return new WaitUntil(() => task.IsCompleted);

                    var result = task.Result;

                    _poseWorldLandmarksAnnotationController.DrawNow(result.poseWorldLandmarks);
                    _mediapipe2UnitySkeletonController.Refresh(result.poseWorldLandmarks);
                }
            }
        }

        private void OnPoseWorldLandmarksOutput(object stream, OutputStream<LandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(LandmarkList.Parser);

            _poseWorldLandmarksAnnotationController.DrawLater(value);
            _mediapipe2UnitySkeletonController.Refresh(value);
        }

        private void OnPoseLandmarksOutput(object stream, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
        {
            var packet  = eventArgs.packet;
            var value   = packet == null ? default : packet.Get(NormalizedLandmarkList.Parser);

            if(value == null)
            {
                return;
            }

            NormalizedLandmarkList nlmList              = value;
            RepeatedField<NormalizedLandmark> lmList    = nlmList.Landmark;

            NormalizedLandmark leftHip  = lmList[23];
            NormalizedLandmark rightHip = lmList[24];

            keyPoint = new Vector3((leftHip.X + rightHip.X) / 2f, 1 - (leftHip.Y + rightHip.Y) / 2f, (leftHip.Z + rightHip.Z) / 2f);

            avatarScaler.SetLandmark(lmList);
        }

        void LateUpdate()
        {
            if (keyPoint == null || fittingAvatar == null)
            {
                return;
            }

            // Screen 0,0 在左下角
            Vector3 hipScreenPos    = new Vector3(keyPoint.x * UnityEngine.Screen.width, keyPoint.y * UnityEngine.Screen.height, 10f);
            Vector3 hipWorldPos     = Camera.main.ScreenToWorldPoint(hipScreenPos);

            //hipAnchor.transform.position    = hipWorldPos;
            //hipAnchor.transform.localScale  = Vector3.one * 1f; // 设置球体的大小

            fittingAvatar.transform.position = hipWorldPos;            
        }
    }
}
