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
using XPlan.MediaPipe;

namespace XPlan.MediaPipe.Demo
{    
    public class FittingRoomSystem : SystemBase
    {
        [SerializeField] private GameObject bootstrapPrefab;
        [SerializeField] private HolisticTrackingGraph graphRunner;
        [SerializeField] private RunningMode runningMode;

        [SerializeField] private GameObject fittingAvatarGO;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            RegisterLogic(new GraphRunnerInitial(graphRunner, runningMode, bootstrapPrefab));
            RegisterLogic(new CamTextureInitial());
            RegisterLogic(new FittingRoomLogic());
            RegisterLogic(new AvatarController(fittingAvatarGO));
        }
    }
}
