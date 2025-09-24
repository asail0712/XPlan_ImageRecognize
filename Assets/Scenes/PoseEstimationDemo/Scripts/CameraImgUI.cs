using System;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#pragma warning disable IDE0065
using Color = UnityEngine.Color;
#pragma warning restore IDE0065

using Mediapipe;
using Mediapipe.Unity;

using XPlan.ImageRecognize;
using XPlan.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class CameraImgUI : UIBase
    {
        [SerializeField] private RawImage screen;

        private void Awake()
        {          
            /******************************
             * UI Listener
             * ****************************/
            ListenCall<ImageSource>(UICommand.InitScreen, InitUI);
        }

        private void InitUI(ImageSource imageSource)
        {
            // 執行命令的UI，一定要設定為enable
            gameObject.SetActive(true);

            float width     = imageSource.textureWidth;
            float height    = imageSource.textureHeight;

            // 設定 Screen Size
            screen.texture                  = imageSource.GetCurrentTexture();
            screen.rectTransform.sizeDelta  = new Vector2(width, height);
            AspectRatioFitter ratioFitter   = screen.GetComponent<AspectRatioFitter>();

            if (ratioFitter == null)
            {
                ratioFitter = screen.gameObject.AddComponent<AspectRatioFitter>();
            }

            if (ratioFitter != null)
            {
                AspectRatioFitter.AspectMode mode   = AspectRatioFitter.AspectMode.HeightControlsWidth;
                float aspectRatio                   = width / height;

                ratioFitter.aspectRatio = aspectRatio;
                ratioFitter.aspectMode  = mode;
            }
        }
    }
}
