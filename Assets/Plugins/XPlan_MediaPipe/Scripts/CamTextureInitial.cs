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
    public class CamTexturePrepareMsg : MessageBase
    {
        public ImageSource imageSource;

        public CamTexturePrepareMsg(ImageSource imageSource)
        {
            this.imageSource = imageSource;
        }
    }

    public class CamTextureInitial : LogicComponent
    {
        public CamTextureInitial()
        {
            StartCoroutine(Run());
        }

        protected IEnumerator Run()
        {           
            // 等待攝像機初始化
            ImageSource imageSource = new WebCamTextureSource();
            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            // 初始化UI
            UISystem.DirectCall<ImageSource>(UICommand.InitScreen, imageSource);

            // 將初始化的結果送出
            SendMsg<CamTexturePrepareMsg>(imageSource);
        }
    }
}
