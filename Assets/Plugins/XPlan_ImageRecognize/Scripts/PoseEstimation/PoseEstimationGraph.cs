using Google.Protobuf;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Sample;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using TextureFrame = Mediapipe.Unity.Experimental.TextureFrame;

namespace XPlan.ImageRecognize
{
    public readonly struct PoseEstimationResult
    {
        public readonly NormalizedLandmarkList poseLandmarks;
        public readonly LandmarkList poseWorldLandmarks;

        public PoseEstimationResult(NormalizedLandmarkList poseLandmarks, 
                                    LandmarkList poseWorldLandmarks)
        {
            this.poseLandmarks      = poseLandmarks;
            this.poseWorldLandmarks = poseWorldLandmarks;
        }
    }

    public class PoseEstimationGraph : GraphRunner
    {
        public enum ModelComplexity
        {
            Lite    = 0,
            Full    = 1,
            Heavy   = 2,
        }

        public bool refineFaceLandmarks         = false;
        public ModelComplexity modelComplexity  = ModelComplexity.Lite;
        public bool smoothLandmarks             = true;
        private float _minDetectionConfidence   = 0.5f;
        private float _minTrackingConfidence    = 0.5f;
        public float minDetectionConfidence
        {
            get => _minDetectionConfidence;
            set => _minDetectionConfidence = Mathf.Clamp01(value);
        }

        public float minTrackingConfidence
        {
            get => _minTrackingConfidence;
            set => _minTrackingConfidence = Mathf.Clamp01(value);
        }

        public event EventHandler<OutputStream<NormalizedLandmarkList>.OutputEventArgs> OnPoseLandmarksOutput
        {
            add => _poseLandmarksStream.AddListener(value, timeoutMicrosec);
            remove => _poseLandmarksStream.RemoveListener(value);
        }

        public event EventHandler<OutputStream<LandmarkList>.OutputEventArgs> OnPoseWorldLandmarksOutput
        {
            add => _poseWorldLandmarksStream.AddListener(value, timeoutMicrosec);
            remove => _poseWorldLandmarksStream.RemoveListener(value);
        }

        private const string _InputStreamName               = "input_video";
        private const string _PoseLandmarksStreamName       = "pose_landmarks";
        private const string _PoseWorldLandmarksStreamName  = "pose_world_landmarks";
        
        private OutputStream<NormalizedLandmarkList> _poseLandmarksStream;
        private OutputStream<LandmarkList> _poseWorldLandmarksStream;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public override void StartRun(ImageSource imageSource)
        {
            if (runningMode.IsSynchronous())
            {
                _poseLandmarksStream.StartPolling();
                _poseWorldLandmarksStream.StartPolling();
            }

            StartRun(BuildSidePacket(imageSource));
        }

        public override void Stop()
        {
            base.Stop();

            _poseLandmarksStream?.Dispose();
            _poseWorldLandmarksStream?.Dispose();

            _poseLandmarksStream        = null;
            _poseWorldLandmarksStream   = null;
        }

        public void AddTextureFrameToInputStream(TextureFrame textureFrame, GlContext glContext = null)
        {
            AddTextureFrameToInputStream(_InputStreamName, textureFrame, glContext);
        }

        public async Task<PoseEstimationResult> WaitNextAsync()
        {
            var results = await WhenAll(
                    _poseLandmarksStream.WaitNextAsync(),
                    _poseWorldLandmarksStream.WaitNextAsync()
                  );
            AssertResult(results);

            _ = TryGetValue(results.Item1.packet, out var poseLandmarks, (packet) =>
            {
                return packet.Get(NormalizedLandmarkList.Parser);
            });
            _ = TryGetValue(results.Item2.packet, out var poseWorldLandmarks, (packet) =>
            {
                return packet.Get(LandmarkList.Parser);
            });

            return new PoseEstimationResult(poseLandmarks, poseWorldLandmarks);
        }

        protected override IList<WaitForResult> RequestDependentAssets()
        {
            return new List<WaitForResult> 
            {
                WaitForAsset("face_detection_short_range.bytes"),
                WaitForAsset(refineFaceLandmarks ? "face_landmark_with_attention.bytes" : "face_landmark.bytes"),
                WaitForAsset("iris_landmark.bytes"),
                WaitForAsset("hand_landmark_full.bytes"),
                WaitForAsset("hand_recrop.bytes"),
                WaitForAsset("handedness.txt"),
                WaitForAsset("palm_detection_full.bytes"),
                WaitForAsset("pose_detection.bytes"),
                WaitForPoseLandmarkModel(),
            };
        }

