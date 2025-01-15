using System;
using System.Collections;
using System.Linq;
using UnityEngine;

using Mediapipe.Unity;

using XPlan.Utility;

namespace XPlan.ImageRecognize
{
    public class CamTextureSource : ImageSource
    {
        private WebCamController webCamController;
        private WebCamDevice webCamDevice;

        public override string sourceName
        {
            get
            {
                return (webCamController == null) ? null : webCamController.GetDeviceName();
            }
        }
        public override string[] sourceCandidateNames
        {
            get
            {
                return WebCamTexture.devices.Select(device => device.name).ToArray();
            }
        }
        public override ResolutionStruct[] availableResolutions
        {
            get
            {
                return webCamDevice.name == ""? null : webCamDevice.availableResolutions.Select(resolution => new ResolutionStruct(resolution)).ToArray();
            }
        }
        public override bool isPrepared
        {
            get
            {
                 return webCamController != null;
            }
        }
        public override bool isPlaying
        {
            get
            {
                 return webCamController != null && webCamController.IsPlaying();
            }
        }
        public override int textureWidth
        { 
            get
            {
                return !isPrepared ? 0 : webCamController.Width;
            }
        }
        public override int textureHeight
        {
            get
            {
                return !isPrepared ? 0 : webCamController.Height;
            }
        }

        public CamTextureSource()
        {
            webCamController = WebCamUtility.GenerateCamController();

            int idx = Array.FindIndex<WebCamDevice>(WebCamTexture.devices, (device) => 
            {
                return device.name == webCamController.GetDeviceName();
            });

            if(idx == -1)
            {
                return;
            }

            webCamDevice = WebCamTexture.devices[idx];
        }

        public override Texture GetCurrentTexture()
        {
            return webCamController == null ? null: webCamController.GetTexture();
        }

        public override IEnumerator Play()
        {
            webCamController.Play();
            yield return new WaitUntil(() => webCamController.IsDeviceReady());
        }

        public override IEnumerator Resume()
        {
            if (!isPrepared)
            {
                throw new InvalidOperationException("WebCamTexture is not prepared yet");
            }
            if (!webCamController.IsPlaying())
            {
                webCamController.Play();
            }
            yield return WaitForWebCamTexture();
        }

        public override void Pause()
        {
            if (isPlaying)
            {
                webCamController.Pause();
            }
        }

        public override void Stop()
        {
            if (webCamController != null)
            {
                webCamController.Stop();
            }
            webCamController = null;
        }

        public override void SelectSource(int sourceId)
        {
            if (sourceId < 0 || sourceId >= WebCamTexture.devices.Length)
            {
                throw new ArgumentException($"Invalid source ID: {sourceId}");
            }

            webCamDevice = WebCamTexture.devices[sourceId];
        }

        private IEnumerator WaitForWebCamTexture()
        {
            const int timeoutFrame  = 2000;
            int count               = 0;

            Debug.Log("Waiting for WebCamTexture to start");
            yield return new WaitUntil(() => count++ > timeoutFrame || webCamController.Width > 16);

            if (webCamController.Width <= 16)
            {
                throw new TimeoutException("Failed to start WebCam");
            }
        }
    }
}
