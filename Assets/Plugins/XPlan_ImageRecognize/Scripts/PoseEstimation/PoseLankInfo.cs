using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;
using UnityEngine;
using XPlan.Utility;


namespace XPlan.ImageRecognize
{
    public class PoseLankInfo
    {
        private List<Vector3> posePtList;

        public PoseLankInfo() 
        {
            posePtList = new List<Vector3>();
        }

        public void SetLandmarks(List<NormalizedLandmark> landmarkList, bool bVisualZ)
        {
            posePtList.Clear();

            for (int i = 0; i < landmarkList.Count; ++i)
            {
                posePtList.Add(new Vector3(landmarkList[i].x, landmarkList[i].y, bVisualZ? landmarkList[i].z:0f));
            }            
        }

        public void ClearLandmarks()
        {
            posePtList.Clear();
        }

        public List<Vector3> GetPtList()
        {
            return posePtList;
        }

        public Vector3 GetHipCenter()
        {
            if (posePtList == null || posePtList.Count == 0)
            {
                return Vector3.zero;
            }

            int leftHipIdx  = (int)BodyPoseType.LeftHip;
            int rightHipIdx = (int)BodyPoseType.RightHip;

            if (!posePtList.IsValidIndex(leftHipIdx) 
            || !posePtList.IsValidIndex(rightHipIdx))
            {
                return Vector3.zero;
            }

            return (posePtList[leftHipIdx] + posePtList[rightHipIdx]) / 2f;
        }


        public float DisSqrToScreenCenter(bool bVisualZ)
        {
            if (posePtList == null || posePtList.Count == 0)
            {
                return float.MaxValue;
            }

            // 計算中心點
            Vector3 center = Vector3.zero;
            foreach (Vector3 pt in posePtList)
            {
                center += new Vector3(pt.x, pt.y, bVisualZ?pt.z:0f);
            }
            center /= posePtList.Count;

            // 因為螢幕中心是(0.5,0.5,0)，因此向量長度就是距離
            if(bVisualZ)
            {
                return (center - new Vector3(0.5f, 0.5f, 0f)).sqrMagnitude;
            }
            else
            {
                return (new Vector2(center.x, center.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
            }
        }
    }
}
