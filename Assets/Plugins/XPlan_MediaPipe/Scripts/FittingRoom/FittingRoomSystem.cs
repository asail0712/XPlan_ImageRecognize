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

namespace XPlan.MediaPipe
{    
    public class FittingRoomSystem : SystemBase
    {
        [SerializeField] private GameObject bootstrapPrefab;
        [SerializeField] private HolisticTrackingGraph graphRunner;
        [SerializeField] private RunningMode runningMode;
        [SerializeField] private ImageSourceType imgSourceType;

        protected override void OnInitialGameObject()
        {

        }

        protected override void OnInitialLogic()
        {
            ImageSource imgSource = null;

            switch (imgSourceType)
            {
                case ImageSourceType.WebCamera:
                    imgSource = new CamTextureSource();
                    break;
                case ImageSourceType.Kinect:
                    break;
            }

            RegisterLogic(new GraphRunnerInitial(graphRunner, runningMode, bootstrapPrefab));
            RegisterLogic(new TextureInitial(imgSource));
            RegisterLogic(new FittingRoomLogic());
        }
    }
}
