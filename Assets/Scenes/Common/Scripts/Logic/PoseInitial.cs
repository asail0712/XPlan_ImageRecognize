using System.Collections.Generic;

using UnityEngine;

using Mediapipe.Unity;

using XPlan;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseInitial : LogicComponent
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public PoseInitial()
        {
            RegisterNotify<PoseLandListMsg>((msg) => 
            {
                DirectCallUI<(List<Vector3>, bool)>(UICommand.UpdatePos, (msg.landmarkList, msg.bIsMirror));
            });

            RegisterNotify<PoseWorldLandListMsg>((msg) =>
            {

            });
        }
    }
}
