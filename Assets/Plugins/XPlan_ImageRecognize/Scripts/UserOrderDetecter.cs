using System.Collections.Generic;
using UnityEngine;

namespace XPlan.ImageRecognize
{
    public enum UserOrderType
    {
        Center,
        LeftToRight,
        RightToLeft,
        NearToFar,
    }

    public static class UserOrderDetecter
    {
        public static List<int> OrderBy(this List<PoseLankInfo> poseList, UserOrderType orderType)
        {
            List<int> closestPoseIndices = null;

            switch (orderType)
            {
                case UserOrderType.Center:
                    closestPoseIndices = OrderByCenter(poseList);
                    break;
                case UserOrderType.LeftToRight:
                    
                    break;
                case UserOrderType.RightToLeft:

                    break;
                case UserOrderType.NearToFar:

                    break;
                default:
                    closestPoseIndices = new List<int>();
                    break;
            }

            return closestPoseIndices;
        }

        static private List<int> OrderByCenter(List<PoseLankInfo> poseList)
        {
            List<(int, float)> disSqrList   = new List<(int, float)>();
            List<int> closestPoseIndices    = new List<int>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                // 為了判斷離中間的距離
                disSqrList.Add((i, poseList[i].DisSqrToScreenCenter(false)));
            }

            disSqrList.Sort((x1, x2) =>
            {
                return x1.Item2.CompareTo(x2.Item2);
            });

            // 依照與螢幕中間距離 排出由小到大的index
            for (int i = 0; i < disSqrList.Count; ++i)
            {
                closestPoseIndices.Add(disSqrList[i].Item1);
            }

            return closestPoseIndices;
        }
    }
}
