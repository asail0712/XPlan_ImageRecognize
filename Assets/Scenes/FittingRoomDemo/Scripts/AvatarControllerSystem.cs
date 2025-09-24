using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.Holistic;

using XPlan;
using XPlan.ImageRecognize;

namespace XPlan.ImageRecognize.Demo
{    
    public class AvatarControllerSystem : SystemBase
    {
        [SerializeField] private GameObject fittingAvatarGO;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new AvatarController(fittingAvatarGO));
            RegisterLogic(new ImageInitial());
        }
    }
}
