using Mediapipe;
using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using XPlan.ImageRecognize;
using XPlan.Observe;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseChecker : MonoBehaviour, INotifyReceiver
    {
        public Func<string> GetLazyZoneID { get; set; }

        private List<Vector3> landmarkList;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            landmarkList = null;

            NotifySystem.Instance.RegisterNotify<PoseWorldLandListMsg>(this, (receiver) => 
            {
                PoseWorldLandListMsg msg    = receiver.GetMessage<PoseWorldLandListMsg>();
                landmarkList                = msg.ptList.Select(x => x.pos).ToList();
            });
        }

        // Update is called once per frame
        void Update()
        {
            if(landmarkList == null || landmarkList.Count != (int)BodyPoseType.NumOtType)
            {
                return;
            }

            if (BodyPoseChecker.IsRightBicepsCurl(landmarkList, 110f))
            {
                Debug.Log("Right Biceps Curl");
            }
            else
            {
                Debug.Log("Right Biceps Straight");
            }

            if (BodyPoseChecker.IsLeftBicepsCurl(landmarkList, 110f))
            {
                Debug.Log("Left Biceps Curl");
            }
            else
            {
                Debug.Log("Left Biceps Straight");
            }
        }
    }
}
