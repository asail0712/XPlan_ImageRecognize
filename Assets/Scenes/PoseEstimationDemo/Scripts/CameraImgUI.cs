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
            screen.rectTransform.sizeDelta          = new Vector2(width, height);
            screen.rectTransform.localEulerAngles   = imageSource.rotation.Reverse().GetEulerAngles();
            ResetUvRect(imageSource);
            screen.texture                          = imageSource.GetCurrentTexture();            
        }

        private void ResetUvRect(ImageSource imageSource)
        {
            var rect = new UnityEngine.Rect(0, 0, 1, 1);

            if (imageSource.isFrontFacing)
            {
                // Flip the image (not the screen) horizontally.
                // It should be taken into account that the image will be rotated later.
                var rotation = imageSource.rotation;

                if (rotation == RotationAngle.Rotation0 || rotation == RotationAngle.Rotation180)
                {
                    rect = FlipHorizontally(rect);
                }
                else
                {
                    rect = FlipVertically(rect);
                }
            }

            screen.uvRect = rect;
        }

        private UnityEngine.Rect FlipHorizontally(UnityEngine.Rect rect)
        {
            return new UnityEngine.Rect(1 - rect.x, rect.y, -rect.width, rect.height);
        }

        private UnityEngine.Rect FlipVertically(UnityEngine.Rect rect)
        {
            return new UnityEngine.Rect(rect.x, 1 - rect.y, rect.width, -rect.height);
        }
    }
}
