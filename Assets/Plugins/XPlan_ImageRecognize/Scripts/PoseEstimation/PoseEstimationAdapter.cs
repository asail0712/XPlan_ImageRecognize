using Intel.RealSense;
using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XPlan.Interface;
using XPlan.Observe;
using XPlan.Utility;

using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;

namespace XPlan.ImageRecognize
{
    public class Clock
    {
        public float Now { get; set; }

        public void Update()
        {
            Now = Time.time;
        }
    }

    public class PoseLandListMsg : MessageBase
    {
        public int index;
        public List<PTInfo> ptList;
        public bool bIsMirror;

        public PoseLandListMsg(int index, List<PTInfo> ptList, bool bIsMirror)
        {
            this.index      = index;
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

        public MediapipeLandmarkListMsg()
        {
            landmarkList    = null;
            bIsMirror       = false;
        }

        public MediapipeLandmarkListMsg(List<Mediapipe.Landmark> landmarkList, bool bIsMirror)
        {
            this.landmarkList   = landmarkList;
            this.bIsMirror      = bIsMirror;
        }
    }

    public class MediapipePoseMaskMsg : MessageBase
    {
        public Mediapipe.Image maskImg;

        public MediapipePoseMaskMsg()
        {
            this.maskImg = null;
        }

        public MediapipePoseMaskMsg(Mediapipe.Image maskImg)
        {
            this.maskImg = maskImg;
        }
    }

    public class PoseEstimationAdapter : LogicComponent, ITickable
    {
        private bool bMirror;
        private List<PoseLankInfo> pose2DList;
        private List<PoseLankInfo> pose3DList;
        private int numShowPose = 1;
        private bool bSegmentationMasks;
        private Clock clock;
        private List<int> selectedIdxs;


        public PoseEstimationAdapter(PoseEstimationRunner poseRunner, int numShowPose, bool bSegmentationMasks, bool bMirror, float ptSmoothAlpha, float ptSnapDistance)
        {            
            this.bMirror            = bMirror;
            this.pose2DList         = new List<PoseLankInfo>();
            this.pose3DList         = new List<PoseLankInfo>();
            this.numShowPose        = numShowPose;
            this.bSegmentationMasks = bSegmentationMasks;
            this.clock              = new Clock();
            this.selectedIdxs       = new List<int>();

            for (int i = 0; i < poseRunner.config.NumPoses; ++i)
            {
                pose2DList.Add(new PoseLankInfo() 
                {
                    UniqueID        = i,
                    SmoothAlpha     = ptSmoothAlpha,
                    snapDistance    = ptSnapDistance,
                    clock           = clock,
                    finishAction    = FlushPose2D,
                });

                pose3DList.Add(new PoseLankInfo()
                {
                    UniqueID        = i,
                    SmoothAlpha     = ptSmoothAlpha,
                    snapDistance    = ptSnapDistance,
                    clock           = clock,
                    finishAction    = FlushPose3D,                    
                });
            }

            poseRunner.imgInitFinish += (imgSource) => 
            {
                SendGlobalMsg<TexturePrepareMsg>(imgSource);
            };

            poseRunner.resultReceived += (result) =>
            {
                int currPoseCount = result.poseLandmarks != null ? result.poseLandmarks.Count : 0;

                // 若沒有任何 pose，就送出空清單 且避免清除pose 2d資料
                if (currPoseCount == 0)
                {
                    ClearPoseLandmark();
                    return;
                }

                // 添加Pose 2D資料
                AddPoseLandmark(currPoseCount, result);

                // 依照Pose 2D條件選取適合的Pose 
                FilterMatchPose(currPoseCount, result, ref selectedIdxs);

                // 依照選中條件顯示對應的 Pose3D
                AddPoseWorldLandmark(currPoseCount, result, selectedIdxs);

                // 依照選中條件顯示對應的 Mask
                ProcessPoseMask(currPoseCount, result, selectedIdxs);
            };
        }

        private void AddPoseWorldLandmark(int currPoseCount, PoseLandmarkerResult result, List<int> selectidxs)
        {
            List<Landmarks> poseLandmarksList = result.poseWorldLandmarks;

            /***************************************
             * 依照 pose數量 修改 pose2DList資料
             * ************************************/
            for (int i = 0; i < pose3DList.Count; ++i)
            {
                bool b = poseLandmarksList != null && i < poseLandmarksList.Count;

                if (!b)
                {
                    // 沒找到目標 因此移除pose資料
                    pose3DList[i].ClearLandmarks(true);

                    continue;
                }

                if (!selectidxs.Contains(i))
                {
                    // 目標不符合篩選 因此移除pose資料
                    pose3DList[i].ClearLandmarks(true);

                    continue;
                }

                // 加入pose資料
                pose3DList[i].AddFrameLandmarks(poseLandmarksList[i].landmarks);
            }
        }

