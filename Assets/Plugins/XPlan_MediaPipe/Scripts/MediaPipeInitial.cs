using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

using XPlan.Observe;
using XPlan.UI;

using TextureFramePool = Mediapipe.Unity.Experimental.TextureFramePool;

namespace XPlan.MediaPipe
{    
    public class PrepareFinishMsg : MessageBase
    {
        public GraphRunner graphRunner;
        public RunningMode runningMode;
        public ImageSource imageSource;

        public PrepareFinishMsg(GraphRunner graphRunner, RunningMode runningMode, ImageSource imageSource)
        {
            this.graphRunner = graphRunner;
            this.runningMode = runningMode;
            this.imageSource = imageSource;
        }
    }

    public class MediaPipeInitial : LogicComponent
    {
        private static readonly string _BootstrapName = nameof(Bootstrap);

        public MediaPipeInitial(GraphRunner graphRunner, RunningMode runningMode, GameObject bootstrapPrefab)
        {
            StartCoroutine(Run(graphRunner, runningMode, bootstrapPrefab));
        }

        protected IEnumerator Run(GraphRunner graphRunner, RunningMode runningMode, GameObject bootstrapPrefab)
        {
            // 載入 Bootstrap 資料
            GameObject bootstrapObj = GameObject.Instantiate(bootstrapPrefab);
            bootstrapObj.name       = _BootstrapName;
            Bootstrap bootstrap     = bootstrapObj.GetComponent<Bootstrap>();
            GameObject.DontDestroyOnLoad(bootstrapObj);
            yield return new WaitUntil(() => bootstrap.isFinished);

            // 等待graphRunner初始化
            WaitForResult graphInitRequest  = graphRunner.WaitForInit(runningMode);
            yield return graphInitRequest;
            if (graphInitRequest.isError)
            {
                Debug.LogError(graphInitRequest.error);
                yield break;
            }

            // 等待攝像機初始化
            ImageSource imageSource         = new WebCamTextureSource();
            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            // 初始化UI
            UISystem.DirectCall<ImageSource>(UICommand.InitScreen, imageSource);

            // 將初始化的結果送出
            SendMsg<PrepareFinishMsg>(graphRunner, runningMode, imageSource);
        }
    }
}
