using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

namespace XPlan.ImageRecognize
{    
    public class PoseRunnerSystem : SystemBase
    {
        [SerializeField] private PoseEstimationRunner poseRunner;
        [Header("Runner 設定")]
        [SerializeField] private RunningMode runningMode;
        [SerializeField] private ModelType modelType                = ModelType.BlazePoseHeavy;
        [SerializeField] private int maxNumOfPose                   = 3;
        [SerializeField] private bool bMirror                       = true;
        [SerializeField] private float minPoseDetectionConfidence   = 0.5f;
        [SerializeField] private float minPosePresenceConfidence    = 0.5f;
        [SerializeField] private float minTrackingConfidence        = 0.5f;
        [SerializeField] private bool bSegmentationMasks            = false;

        [Header("Runner Adapter 設定")]
        [SerializeField] private UserOrderType orderType            = UserOrderType.Center;
        [SerializeField] private int numShowPose                    = 1;
        [SerializeField] private float ptSmoothAlpha                = 0.4f;
        [SerializeField] private float ptSnapDistance               = 0.15f;
        [SerializeField] private bool bMonitorActive                = false;

        protected override void OnPreInitial()
        {
            // 要在Start之前

            if(poseRunner == null)
            {
                return;
            }

            poseRunner.config.MinPoseDetectionConfidence    = minPoseDetectionConfidence;
            poseRunner.config.MinPosePresenceConfidence     = minPosePresenceConfidence;
            poseRunner.config.MinTrackingConfidence         = minTrackingConfidence;
            poseRunner.config.OutputSegmentationMasks       = bSegmentationMasks;

            poseRunner.config.Model     = modelType;
            poseRunner.config.NumPoses  = maxNumOfPose;
            poseRunner.runningMode      = runningMode;
        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new PoseEstimationAdapter(poseRunner, orderType, numShowPose, bSegmentationMasks, bMirror, ptSmoothAlpha, ptSnapDistance));
            RegisterLogic(new PoseEstimationMonitor(poseRunner, bMonitorActive, bMirror));
        }
    }
}
