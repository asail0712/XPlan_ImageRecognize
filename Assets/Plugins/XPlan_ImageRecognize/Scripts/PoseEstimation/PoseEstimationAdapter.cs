using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using System.Collections.Generic;
using UnityEngine;

using NormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;

namespace XPlan.ImageRecognize
{
    public class PoseEstimationAdapter : LogicComponent
    {
        private bool bMirror;

        public PoseEstimationAdapter(PoseEstimationRunner poseRunner, bool bMirror)
        {
            poseRunner.imgInitFinish    += OnImgInit;
            poseRunner.resultReceived   += OnPoseResult;
            this.bMirror                = bMirror;
        }

        private void OnImgInit(ImageSource imgSource)
        {
            SendGlobalMsg<TexturePrepareMsg>(imgSource);
        }

        private void OnPoseResult(PoseLandmarkerResult result)
        {
            ProcessPoseLandmark(result.poseLandmarks);
            ProcessPoseWorldLandmark(result.poseWorldLandmarks);
        }

        private void ProcessPoseLandmark(List<NormalizedLandmarks> poseLandmarksList)
        {
            if (poseLandmarksList == null || poseLandmarksList.Count == 0)
            {
                SendGlobalMsg<PoseLandListMsg>(new List<Vector3>(), bMirror);
                return;
            }

            List<Vector3> posLost = new List<Vector3>();

            for (int i = 0; i < poseLandmarksList.Count; ++i)
            {
                posLost.Clear();
                List<NormalizedLandmark> landmarkList = poseLandmarksList[i].landmarks;

                for (int j = 0; j < landmarkList.Count; ++j)
                {
                    Vector3 p = new Vector3(landmarkList[j].x, landmarkList[j].y, 0f);
                    posLost.Add(p);
                }

                Debug.Log($"Left Ankle {landmarkList[27]}");

                SendGlobalMsg<PoseLandListMsg>(posLost, bMirror);
            }
        }

        private void ProcessPoseWorldLandmark(List<Landmarks> poseWorldLandmarkList)
        {
        //    //new MediapipeLandmarkListMsg(poseWorldLandmarkList, bMirror);

        //    if (poseWorldLandmarkList == null)
        //    {
        //        SendGlobalMsg<PoseWorldLandListMsg>(new List<Vector3>(), bMirror);
        //        return;
        //    }

        //    List<Vector3> posLost = new List<Vector3>();
        //    IReadOnlyList<Landmark> landmarkList = poseWorldLandmarkList.landmarks;

        //    for (int i = 0; i < poseWorldLandmarkList.Count; ++i)
        //    {
        //        Vector3 p = new Vector3(poseWorldLandmarkList[i].X, poseWorldLandmarkList[i].Y, poseWorldLandmarkList[i].Z);
        //        posLost.Add(p);
        //    }

        //    SendGlobalMsg<PoseWorldLandListMsg>(posLost, bMirror);
        }
    }
}
