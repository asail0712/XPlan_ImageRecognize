using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

using XPlan;
using XPlan.UI;

namespace XPlan.MediaPipe.RemovalBackground
{    
    public class RemovalBackgroundSystem : SystemBase
    {
        [SerializeField] private GameObject bootstrapPrefab;
        [SerializeField] private RemovalBackgroundGraph graphRunner;
        [SerializeField] private RunningMode runningMode;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new MediaPipeInitial(graphRunner, runningMode, bootstrapPrefab));
            RegisterLogic(new RemovalBackgroundLogic());
        }
    }
}
