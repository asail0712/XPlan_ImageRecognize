using Intel.RealSense;
using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;

using UnityEngine;
using XPlan.Utility;

using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;

namespace XPlan.ImageRecognize
{
    public class PoseEstimationAdapter : LogicComponent
    {
        private bool bMirror;
        private List<PoseLankInfo> pose2DList;
        private PoseLankInfo pose3D;
        private int numShowPose = 1;

        private List<Landmarks> reservePoseWorldLandmarkList;

        public PoseEstimationAdapter(PoseEstimationRunner poseRunner, int numShowPose, bool bMirror, float ptSmoothAlpha, float ptSnapDistance)
        {            
            this.bMirror        = bMirror;
            this.pose2DList     = new List<PoseLankInfo>();
            this.pose3D         = new PoseLankInfo();
            this.numShowPose    = numShowPose;

            for (int i = 0; i < poseRunner.config.NumPoses; ++i)
            {
                pose2DList.Add(new PoseLankInfo() 
                {
                    SmoothAlpha     = ptSmoothAlpha,
                    snapDistance    = ptSnapDistance,
                });
            }

            pose3D.SmoothAlpha  = ptSmoothAlpha;
            pose3D.snapDistance = ptSnapDistance;

            poseRunner.imgInitFinish += (imgSource) => 
            {
                SendGlobalMsg<TexturePrepareMsg>(imgSource);
            };

            poseRunner.resultReceived += (result) =>
            {
                ProcessPoseLandmark(result);
            };
        }

        private void ProcessPoseLandmark(PoseLandmarkerResult result)
        {
            List<NormalizedLandmarks> poseLandmarksList = result.poseLandmarks;
            reservePoseWorldLandmarkList                = result.poseWorldLandmarks;

            // 依照2D資料確認要選取的pose index 再傳出3D資訊

            /***************************************
             * 依照 pose數量 修改 pose2DList資料
             * ************************************/
            int currPoseCount = poseLandmarksList != null ? poseLandmarksList.Count : 0;

            // 若沒有任何 pose，就送出空清單 且避免清除pose 2d資料
            if (currPoseCount == 0)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<Vector3>(), bMirror);
                return;
            }

            for (int i = 0; i < pose2DList.Count; ++i)
            {
                bool b = poseLandmarksList != null && i < poseLandmarksList.Count;

                if (b)
                {
                    // 加入pose資料
                    pose2DList[i].AddFrameLandmarks(poseLandmarksList[i].landmarks);
                }
                else
                {
                    // 沒找到目標 因此移除pose資料
                    pose2DList[i].ClearLandmarks();
                }
            }

            /***************************************
             * 檢查 pose 過近時 要刪除其中一個
             * ************************************/
            int totalPoseCount = currPoseCount;

            if (currPoseCount > 1)
            {
                currPoseCount = FilterTooNearPose(ref pose2DList, currPoseCount);
            }

            /*************************************************************
             * 依照 numShowPose 決定顯示幾個Pose (以靠近中間為主)
             * **********************************************************/
            int closestIdx          = -1;
            int filterNearestPose   = currPoseCount;
            int finalPoseCount      = KeepClosestToCenter(ref pose2DList, ref closestIdx);

            Debug.Log($"Total Pose Count: {totalPoseCount}, Filter Nearest Pose: {filterNearestPose}, Final Pose Count: {finalPoseCount}, Closest Index: {closestIdx}");

            /*************************************************************
             * 將 pose 2D 資料送出
             * **********************************************************/
            List<Vector3> posList = new List<Vector3>();

            for (int i = 0; i < pose2DList.Count; ++i)
            {
                posList.AddRange(pose2DList[i].GetPtList());
            }

            SendGlobalMsg<PoseLandListMsg>(posList, bMirror);

            /*************************************************************
             * 將 pose 3D 資料送出
             * **********************************************************/
            if(reservePoseWorldLandmarkList.IsValidIndex(closestIdx))
            {
                List<Landmark> landmarks    = reservePoseWorldLandmarkList[closestIdx].landmarks;
                pose3D.AddFrameLandmarks(landmarks);
                List<Vector3> vecList       = pose3D.GetPtList();

                SendGlobalMsg<MediapipeLandmarkListMsg>(vecList.ToMpLandmarkList(), bMirror);
            }
            else
            {
                pose3D.ClearLandmarks();
            }
        }

        private void ProcessPoseWorldLandmark(List<Landmarks> poseWorldLandmarkList)
        {
            reservePoseWorldLandmarkList = poseWorldLandmarkList;

            //    if (poseWorldLandmarkList == null)
            //    {
            //        SendGlobalMsg<PoseWorldLandListMsg>(new List<Vector3>(), bMirror);
            //        return;
            //    }

            //    List<Vector3> posLost = new List<Vector3>();
            //    IReadOnlyList<Landmark> landmarkList = poseWorldLandmarkList.landmarks;

            //    for (int i = 0; i < poseWorldLandmarkList.Count; ++i)
            //    {
            //        Vector3 p = new Vector3(poseWorldLandmarkList[i].X, poseWorldLandmarkList[i].Y, poseWorldLandmarkList[i].Z);
            //        posLost.Add(p);
            //    }

            //    SendGlobalMsg<PoseWorldLandListMsg>(posLost, bMirror);
        }

        private int FilterTooNearPose(ref List<PoseLankInfo> poseList, int currPoseCount, float thredhoild = 0.05f)
        {
            float sqrThreshold      = Mathf.Pow(thredhoild, 2f);            // 可依實測調整
            List<int> delIdxList    = new List<int>();

            for (int i = 0; i < poseList.Count - 1; ++i)
            {
                for (int j = i + 1; j < poseList.Count; ++j)
                {
                    Vector3 hipCenter1 = poseList[i].GetHipCenter();
                    Vector3 hipCenter2 = poseList[j].GetHipCenter();

                    float hipSqr = (hipCenter1 - hipCenter2).sqrMagnitude;
                    if (hipSqr <= sqrThreshold)
                    {
                        delIdxList.AddUnique(j);
                    }
                }
            }

            if (delIdxList.Count > 0)
            {
                for (int k = delIdxList.Count - 1; k >= 0; --k)
                {
                    poseList[delIdxList[k]].ClearLandmarks();
                }
            }

            currPoseCount -= delIdxList.Count;

            return currPoseCount;
        }

        private int KeepClosestToCenter(ref List<PoseLankInfo> poseList, ref int closestPoseIndex)
        {
            List<(int, float)> disSqrList = new List<(int, float)>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                // 為了判斷離中間的距離
                disSqrList.Add((i, poseList[i].DisSqrToScreenCenter(false)));
            }

            disSqrList.Sort((x1, x2) =>
            {
                return x1.Item2.CompareTo(x2.Item2);
            });

            // 取前 numShowPose
            for (int i = 0; i < disSqrList.Count; ++i)
            {
                int idx = disSqrList[i].Item1;

                // 把過遠的資料移除
                if (i >= numShowPose)
                {
                    poseList[idx].ClearLandmarks();
                }
            }

            // 回傳最靠近螢幕中間的index
            if (disSqrList.Count == 0)
            {
                closestPoseIndex = -1;
            }
            else
            {
                closestPoseIndex = disSqrList[0].Item1;
            }

            int poseNum = 0;

            for (int i = 0; i < poseList.Count; ++i)
            {
                if(poseList[i].HasPose())
                {
                    ++poseNum;
                }
            }

            return poseNum;
        }
    }
}
