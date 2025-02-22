using UnityEngine;

namespace XPlan.ImageRecognize.HumanJoint
{
    public class RightElbowCalculator: RightLimbJointCalculator
    {   
        public RightElbowCalculator (Transform t) : base(t) {}

        public override void Calc () 
        {
            if (_landmarkList == null) return;

            Refresh();

            obj.Rotate(
                Quaternion.FromToRotation(-obj.right, v_elbow_wrist).eulerAngles,
                Space.World
            );

            var norm_x = Vector3.Cross(v_wrist_index, v_wrist_pinky);
            obj.Rotate(
                Quaternion.FromToRotation(obj.forward, norm_x).eulerAngles,
                Space.World
            );
        }
    }
};