        private void ClearPoseLandmark()
        {
            SendGlobalMsg<MediapipePoseMaskMsg>();

            for (int i = 0; i < pose2DList.Count; ++i)
            {
                // 沒找到目標 因此移除pose資料
                pose2DList[i].ClearLandmarks();
            }
        }

        private void AddPoseLandmark(int currPoseCount, PoseLandmarkerResult result)
        {
            List<NormalizedLandmarks> poseLandmarksList = result.poseLandmarks;
            List<Mediapipe.Image> poseMaskImgList       = result.segmentationMasks;

            /***************************************
             * 依照 pose數量 修改 pose2DList資料
             * ************************************/
            for (int i = 0; i < pose2DList.Count; ++i)
            {
                bool b = poseLandmarksList != null && i < poseLandmarksList.Count;

                if(!b)
                {
                    // 沒找到目標 因此移除pose資料
                    pose2DList[i].ClearLandmarks();

                    continue;
                }

                bool bIsPoseValid = poseLandmarksList[i].IsPoseValid();
                bool bIsMaskValid = !bSegmentationMasks || poseMaskImgList[i].IsMaskValid();

                if (!bIsPoseValid || !bIsMaskValid)
                {
                    // 目標不符合篩選 因此移除pose資料
                    pose2DList[i].ClearLandmarks();

                    continue;
                }

                // 加入pose資料
                pose2DList[i].AddFrameLandmarks(poseLandmarksList[i].landmarks);
            }
        }

        private void FilterMatchPose(int currPoseCount, PoseLandmarkerResult result, ref List<int> closestIdxs)
        {
            /***************************************
             * 檢查 pose 過近時 要刪除其中一個
             * ************************************/
            int totalPoseCount = currPoseCount;

            if (currPoseCount > 1)
            {
                if(FilterTooNearPose(ref pose2DList, ref currPoseCount))
                {
                    //Debug.LogWarning("Pose Too Closest");
                }
            }

            /*************************************************************
             * 依照 numShowPose 決定顯示幾個Pose (以靠近中間為主)
             * **********************************************************/
            int filterNearestPose   = currPoseCount;
            int finalPoseCount      = KeepClosestToCenter(ref pose2DList, ref closestIdxs);

            if (finalPoseCount > 0)
            {
                Debug.Log($"Total Pose Count: {totalPoseCount}, Filter Nearest Pose: {filterNearestPose}, Final Pose Count: {finalPoseCount}, Closest Index: {closestIdxs[0]}");
            }
            else
            {
                Debug.Log($"Total Pose Count: {totalPoseCount}, Filter Nearest Pose: {filterNearestPose}, Final Pose Count: 0");
            }
        }

        private void ProcessPoseMask(int currPoseCount, PoseLandmarkerResult result, List<int> selectidxs)
        {
            if(!bSegmentationMasks)
            {
                return;
            }

            List<Image> poseMasks = result.segmentationMasks;

            /*************************************************************
             * 將選中的 pose Image 資料送出
             * **********************************************************/
            if (selectidxs.Count == 0)
            {
                SendGlobalMsg<MediapipePoseMaskMsg>();
            }
            else if(!result.segmentationMasks.IsValidIndex(selectidxs[0]))
            {
                SendGlobalMsg<MediapipePoseMaskMsg>();
            }
            else
            {
                SendGlobalMsg<MediapipePoseMaskMsg>(result.segmentationMasks[selectidxs[0]]);
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

        public void Tick(float deltaTime)
        {
            clock.Update();

            for (int i = 0; i < pose2DList.Count; ++i)
            {
                pose2DList[i].FlushPose();
            }

            for (int i = 0; i < pose3DList.Count; ++i)
            {
                pose3DList[i].FlushPose();
            }
        }

        private void FlushPose2D(int index, List<PTInfo> ptList)
        {
            if(!selectedIdxs.Contains(index))
            {
                SendGlobalMsg<PoseLandListMsg>(index, new List<PTInfo>(), bMirror);
                return;
            }

            SendGlobalMsg<PoseLandListMsg>(index, ptList, bMirror);
        }

        private void FlushPose3D(int index, List<PTInfo> ptList)
        {
            if(!selectedIdxs.Contains(index))
            {
                SendGlobalMsg<MediapipeLandmarkListMsg>();

                return;
            }

            List<Vector3> vecList = ptList.Select(x => x.pos).ToList();
            SendGlobalMsg<MediapipeLandmarkListMsg>(vecList.ToMpLandmarkList(), bMirror);
         }

        private bool FilterTooNearPose(ref List<PoseLankInfo> poseList, ref int currPoseCount, float thredhoild = 0.05f)
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
                    poseList[delIdxList[k]].ClearLandmarks(true);
                }
            }

            currPoseCount -= delIdxList.Count;

            return delIdxList.Count > 0;
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
