using UnityEngine;

using XPlan;

namespace XPlan.ImageRecognize.Demo
{ 
    public class ImageController : SystemBase
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void OnInitialLogic()
        {
            RegisterLogic(new ImageUIInitial());
        }
    }
}
