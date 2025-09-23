using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using Google.Protobuf;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Sample;

using TextureFrame = Mediapipe.Unity.Experimental.TextureFrame;

namespace XPlan.ImageRecognize
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
            Lite    = 0,
            Full    = 1,
            Heavy   = 2,
        }

        public ModelComplexity modelComplexity  = ModelComplexity.Lite;
        public bool enableSegmentation          = true;
        public bool smoothSegmentation          = true;

        public event EventHandler<OutputStream<ImageFrame>.OutputEventArgs> OnSegmentationMaskOutput
        {
            add     => _segmentationMaskStream.AddListener(value, timeoutMicrosec);
            remove  => _segmentationMaskStream.RemoveListener(value);
        }

        private const string _InputStreamName               = "input_video";
        private const string _SegmentationMaskStreamName    = "segmentation_mask";

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
            base.Stop();

            _segmentationMaskStream?.Dispose();
            _segmentationMaskStream = null;
        }

        public void AddTextureFrameToInputStream(TextureFrame textureFrame, GlContext glContext = null)
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
                WaitForAsset("face_landmark.bytes"),
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

                var extensionRegistry       = new ExtensionRegistry() { TensorsToDetectionsCalculatorOptions.Extensions.Ext, ThresholdingCalculatorOptions.Extensions.Ext };
                var cannonicalizedConfig    = validatedGraphConfig.Config(extensionRegistry);
                
                calculatorGraph.Initialize(cannonicalizedConfig);
            }
        }

        private PacketMap BuildSidePacket(ImageSource imageSource)
        {
            var sidePacket = new PacketMap();

            SetImageTransformationOptions(sidePacket, imageSource);

            // TODO: refactoring
            // The orientation of the output image must match that of the input image.
            var isInverted                  = ImageCoordinate.IsInverted(RotationAngle.Rotation0);
            var outputRotation              = RotationAngle.Rotation0;
            var outputHorizontallyFlipped   = !isInverted && false;
            var outputVerticallyFlipped     = (!runningMode.IsSynchronous() && false) ^ (isInverted && false);

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

            sidePacket.Emplace("refine_face_landmarks", Packet.CreateBool(false));
            sidePacket.Emplace("model_complexity", Packet.CreateInt((int)modelComplexity));
            sidePacket.Emplace("smooth_landmarks", Packet.CreateBool(false));
            sidePacket.Emplace("enable_segmentation", Packet.CreateBool(enableSegmentation));
            sidePacket.Emplace("smooth_segmentation", Packet.CreateBool(smoothSegmentation));

            return sidePacket;
        }
    }
}
