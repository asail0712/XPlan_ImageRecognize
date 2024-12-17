using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;

using Google.Protobuf;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Sample;

namespace asail0712.Test
{
    public readonly struct RemovalBackgroundResult
    {
        public readonly ImageFrame segmentationMask;
     
        public RemovalBackgroundResult(ImageFrame segmentationMask)
        {
            this.segmentationMask = segmentationMask;
        }
    }

    public class RemovalBackgroundGraph : GraphRunner
    {
        public enum ModelComplexity
        {
            Lite = 0,
            Full = 1,
            Heavy = 2,
        }

        private float _minDetectionConfidence = 0.5f;
        public float minDetectionConfidence
        {
            get => _minDetectionConfidence;
            set => _minDetectionConfidence = Mathf.Clamp01(value);
        }

        private float _minTrackingConfidence = 0.5f;
        public float minTrackingConfidence
        {
            get => _minTrackingConfidence;
            set => _minTrackingConfidence = Mathf.Clamp01(value);
        }

        public bool refineFaceLandmarks = false;
        public ModelComplexity modelComplexity = ModelComplexity.Lite;
        public bool smoothLandmarks = true;
        public bool enableSegmentation = true;
        public bool smoothSegmentation = true;

        public event EventHandler<OutputStream<ImageFrame>.OutputEventArgs> OnSegmentationMaskOutput
        {
            add => _segmentationMaskStream.AddListener(value, timeoutMicrosec);
            remove => _segmentationMaskStream.RemoveListener(value);
        }

        private const string _InputStreamName = "input_video";
        private const string _SegmentationMaskStreamName = "segmentation_mask";

        private OutputStream<ImageFrame> _segmentationMaskStream;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public override void StartRun(ImageSource imageSource)
        {
            if (runningMode.IsSynchronous())
            {
                _segmentationMaskStream.StartPolling();
            }

            StartRun(BuildSidePacket(imageSource));
        }

        public override void Stop()
        {
            _segmentationMaskStream?.Dispose();
            _segmentationMaskStream = null;
        }

        public void AddTextureFrameToInputStream(Mediapipe.Unity.Experimental.TextureFrame textureFrame, GlContext glContext = null)
        {
            AddTextureFrameToInputStream(_InputStreamName, textureFrame, glContext);
        }

        public async Task<RemovalBackgroundResult> WaitNextAsync()
        {
            var results = await _segmentationMaskStream.WaitNextAsync();

            AssertResult(results);

            _ = TryGetValue(results.packet, out var segmentationMask, (packet) =>
            {
                return packet.Get();
            });

            return new RemovalBackgroundResult(segmentationMask);
        }

        protected override IList<WaitForResult> RequestDependentAssets()
        {
            return new List<WaitForResult> 
            {
                WaitForAsset("face_detection_short_range.bytes"),
                WaitForAsset(refineFaceLandmarks ? "face_landmark_with_attention.bytes" : "face_landmark.bytes"),
                WaitForAsset("hand_landmark_full.bytes"),
                WaitForAsset("hand_recrop.bytes"),
                WaitForAsset("handedness.txt"),
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
            // 初始化與 Segmentation Mask 相關的輸出流
            _segmentationMaskStream = new OutputStream<ImageFrame>(calculatorGraph, _SegmentationMaskStreamName, true);

            using (var validatedGraphConfig = new ValidatedGraphConfig())
            {
                validatedGraphConfig.Initialize(config);

                var extensionRegistry = new ExtensionRegistry() { TensorsToDetectionsCalculatorOptions.Extensions.Ext, ThresholdingCalculatorOptions.Extensions.Ext };
                var cannonicalizedConfig = validatedGraphConfig.Config(extensionRegistry);

                var poseDetectionCalculatorPattern = new Regex("__posedetection[a-z]+__TensorsToDetectionsCalculator$");
                var tensorsToDetectionsCalculators = cannonicalizedConfig.Node.Where((node) => poseDetectionCalculatorPattern.Match(node.Name).Success).ToList();

                var poseTrackingCalculatorPattern = new Regex("tensorstoposelandmarksandsegmentation__ThresholdingCalculator$");
                var thresholdingCalculators = cannonicalizedConfig.Node.Where((node) => poseTrackingCalculatorPattern.Match(node.Name).Success).ToList();

                foreach (var calculator in tensorsToDetectionsCalculators)
                {
                    if (calculator.Options.HasExtension(TensorsToDetectionsCalculatorOptions.Extensions.Ext))
                    {
                        var options = calculator.Options.GetExtension(TensorsToDetectionsCalculatorOptions.Extensions.Ext);
                        options.MinScoreThresh = minDetectionConfidence;
                        Debug.Log($"Min Detection Confidence = {minDetectionConfidence}");
                    }
                }

                foreach (var calculator in thresholdingCalculators)
                {
                    if (calculator.Options.HasExtension(ThresholdingCalculatorOptions.Extensions.Ext))
                    {
                        var options = calculator.Options.GetExtension(ThresholdingCalculatorOptions.Extensions.Ext);
                        options.Threshold = minTrackingConfidence;
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
            var isInverted = ImageCoordinate.IsInverted(RotationAngle.Rotation0);
            var outputRotation = RotationAngle.Rotation0;
            var outputHorizontallyFlipped = !isInverted && false;
            var outputVerticallyFlipped = (!runningMode.IsSynchronous() && false) ^ (isInverted && false);

            if ((outputHorizontallyFlipped && outputVerticallyFlipped) || outputRotation == RotationAngle.Rotation180)
            {
                outputRotation = outputRotation.Add(RotationAngle.Rotation180);
                outputHorizontallyFlipped = !outputHorizontallyFlipped;
                outputVerticallyFlipped = !outputVerticallyFlipped;
            }

            sidePacket.Emplace("output_rotation", Packet.CreateInt((int)outputRotation));
            sidePacket.Emplace("output_horizontally_flipped", Packet.CreateBool(outputHorizontallyFlipped));
            sidePacket.Emplace("output_vertically_flipped", Packet.CreateBool(outputVerticallyFlipped));

            Debug.Log($"outtput_rotation = {outputRotation}, output_horizontally_flipped = {outputHorizontallyFlipped}, output_vertically_flipped = {outputVerticallyFlipped}");

            sidePacket.Emplace("refine_face_landmarks", Packet.CreateBool(refineFaceLandmarks));
            sidePacket.Emplace("model_complexity", Packet.CreateInt((int)modelComplexity));
            sidePacket.Emplace("smooth_landmarks", Packet.CreateBool(smoothLandmarks));
            sidePacket.Emplace("enable_segmentation", Packet.CreateBool(enableSegmentation));
            sidePacket.Emplace("smooth_segmentation", Packet.CreateBool(smoothSegmentation));

            return sidePacket;
        }
    }
}
