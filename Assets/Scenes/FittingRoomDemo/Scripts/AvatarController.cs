using UnityEngine;

using Google.Protobuf.Collections;
using Mediapipe;

using XPlan;
using XPlan.Interface;
using XPlan.ImageRecognize;

namespace XPlan.ImageRecognize.Demo
{
    public class AvatarController : LogicComponent, ITickable
    {
        private GameObject fittingAvatarGO;
        private Vector3 keyPoint;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public AvatarController(GameObject fittingAvatarGO)
        {
            this.fittingAvatarGO        = fittingAvatarGO;
            AvatarScaler avatarScaler   = fittingAvatarGO.GetComponent<AvatarScaler>();
            AvatarFitting avatarFitting = fittingAvatarGO.GetComponent<AvatarFitting>();

            RegisterNotify<PoseWorldLandmarkMsg>((msg) => 
            {
                avatarFitting.Refresh(msg.landmarkList);
            });

            RegisterNotify<PoseLandmarkMsg>((msg) =>
            {
                NormalizedLandmarkList nlmList              = msg.landmarkList;
                RepeatedField<NormalizedLandmark> lmList    = nlmList.Landmark;

                NormalizedLandmark leftHip  = lmList[23];
                NormalizedLandmark rightHip = lmList[24];
                keyPoint                    = new Vector3((leftHip.X + rightHip.X) / 2f, 1 - (leftHip.Y + rightHip.Y) / 2f, (leftHip.Z + rightHip.Z) / 2f);

                avatarScaler.SetLandmark(nlmList.Landmark);
            });
        }

        public void Tick(float deltaTime)
        {
            if (keyPoint == null || fittingAvatarGO == null)
            {
                return;
            }

            // Screen 0,0 在左下角
            Vector3 hipScreenPos    = new Vector3(keyPoint.x * Screen.width, keyPoint.y * Screen.height, 10f);
            Vector3 hipWorldPos     = Camera.main.ScreenToWorldPoint(hipScreenPos);

            fittingAvatarGO.transform.position = hipWorldPos;
        }
    }
}
