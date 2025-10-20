using System.Collections.Generic;
using UnityEngine;

namespace XPlan.ImageRecognize
{
    public enum UserOrderType
    {
        Center,
        LeftToRight,
        RightToLeft,
        ROI,
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
                    closestPoseIndices = OrderByLeft(poseList);
                    break;
                case UserOrderType.RightToLeft:
                    closestPoseIndices = OrderByRight(poseList);
                    break;
                case UserOrderType.ROI:
                    closestPoseIndices = OrderByROI(poseList);
                    break;
                default:
                    closestPoseIndices = new List<int>();
                    break;
            }

            return closestPoseIndices;
        }

        static private List<int> OrderByLeft(List<PoseLankInfo> poseList)
        {
            List<(int, float)> disList      = new List<(int, float)>();
            List<int> closestPoseIndices    = new List<int>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                // 為了判斷最靠近左邊的位置
                if (!poseList[i].IsValid())
                {
                    disList.Add((i, float.MaxValue));
                }
                else
                {
                    disList.Add((i, poseList[i].GetHipCenter().x));
                }                    
            }

            disList.Sort((x1, x2) =>
            {
                return x1.Item2.CompareTo(x2.Item2);
            });

            // 依照與螢幕左邊距離 排出由小到大的index
            for (int i = 0; i < disList.Count; ++i)
            {
                closestPoseIndices.Add(disList[i].Item1);
            }

            return closestPoseIndices;
        }

        static private List<int> OrderByRight(List<PoseLankInfo> poseList)
        {
            List<(int, float)> disList      = new List<(int, float)>();
            List<int> closestPoseIndices    = new List<int>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                // 為了判斷最靠近左邊的位置
                if (!poseList[i].IsValid())
                {
                    disList.Add((i, float.MinValue));
                }
                else
                {
                    disList.Add((i, poseList[i].GetHipCenter().x));
                }
            }

            disList.Sort((x1, x2) =>
            {
                return x2.Item2.CompareTo(x1.Item2);
            });

            // 依照與螢幕左邊距離 排出由小到大的index
            for (int i = 0; i < disList.Count; ++i)
            {
                closestPoseIndices.Add(disList[i].Item1);
            }

            return closestPoseIndices;
        }

        static private List<int> OrderByROI(List<PoseLankInfo> poseList)
        {
            List<(int, float)> roiAreaList  = new List<(int, float)>();
            List<int> closestPoseIndices    = new List<int>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                // 為了判斷最靠近左邊的位置
                if (!poseList[i].IsValid())
                {
                    roiAreaList.Add((i, float.MinValue));
                }
                else
                {
                    Rect rect   = poseList[i].GetROI();
                    float area  = rect.width * rect.height;

                    roiAreaList.Add((i, area));
                }
            }

            roiAreaList.Sort((x1, x2) =>
            {
                return x2.Item2.CompareTo(x1.Item2);
            });

            // 依照ROI 排出由大到小的index
            for (int i = 0; i < roiAreaList.Count; ++i)
            {
                closestPoseIndices.Add(roiAreaList[i].Item1);
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
