using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;

using UnityEngine;
using XPlan.Utility;

using Landmark = Mediapipe.Landmark;

namespace XPlan.ImageRecognize
{
    public class PoseEstimationAdapter : LogicComponent
    {
        private bool bMirror;
        private List<PoseLankInfo> poseList;
        private int numShowPose;

        private List<Landmarks> reservePoseWorldLandmarkList;

        public PoseEstimationAdapter(PoseEstimationRunner poseRunner, int numShowPose, bool bMirror, float ptSmoothAlpha, float ptSnapDistance)
        {            
            this.bMirror        = bMirror;
            this.poseList       = new List<PoseLankInfo>();
            this.numShowPose    = numShowPose;

            for (int i = 0; i < poseRunner.config.NumPoses; ++i)
            {
                poseList.Add(new PoseLankInfo() 
                {
                    SmoothAlpha     = ptSmoothAlpha,
                    snapDistance    = ptSnapDistance,
                });
            }

            poseRunner.imgInitFinish += (imgSource) => 
            {
                SendGlobalMsg<TexturePrepareMsg>(imgSource);
            };

            poseRunner.resultReceived += (result) =>
            {
                ProcessPoseLandmark(result.poseLandmarks);
                ProcessPoseWorldLandmark(result.poseWorldLandmarks);
            };
        }

        private void ProcessPoseLandmark(List<NormalizedLandmarks> poseLandmarksList)
        {
            /***************************************
             * 依照 pose數量 改變 poseList
             * ************************************/
            int currPoseCount = poseLandmarksList != null ? poseLandmarksList.Count : 0;

            for (int i = 0; i < poseList.Count; ++i)
            {
                bool b = poseLandmarksList != null && i < poseLandmarksList.Count;

                if (b)
                {
                    poseList[i].AddFrameLandmarks(poseLandmarksList[i].landmarks, false);
                }
                else
                {
                    poseList[i].ClearLandmarks();
                }
            }

            // 若沒有任何 pose，就送出空清單避免後續 NRE
            if (currPoseCount == 0)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<Vector3>(), bMirror);
                return;
            }

            /***************************************
             * 檢查 pose 過近時 要刪除其中一個
             * ************************************/
            int nearestPoseCount = currPoseCount;

            if (currPoseCount > 1)
            {
                FilterTooNearPose(ref poseList, ref currPoseCount);                
            }

            /*************************************************************
             * 依照 numShowPose 決定顯示幾個Pose (以靠近中間為主)
             * **********************************************************/
            int limitCount  = currPoseCount;
            int closestIdx  = KeepClosestToCenter(ref poseList);

            Debug.Log($"Before Near Filter: {nearestPoseCount}, Before Num Filter: {limitCount}, Pose Count: {Mathf.Min(limitCount, numShowPose)}, Closest Index: {closestIdx}");

            /*************************************************************
             * 將pose資料送出
             * **********************************************************/
            List<Vector3> posList = new List<Vector3>();

            for (int i = 0; i < poseList.Count; ++i)
            {
                posList.AddRange(poseList[i].GetPtList());
            }

            SendGlobalMsg<PoseLandListMsg>(posList, bMirror);
        }

        private void ProcessPoseWorldLandmark(List<Landmarks> poseWorldLandmarkList)
        {
            reservePoseWorldLandmarkList = poseWorldLandmarkList;

            //List<Landmark> landmarks = poseWorldLandmarkList[0].landmarks;

            //LandmarkList landmarkList = new LandmarkList();
            //landmarkList.Landmark.AddRange(landmarks);
            
            //new MediapipeLandmarkListMsg(landmarkList, bMirror);

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

        private void FilterTooNearPose(ref List<PoseLankInfo> poseList, ref int currPoseCount)
        {
            float sqrThreshold      = Mathf.Pow(0.05f, 2f);            // 可依實測調整
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
        }

        private int KeepClosestToCenter(ref List<PoseLankInfo> poseList)
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
            if(disSqrList.Count == 0)
            {
                return -1;
            }
            else
            {
                return disSqrList[0].Item1;
            }
        }
    }
}
