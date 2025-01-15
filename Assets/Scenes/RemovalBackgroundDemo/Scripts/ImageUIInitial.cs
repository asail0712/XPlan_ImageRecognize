using UnityEngine;

using Mediapipe.Unity;

using XPlan;

namespace XPlan.ImageRecognize.Demo
{
    public static class UICommand
    {
        public const string InitScreen = "InitScreen";
        public const string UpdateMask = "UpdateMask";
    }

    public class ImageUIInitial : LogicComponent
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public ImageUIInitial()
        {
            RegisterNotify<TexturePrepareMsg>((msg) => 
            {
                DirectCallUI<ImageSource>(UICommand.InitScreen, msg.imageSource);
            });

            RegisterNotify<FloatMaskMsg>((msg) =>
            {
                DirectCallUI<float[]>(UICommand.UpdateMask, msg.maskArray);
            });            
        }
    }
}
