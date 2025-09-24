using UnityEngine;

using Mediapipe.Unity;

using XPlan;

namespace XPlan.ImageRecognize.Demo
{
    public class MaskInitial : LogicComponent
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public MaskInitial()
        {            
            RegisterNotify<FloatMaskMsg>((msg) =>
            {
                DirectCallUI<float[]>(UICommand.UpdateMask, msg.maskArray);
            });            
        }
    }
}
