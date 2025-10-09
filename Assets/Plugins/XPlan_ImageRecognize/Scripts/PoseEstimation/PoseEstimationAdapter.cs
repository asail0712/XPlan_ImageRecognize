using Intel.RealSense;
using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XPlan.Observe;
using XPlan.Utility;

using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;

namespace XPlan.ImageRecognize
{
    public class PoseLandListMsg : MessageBase
    {
        public List<PTInfo> ptList;
        public bool bIsMirror;

        public PoseLandListMsg(List<PTInfo> ptList, bool bIsMirror)
        {
            this.ptList     = ptList;
            this.bIsMirror  = bIsMirror;
        }
    }

    public class PoseWorldLandListMsg : MessageBase
    {
        public List<PTInfo> ptList;
        public bool bIsMirror;

        public PoseWorldLandListMsg(List<PTInfo> ptList, bool bIsMirror)
        {
            this.ptList     = ptList;
            this.bIsMirror  = bIsMirror;
        }
    }

    public class DistanceInfo
    {
        public Vector2 point2D;
        public Vector2 point3D;
        public float dis;
    }

    public class MediapipeLandmarkListMsg : MessageBase
    {
        public List<Mediapipe.Landmark> landmarkList;
        public bool bIsMirror;

        public MediapipeLandmarkListMsg(List<Mediapipe.Landmark> landmarkList, bool bIsMirror)
        {
            this.landmarkList   = landmarkList;
            this.bIsMirror      = bIsMirror;
        }
    }

    public class MediapipePoseMaskMsg : MessageBase
    {
        public Mediapipe.Image maskImg;

        public MediapipePoseMaskMsg(Mediapipe.Image maskImg)
        {
            this.maskImg = maskImg;
        }
    }

    public class PoseEstimationAdapter : LogicComponent
    {
        private bool bMirror;
        private List<PoseLankInfo> pose2DList;
        private List<PoseLankInfo> pose3DList;
        private PoseLankInfo pose3D;
        private int numShowPose = 1;

        public PoseEstimationAdapter(PoseEstimationRunner poseRunner, int numShowPose, bool bMirror, float ptSmoothAlpha, float ptSnapDistance)
        {            
            this.bMirror        = bMirror;
            this.pose2DList     = new List<PoseLankInfo>();
            this.pose3DList     = new List<PoseLankInfo>();
            this.pose3D         = new PoseLankInfo();
            this.numShowPose    = numShowPose;

            for (int i = 0; i < poseRunner.config.NumPoses; ++i)
            {
                pose2DList.Add(new PoseLankInfo() 
                {
                    SmoothAlpha     = ptSmoothAlpha,
                    snapDistance    = ptSnapDistance,
                });

                pose3DList.Add(new PoseLankInfo()
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
            List<Landmarks> poseWorldLandmarkList       = result.poseWorldLandmarks;
            List<Image> maskImg                         = result.segmentationMasks;

            // 依照2D資料確認要選取的pose index 再傳出3D資訊

            /***************************************
             * 依照 pose數量 修改 pose2DList資料
             * ************************************/
            int currPoseCount = poseLandmarksList != null ? poseLandmarksList.Count : 0;

            // 若沒有任何 pose，就送出空清單 且避免清除pose 2d資料
            if (currPoseCount == 0)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<PTInfo>(), bMirror);
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
            List<int> closestIdxs   = new List<int>();
            int filterNearestPose   = currPoseCount;
            int finalPoseCount      = KeepClosestToCenter(ref pose2DList, ref closestIdxs);
            int closestIdx          = -1;

            if (finalPoseCount > 0)
            {
                Debug.Log($"Total Pose Count: {totalPoseCount}, Filter Nearest Pose: {filterNearestPose}, Final Pose Count: {finalPoseCount}, Closest Index: {closestIdxs[0]}");

                closestIdx = closestIdxs[0];
            }
            else
            {
                Debug.Log($"Total Pose Count: {totalPoseCount}, Filter Nearest Pose: {filterNearestPose}, Final Pose Count: 0");
            }

            /*************************************************************
             * 將 pose 2D 資料送出
             * **********************************************************/
            List<PTInfo> ptList = new List<PTInfo>();

            for (int i = 0; i < pose2DList.Count; ++i)
            {
                ptList.AddRange(pose2DList[i].GetPtList());
            }

            SendGlobalMsg<PoseLandListMsg>(ptList, bMirror);

            /*************************************************************
             * 將選中的 pose 3D 資料送出
             * **********************************************************/
            if(poseWorldLandmarkList.IsValidIndex(closestIdx))
            {
                List<Landmark> landmarks    = poseWorldLandmarkList[closestIdx].landmarks;
                pose3D.AddFrameLandmarks(landmarks);
                List<Vector3> vecList       = pose3D.GetVecList();

                SendGlobalMsg<MediapipeLandmarkListMsg>(vecList.ToMpLandmarkList(), bMirror);
            }
            else
            {
                pose3D.ClearLandmarks();
            }

            /*************************************************************
             * 將有效的 pose 3D 資料送出
             * **********************************************************/
            if (finalPoseCount == 0)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<PTInfo>(), bMirror);
                return;
            }

            for (int i = 0; i < pose3DList.Count; ++i)
            {
                // 依照選中的pose去過濾3D資料
                if (closestIdxs.Contains(i))
                {
                    // 加入pose資料
                    pose3DList[i].AddFrameLandmarks(poseWorldLandmarkList[i].landmarks);
                }
                else
                {
                    // 沒找到目標 因此移除pose資料
                    pose3DList[i].ClearLandmarks();
                }
            }

            List<PTInfo> pt3DList = new List<PTInfo>();

            for (int i = 0; i < pose3DList.Count; ++i)
            {
                pt3DList.AddRange(pose3DList[i].GetPtList());
            }

            SendGlobalMsg<PoseWorldLandListMsg>(pt3DList, bMirror);

            
            /*************************************************************
             * 將選中的 pose Image 資料送出
             * **********************************************************/
            if (result.segmentationMasks.IsValidIndex(closestIdx))
            {                
                SendGlobalMsg<MediapipePoseMaskMsg>(result.segmentationMasks[closestIdx]);
            }
            else
            {
                SendGlobalMsg<MediapipePoseMaskMsg>(null);
            }

            // dispose mask data
            if (result.segmentationMasks != null)
            {
                foreach (var mask in result.segmentationMasks)
                {
                    mask.Dispose();
                }
            }
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

        private int KeepClosestToCenter(ref List<PoseLankInfo> poseList, ref List<int> closestPoseIndexs)
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

            closestPoseIndexs.Clear();

            // 依照與螢幕中間距離 排出由小到大的index
            for (int i = 0; i < disSqrList.Count; ++i)
            {
                if(i >= numShowPose)
                {
                    break;
                }

                closestPoseIndexs.Add(disSqrList[i].Item1);
            }

            return closestPoseIndexs.Count;
        }
    }
}
