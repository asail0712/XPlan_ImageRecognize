using Mediapipe;
using Mediapipe.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

using XPlan.Observe;

namespace XPlan.ImageRecognize.Demo
{
    public class ReceivePoseWorldData : MonoBehaviour, INotifyReceiver
    {
        [SerializeField] private PoseWorldLandmarkListAnnotationController annotationController;

        private LandmarkList landmarkList;

        public Func<string> GetLazyZoneID { get; set; } = () => "";

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            NotifySystem.Instance.RegisterNotify<MediapipeLandmarkListMsg>(this, (msgReceiver) =>
            {
                MediapipeLandmarkListMsg msg    = msgReceiver.GetMessage<MediapipeLandmarkListMsg>();
                landmarkList                    = msg.landmarkList;

                annotationController.isMirrored = msg.bIsMirror;
            });
        }

        void Update()
        {
            if(landmarkList == null)
            {
                return;
            }

            annotationController.DrawNow(landmarkList);
        }
    }
}
