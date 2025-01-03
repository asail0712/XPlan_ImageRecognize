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
using XPlan.MediaPipe;
using XPlan.MediaPipe.RemovalBackground;

namespace XPlan.Demo
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
            RegisterLogic(new GraphRunnerInitial(graphRunner, runningMode, bootstrapPrefab));
            RegisterLogic(new CamTextureInitial());
            RegisterLogic(new RemovalBackgroundLogic());
        }
    }
}
