using Mediapipe;
using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using XPlan.ImageRecognize;
using XPlan.Interface;
using XPlan.Observe;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseChecker : LogicComponent, ITickable
    {
        private List<Vector3> landmarkList;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public PoseChecker()
        {
            landmarkList = null;

            RegisterNotify<PoseWorldLandListMsg>((msg) => 
            {
                landmarkList = msg.ptList.Select(x => x.pos).ToList();
            });
        }

        // Update is called once per frame
        public void Tick(float deltaTime)
        {
            if(landmarkList == null || landmarkList.Count != (int)BodyPoseType.NumOtType)
            {
                return;
            }

            if (BodyPoseChecker.IsRightBicepsCurl(landmarkList, 110f))
            {
                DirectCallUI<string>(UICommand.RightBicepsCurl, "Right Biceps Curl");
            }
            else
            {
                DirectCallUI<string>(UICommand.RightBicepsStraight, "Right Biceps Straight");
            }

            if (BodyPoseChecker.IsLeftBicepsCurl(landmarkList, 110f))
            {
                DirectCallUI<string>(UICommand.LeftBicepsCurl, "Left Biceps Curl");
            }
            else
            {
                DirectCallUI<string>(UICommand.LeftBicepsStraight, "Left Biceps Straight");
            }
        }
    }
}
