using UnityEngine;
using Mediapipe.Unity;

namespace XPlan.ImageRecognize
{    
    public class PoseEstimationSystem2 : SystemBase
    {
        [SerializeField] private PoseEstimationRunner poseRunner;
        [SerializeField] private RunningMode runningMode;
        [SerializeField] private int numOfPose  = 3;
        [SerializeField] private bool bMirror   = true;

        protected override void OnPreInitial()
        {
            // 要在Start之前

            if(poseRunner == null)
            {
                return;
            }

            poseRunner.config.NumPoses  = numOfPose;
            poseRunner.runningMode      = runningMode;
        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new PoseEstimationAdapter(poseRunner, bMirror));
        }
    }
}
