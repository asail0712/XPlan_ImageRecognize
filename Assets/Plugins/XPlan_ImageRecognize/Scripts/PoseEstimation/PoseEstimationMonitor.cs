using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;
using UnityEngine;

using XPlan.Observe;

using Rect = UnityEngine.Rect;

namespace XPlan.ImageRecognize
{
    public class PoseMonitorMsg : MessageBase
    {
        public int uniqueID;
        public float faceAng;
        public Rect rect;
        public float maskConverage;

        public PoseMonitorMsg(int uniqueID, float faceAng, Rect rect, float maskConverage)
        {
            this.uniqueID       = uniqueID;
            this.faceAng        = faceAng;
            this.rect           = rect;
            this.maskConverage  = maskConverage;
        }
    }

    public class PoseEstimationMonitor : LogicComponent
    {
        public PoseEstimationMonitor(PoseEstimationRunner poseRunner, bool bDebugMode, bool bMirror)
        {
            poseRunner.resultReceived += (result) =>
            {
                if(!bDebugMode)
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
                List<Mediapipe.Image> poseMaskImgList       = result.segmentationMasks;

                for(int i = 0; i < poseLandmarksList.Count; ++i)
                {
                    float faceAng       = poseLandmarksList[i].GetFaceFrontAngle();
                    Rect rect           = poseLandmarksList[i].GetBoundingBox();
                    float maskConverage = poseMaskImgList[i].GetMaskCoverage();

                    SendGlobalMsg<PoseMonitorMsg>(faceAng, rect, maskConverage);
                }
            };
        }
    }
}
