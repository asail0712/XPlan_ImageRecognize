using UnityEngine;

using Mediapipe.Unity;

using XPlan;

namespace XPlan.ImageRecognize.Demo
{
    public class ImageInitial : LogicComponent
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public ImageInitial()
        {
            RegisterNotify<TexturePrepareMsg>((msg) => 
            {
                DirectCallUI<ImageSource>(UICommand.InitScreen, msg.imageSource);
            });         
        }
    }
}
