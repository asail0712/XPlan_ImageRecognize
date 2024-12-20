// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

using XPlan.Utility;

using Logger                    = Mediapipe.Logger;
using TextureFramePool          = Mediapipe.Unity.Experimental.TextureFramePool;
using RunningMode               = Mediapipe.Tasks.Vision.Core.RunningMode;
using ImageProcessingOptions    = Mediapipe.Tasks.Vision.Core.ImageProcessingOptions;
using Delegate                  = Mediapipe.Tasks.Core.BaseOptions.Delegate;

namespace XPlan.MediaPipe
{
    public class HumannoidAcatarRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        /**********************************************************
         * Debug工具
         **********************************************************/
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;
        /**********************************************************/

        private TextureFramePool _textureFramePool;
        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        protected override IEnumerator Run()
        {
            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Model = {config.ModelName}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumPoses = {config.NumPoses}");
            Debug.Log($"MinPoseDetectionConfidence = {config.MinPoseDetectionConfidence}");
            Debug.Log($"MinPosePresenceConfidence = {config.MinPosePresenceConfidence}");
            Debug.Log($"MinTrackingConfidence = {config.MinTrackingConfidence}");
            Debug.Log($"OutputSegmentationMasks = {config.OutputSegmentationMasks}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            /**********************************************************
             * Debug工具
             **********************************************************/
            var options     = config.GetPoseLandmarkerOptions(config.RunningMode == RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null);
            /**********************************************************/
            taskApi         = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
            Logger.LogError(TAG, "Failed to start ImageSource, exiting...");
            yield break;
            }

            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so maybe the following code needs to be fixed.
            _textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            /**********************************************************
             * Debug工具
             **********************************************************/
            screen.Initialize(imageSource);

            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);
            /**********************************************************/

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally      = transformationOptions.flipHorizontally;
            var flipVertically        = transformationOptions.flipVertically;

            // Always setting rotationDegrees to 0 to avoid the issue that the detection becomes unstable when the input image is rotated.
            // https://github.com/homuler/MediaPipeUnityPlugin/issues/1196
            var imageProcessingOptions    = new ImageProcessingOptions(rotationDegrees: 0);

            AsyncGPUReadbackRequest req   = default;
            var waitUntilReqDone          = new WaitUntil(() => req.done);
            var result                    = PoseLandmarkerResult.Alloc(options.numPoses, options.outputSegmentationMasks);

            // NOTE: we can share the GL context of the render thread with MediaPipe (for now, only on Android)
            var canUseGpuImage            = options.baseOptions.delegateCase == Delegate.GPU &&
                                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 &&
                                GpuManager.GpuResources != null;
            using var glContext           = canUseGpuImage ? GpuManager.GetGlContext() : null;

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

                // Build the input Image
                Image image;
                if (canUseGpuImage)
                {
                    yield return new WaitForEndOfFrame();
                    Texture texture = imageSource.GetCurrentTexture();

                    if (texture == null)
                    {
                        yield return new WaitForEndOfFrame();
                        continue;
                    }

                    textureFrame.ReadTextureOnGPU(texture, flipHorizontally, flipVertically);
                    image = textureFrame.BuildGpuImage(glContext);
                }
                else
                {
                    Texture texture = imageSource.GetCurrentTexture();

                    if (texture == null)
                    {
                        yield return new WaitForEndOfFrame();
                        continue;
                    }

                    req = textureFrame.ReadTextureAsync(texture, flipHorizontally, flipVertically);
                    yield return waitUntilReqDone;

                    if (req.hasError)
                    {
                        Debug.LogWarning($"Failed to read texture from the image source, exiting...");
                        yield return new WaitForEndOfFrame();
                        continue;
                    }
                    image = textureFrame.BuildCPUImage();
                    textureFrame.Release();
                }

                /**********************************************************
                 * Debug工具
                 **********************************************************/
                switch (taskApi.runningMode)
                {
                    case RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                        {
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;
                    case RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
                        {
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;
                    case RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                    break;
                }
                /**********************************************************/
            }
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
        {
            /**********************************************************
             * Debug工具
             **********************************************************/
            _poseLandmarkerResultAnnotationController.DrawLater(result);
            /**********************************************************/
        }
    }
}