        private WaitForResult WaitForPoseLandmarkModel()
        {
            switch (modelComplexity)
            {
                case ModelComplexity.Lite: return WaitForAsset("pose_landmark_lite.bytes");
                case ModelComplexity.Full: return WaitForAsset("pose_landmark_full.bytes");
                case ModelComplexity.Heavy: return WaitForAsset("pose_landmark_heavy.bytes");
                default: throw new InternalException($"Invalid model complexity: {modelComplexity}");
            }
        }
        protected override void ConfigureCalculatorGraph(CalculatorGraphConfig config)
        {
            // 初始化與 Pose 相關的輸出流
            _poseLandmarksStream        = new OutputStream<NormalizedLandmarkList>(calculatorGraph, _PoseLandmarksStreamName, true);
            _poseWorldLandmarksStream   = new OutputStream<LandmarkList>(calculatorGraph, _PoseWorldLandmarksStreamName, true);

            using (var validatedGraphConfig = new ValidatedGraphConfig())
            {
                validatedGraphConfig.Initialize(config);

                var extensionRegistry               = new ExtensionRegistry() { TensorsToDetectionsCalculatorOptions.Extensions.Ext, ThresholdingCalculatorOptions.Extensions.Ext };
                var cannonicalizedConfig            = validatedGraphConfig.Config(extensionRegistry);

                var poseDetectionCalculatorPattern  = new Regex("__posedetection[a-z]+__TensorsToDetectionsCalculator$");
                var tensorsToDetectionsCalculators  = cannonicalizedConfig.Node.Where((node) => poseDetectionCalculatorPattern.Match(node.Name).Success).ToList();

                var poseTrackingCalculatorPattern   = new Regex("tensorstoposelandmarksandsegmentation__ThresholdingCalculator$");
                var thresholdingCalculators         = cannonicalizedConfig.Node.Where((node) => poseTrackingCalculatorPattern.Match(node.Name).Success).ToList();

                foreach (var calculator in tensorsToDetectionsCalculators)
                {
                    if (calculator.Options.HasExtension(TensorsToDetectionsCalculatorOptions.Extensions.Ext))
                    {
                        var options             = calculator.Options.GetExtension(TensorsToDetectionsCalculatorOptions.Extensions.Ext);
                        options.MinScoreThresh  = minDetectionConfidence;
                        Debug.Log($"Min Detection Confidence = {minDetectionConfidence}");
                    }
                }

                foreach (var calculator in thresholdingCalculators)
                {
                    if (calculator.Options.HasExtension(ThresholdingCalculatorOptions.Extensions.Ext))
                    {
                        var options         = calculator.Options.GetExtension(ThresholdingCalculatorOptions.Extensions.Ext);
                        options.Threshold   = minTrackingConfidence;
                        Debug.Log($"Min Tracking Confidence = {minTrackingConfidence}");
                    }
                }
                calculatorGraph.Initialize(cannonicalizedConfig);
            }
        }

        private PacketMap BuildSidePacket(ImageSource imageSource)
        {
            var sidePacket = new PacketMap();

            SetImageTransformationOptions(sidePacket, imageSource);

            // TODO: refactoring
            // The orientation of the output image must match that of the input image.
            var isInverted                  = ImageCoordinate.IsInverted(imageSource.rotation);
            var outputRotation              = imageSource.rotation;
            var outputHorizontallyFlipped   = !isInverted && imageSource.isHorizontallyFlipped;
            var outputVerticallyFlipped     = (!runningMode.IsSynchronous() && imageSource.isVerticallyFlipped) ^ (isInverted && imageSource.isHorizontallyFlipped);

            if ((outputHorizontallyFlipped && outputVerticallyFlipped) || outputRotation == RotationAngle.Rotation180)
            {
                outputRotation              = outputRotation.Add(RotationAngle.Rotation180);
                outputHorizontallyFlipped   = !outputHorizontallyFlipped;
                outputVerticallyFlipped     = !outputVerticallyFlipped;
            }

            sidePacket.Emplace("output_rotation", Packet.CreateInt((int)outputRotation));
            sidePacket.Emplace("output_horizontally_flipped", Packet.CreateBool(outputHorizontallyFlipped));
            sidePacket.Emplace("output_vertically_flipped", Packet.CreateBool(outputVerticallyFlipped));

            Debug.Log($"outtput_rotation = {outputRotation}, output_horizontally_flipped = {outputHorizontallyFlipped}, output_vertically_flipped = {outputVerticallyFlipped}");

            sidePacket.Emplace("refine_face_landmarks", Packet.CreateBool(refineFaceLandmarks));
            sidePacket.Emplace("model_complexity", Packet.CreateInt((int)modelComplexity));
            sidePacket.Emplace("smooth_landmarks", Packet.CreateBool(smoothLandmarks));

            Debug.Log($"Refine Face Landmarks = {refineFaceLandmarks}");
            Debug.Log($"Model Complexity = {modelComplexity}");
            Debug.Log($"Smooth Landmarks = {smoothLandmarks}");

            return sidePacket;
        }
    }
}
