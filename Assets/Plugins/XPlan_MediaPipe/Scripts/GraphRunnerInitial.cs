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
    public class GraphRunnerPrepareMsg : MessageBase
    {
        public GraphRunner graphRunner;
        public RunningMode runningMode;

        public GraphRunnerPrepareMsg(GraphRunner graphRunner, RunningMode runningMode)
        {
            this.graphRunner = graphRunner;
            this.runningMode = runningMode;
        }
    }

    public class GraphRunnerInitial : LogicComponent
    {
        private static readonly string _BootstrapName = nameof(Bootstrap);

        public GraphRunnerInitial(GraphRunner graphRunner, RunningMode runningMode, GameObject bootstrapPrefab)
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

            // 將初始化的結果送出
            SendMsg<GraphRunnerPrepareMsg>(graphRunner, runningMode);
        }
    }
}
