using UnityEngine;
using XPlan;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseCheckerSystem : SystemBase
    {
        protected override void OnInitialLogic() 
        {
            RegisterLogic(new PoseChecker());
        }
    }
}
