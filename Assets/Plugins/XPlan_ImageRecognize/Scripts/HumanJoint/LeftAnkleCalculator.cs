using UnityEngine;

namespace XPlan.ImageRecognize.HumanJoint
{
    public class LeftAnkleCalculator: LeftLimbJointCalculator
    {   
        public LeftAnkleCalculator (Transform t) : base(t) {}

        public override void Calc () 
        {
            if (_landmarkList == null) return;

            Refresh();

            obj.Rotate(
                Quaternion.FromToRotation(-obj.right, v_ankle_heel).eulerAngles,
                Space.World
            );
        }
    }
};
