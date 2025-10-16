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
            List<Vector3> poseCenterList = new List<Vector3>();

            RegisterNotify<TexturePrepareMsg>((msg) =>
            {
                DirectCallUI<ImageSource>(UICommand.InitScreen, (msg.imageSource));
            });

            RegisterNotify<PoseLandListMsg>((msg) => 
            {
                DirectCallUI<(int, List<PTInfo>, bool)>(UICommand.UpdatePose, (msg.index, msg.ptList, msg.bIsMirror));
            });

            RegisterNotify<MediapipePoseMaskMsg>((msg) =>
            {
                DirectCallUI<Mediapipe.Image>(UICommand.UpdatePoseMask, (msg.maskImg));
            });
        }
    }
}
