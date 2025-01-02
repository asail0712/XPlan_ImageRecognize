using UnityEngine;

namespace XPlan.MediaPipe.HumanJoint
{
    public class HipCalculator: HumanJointCalculator
    {
        GameObject leftHip;
        GameObject rightHip;

        public HipCalculator (Transform t) : base(t) 
        {
            //leftHip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //rightHip = GameObject.CreatePrimitive(PrimitiveType.Cube);

            //leftHip.transform.parent = obj;            
            //rightHip.transform.parent = obj;

            //leftHip.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            //rightHip.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }

        public override void Calc () 
        {
            if (_landmarkList == null) return;

            //leftHip.transform.position = new Vector3(_landmarkList.Landmark[24].X, -_landmarkList.Landmark[24].Y, -_landmarkList.Landmark[24].Z) + obj.position; 
            //rightHip.transform.position = new Vector3(_landmarkList.Landmark[23].X, -_landmarkList.Landmark[23].Y, -_landmarkList.Landmark[23].Z) + obj.position;

            //var v_hips = new Vector3(
            //    -_landmarkList.Landmark[24].X + _landmarkList.Landmark[23].X,
            //    _landmarkList.Landmark[24].Y - _landmarkList.Landmark[23].Y,
            //    _landmarkList.Landmark[24].Z - _landmarkList.Landmark[23].Z
            //);

            var v_hips = new Vector3(
                _landmarkList.Landmark[24].X - _landmarkList.Landmark[23].X,
                _landmarkList.Landmark[24].Y - _landmarkList.Landmark[23].Y,
                _landmarkList.Landmark[24].Z - _landmarkList.Landmark[23].Z
            );

            Vector3 hipRotate = Quaternion.FromToRotation(obj.forward, v_hips).eulerAngles;

            obj.Rotate(
                hipRotate,
                Space.World
            );
        }
    }
};
