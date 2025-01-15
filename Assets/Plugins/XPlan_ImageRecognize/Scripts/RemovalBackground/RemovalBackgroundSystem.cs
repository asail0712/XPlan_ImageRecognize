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

namespace XPlan.ImageRecognize
{    
    public class RemovalBackgroundSystem : SystemBase
    {
        [SerializeField] private GameObject bootstrapPrefab;
        [SerializeField] private RemovalBackgroundGraph graphRunner;
        [SerializeField] private RunningMode runningMode;
        [SerializeField] private ImageSourceType imgSourceType;
        [SerializeField] private MaskType maskType;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new GraphRunnerInitial(graphRunner, runningMode, bootstrapPrefab));
            RegisterLogic(new TextureInitial(imgSourceType));
            RegisterLogic(new RemovalBackgroundLogic(maskType));
        }
    }
}
