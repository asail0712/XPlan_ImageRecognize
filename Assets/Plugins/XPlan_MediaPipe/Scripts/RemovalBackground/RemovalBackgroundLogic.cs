using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

using XPlan.Observe;
using XPlan.UI;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.MediaPipe
{    
    public enum MaskType
    {
        FloatArray,
        ColorArray,
    }

    public class ColorMaskMsg : MessageBase
    {
        public Color32[] maskColorArray;
        public int width;
        public int height;

        public ColorMaskMsg(Color32[] maskColorArray, int width, int height)
        {
            this.maskColorArray = maskColorArray;
            this.width          = width;
            this.height         = height;
        }
    }

    public class FloatMaskMsg : MessageBase
    {
        public float[] maskArray;

        public FloatMaskMsg(float[] maskArray)
        {
            this.maskArray = maskArray;
        }
    }

    public class RemovalBackgroundLogic : LogicComponent
    {
        private RemovalBackgroundGraph graphRunner;
        private RunningMode runningMode;
        private ImageSource imageSource;

        private float[] maskArray;
        private Color32[] maskColorArray;
        private MaskType maskType;

        public RemovalBackgroundLogic(MaskType maskType)
        {
            this.graphRunner    = null;
            this.runningMode    = RunningMode.Async;
            this.imageSource    = null;
            this.maskType       = maskType;

            RegisterNotify<GraphRunnerPrepareMsg>((msg) => 
            {
                runningMode = msg.runningMode;
                graphRunner = (RemovalBackgroundGraph)msg.graphRunner;

                StartRun();
            });

            RegisterNotify<TexturePrepareMsg>((msg) =>
            {
                imageSource     = msg.imageSource;
                maskArray       = new float[imageSource.textureWidth * imageSource.textureHeight];
                maskColorArray  = new Color32[imageSource.textureWidth * imageSource.textureHeight];

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

        protected IEnumerator Run(RemovalBackgroundGraph graphRunner, RunningMode runningMode, ImageSource imageSource)
        {
            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so the following code must be fixed.
            TextureFramePool textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            if (!runningMode.IsSynchronous())
            {
                graphRunner.OnSegmentationMaskOutput += OnSegmentationMaskOutput;
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
                    Task<RemovalBackgroundResult> task  = graphRunner.WaitNextAsync();

                    yield return new WaitUntil(() => task.IsCompleted);

                    RemovalBackgroundResult result      = task.Result;
                    ProcessImageFrame(result.segmentationMask);
                    result.segmentationMask?.Dispose();
                }
            }
        }
        private void OnSegmentationMaskOutput(object stream, OutputStream<ImageFrame>.OutputEventArgs eventArgs)
        {
            Packet<ImageFrame> packet   = eventArgs.packet;
            ImageFrame imgFrame         = packet == null ? default : packet.Get();

            ProcessImageFrame(imgFrame);

            imgFrame?.Dispose();
        }

        private void ProcessImageFrame(ImageFrame imgFrame)
        {
            if (imgFrame == null)
            {
                return;
            }


            bool bResult = false;

            switch(maskType)
            {
                case MaskType.FloatArray:
                    bResult = imgFrame.TryReadChannelNormalized(0, maskArray);
                    break;
                case MaskType.ColorArray:
                    bResult = imgFrame.TryReadPixelData(maskColorArray);
                    break;
            }

            if(!bResult)
            {
                return;
            }

            switch (maskType)
            {
                case MaskType.FloatArray:
                    SendGlobalMsg<FloatMaskMsg>(maskArray);
                    break;
                case MaskType.ColorArray:
                    SendGlobalMsg<ColorMaskMsg>(maskColorArray, imgFrame.Width(), imgFrame.Height());
                    break;
            }
        }
    }
}
