using UnityEngine;
using Mediapipe.Unity;

namespace XPlan.ImageRecognize
{    
    public class PoseEstimationSystem : SystemBase
    {
        [SerializeField] private GameObject bootstrapPrefab;
        [SerializeField] private PoseEstimationGraph graphRunner;
        [SerializeField] private RunningMode runningMode;
        [SerializeField] private ImageSourceType imgSourceType;

        [SerializeField] private float tickTime = 0f;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            // 初始化GraphRunner與Mediapipe相關
            RegisterLogic(new GraphRunnerInitial(graphRunner, runningMode, bootstrapPrefab));
            // 設定影像來源，是Camera或是Kinect(目前就這兩種選項)
            RegisterLogic(new TextureInitial(imgSourceType));
            // 當生成影像與顯示影像都準備好了，就可以開始顯示
            RegisterLogic(new PoseEstimationLogic(tickTime));
        }
    }
}
