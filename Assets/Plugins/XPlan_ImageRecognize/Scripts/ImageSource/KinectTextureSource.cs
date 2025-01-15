using System;
using System.Collections;
using System.Linq;
using UnityEngine;

using Mediapipe.Unity;

#if KINECT
using com.rfilkov.kinect;
#endif // KINECT

using XPlan.Utility;

namespace XPlan.ImageRecognize
{
    public class KinectTextureSource : ImageSource
    {
        static private string KinectDeviceName = "KinectDevice";

        private int sensorIdx = 0;

        public override string sourceName
        {
            get
            {
                return KinectDeviceName;
            }
        }
        public override string[] sourceCandidateNames
        {
            get
            {
                return new string[]{ KinectDeviceName };
            }
        }
        public override ResolutionStruct[] availableResolutions
        {
            get
            {
#if KINECT
                return new ResolutionStruct[] 
                { 
                    new ResolutionStruct(KinectManager.Instance.GetColorImageWidth(sensorIdx),
                                            KinectManager.Instance.GetColorImageHeight(sensorIdx),
                                            1.0 / (double)KinectManager.Instance.GetColorFrameTime(sensorIdx)) 
                };
#else
                return null;
#endif
            }
        }
        public override bool isPrepared
        {
            get
            {
#if KINECT
                return true;
#else
                return false;
#endif
            }
        }
        public override bool isPlaying
        {
            get
            {
#if KINECT
                return KinectManager.Instance.IsPlayModeEnabled();
#else
                return false;
#endif
            }
        }
        public override int textureWidth
        { 
            get
            {
#if KINECT
                return KinectManager.Instance.GetColorImageWidth(sensorIdx);
#else
                return 0;
#endif
            }
        }
        public override int textureHeight
        {
            get
            {
#if KINECT
                return KinectManager.Instance.GetColorImageHeight(sensorIdx);
#else
                return 0;
#endif
            }
        }

        public KinectTextureSource(int sensorIdx = 0)
        {
            this.sensorIdx = sensorIdx;
        }

        public override Texture GetCurrentTexture()
        {
#if KINECT
            return KinectManager.Instance.GetColorImageTex(sensorIdx);
#else
            return null;
#endif
        }

        public override IEnumerator Play()
        {
            yield return null;
        }

        public override IEnumerator Resume()
        {
            yield return null;
        }

        public override void Pause()
        {

        }

        public override void Stop()
        {

        }

        public override void SelectSource(int sourceId)
        {
            
        }
    }
}
