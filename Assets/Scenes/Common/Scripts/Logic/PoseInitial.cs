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
                DirectCallUI<(List<Vector3>, int, bool)>(UICommand.UpdatePos, (msg.landmarkList, msg.count, msg.bIsMirror));
            });

            RegisterNotify<PoseWorldLandListMsg>((msg) =>
            {

            });
        }
    }
}
