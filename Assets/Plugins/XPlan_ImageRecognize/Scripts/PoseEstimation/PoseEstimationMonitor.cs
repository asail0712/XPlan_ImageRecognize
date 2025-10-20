using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;
using UnityEngine;

using XPlan.Observe;
using XPlan.Utility;
using Rect = UnityEngine.Rect;

namespace XPlan.ImageRecognize
{
    public class PoseMonitorMsg : MessageBase
    {
        public int uniqueID;
        public float faceAng;
        public Rect rect;
        public float maskConverage;
        public float roiArea;
        public int imgWidth;
        public int imgHeight;
        public bool bMirror;

        public PoseMonitorMsg(int uniqueID, float faceAng, Rect rect, float maskConverage, float roiArea, int imgWidth, int imgHeight, bool bMirror)
        {
            this.uniqueID       = uniqueID;
            this.faceAng        = faceAng;
            this.rect           = rect;
            this.maskConverage  = maskConverage;
            this.roiArea        = roiArea;
            this.imgWidth       = imgWidth;
            this.imgHeight      = imgHeight;
            this.bMirror        = bMirror;
        }
    }

    public class PoseEstimationMonitor : LogicComponent
    {
        public PoseEstimationMonitor(PoseEstimationRunner poseRunner, bool bDebugMode, bool bMirror)
        {
            int imgWidth            = 0;
            int imgHeight           = 0;
            List<int> selectedIdxs  = new List<int>();

            poseRunner.imgInitFinish += (imgSource) =>
            {
                imgWidth    = imgSource.textureWidth;
                imgHeight   = imgSource.textureHeight;
            };

            poseRunner.resultReceived += (result) =>
            {
                if(!bDebugMode || imgWidth == 0 || imgHeight == 0 || selectedIdxs == null)
                {
                    return;
                }

                int currPoseCount = result.poseLandmarks != null ? result.poseLandmarks.Count : 0;

                // 若沒有任何 pose，就送出空清單 且避免清除pose 2d資料
                if (currPoseCount == 0)
                {
                    return;
                }

                List<NormalizedLandmarks> poseLandmarksList = result.poseLandmarks;
                List<Landmarks> poseWorldLandmarksList      = result.poseWorldLandmarks;
                List<Mediapipe.Image> poseMaskImgList       = result.segmentationMasks;

                for(int i = 0; i < selectedIdxs.Count; ++i)
                {
                    int selectIdx       = selectedIdxs[i];

                    if(!poseLandmarksList.IsValidIndex(selectIdx))
                    {
                        continue;
                    }

                    float faceAng       = poseLandmarksList[selectIdx].GetFaceFrontAngle();
                    Rect rect           = poseLandmarksList[selectIdx].GetBoundingBox();
                    Rect roiRect        = poseLandmarksList[selectIdx].GetBoundingBox();                                    
                    float maskConverage = poseMaskImgList == null?0f:poseMaskImgList[selectIdx].GetMaskCoverage();
                    
                    SendGlobalMsgAsync<PoseMonitorMsg>(selectIdx, faceAng, rect, maskConverage, roiRect.width * roiRect.height, imgWidth, imgHeight, bMirror);
                }
            };

            RegisterNotify<SelectindexesMsg>((msg) => 
            {
                selectedIdxs.Clear();
                selectedIdxs.AddRange(msg.indexList);
            });
        }
    }
}
