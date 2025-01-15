using UnityEngine;
//using Windows.Kinect;

using System;
using System.Collections;
using System.Collections.Generic;
using com.rfilkov.kinect;
using com.rfilkov.components;

using XPlan.Observe;

namespace XPlan.ImageRecognize
{
    /// <summary>
    /// Avatar controller is the component that transfers the captured user motion to a humanoid model (avatar).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AvatarControllerWithMP : AvatarController, INotifyReceiver
    {
        public Func<string> GetLazyZoneID { get; set; }
        public bool bKinectMoving = true;
        private Vector3 hipPos;

        private void Start()
        {
            NotifySystem.Instance.RegisterNotify<HipPositionMsg>(this, (msgReceiver) =>
            {
                HipPositionMsg msg = msgReceiver.GetMessage<HipPositionMsg>();

                hipPos = msg.hipPos;
            });
        }

        // Moves the avatar - gets the tracked position of the user and applies it to avatar.
        protected override void MoveAvatar(ulong UserID)
        {
            if ((moveRate == 0f) || !kinectManager ||
               (kinectManager.GetJointTrackingState(UserID, (int)KinectInterop.JointType.Pelvis) < KinectInterop.TrackingState.Tracked))
            {
                return;
            }

            // get the position of user's spine base
            Vector3 trans = kinectManager.GetUserPosition(UserID);

            if(bKinectMoving)
            {
                DoMoveAvatar(UserID, trans);
            }
            else
            {
                float newX = (mirroredMovement ? 1 - hipPos.x : hipPos.x) * Screen.width;
                float newY = (1 - hipPos.y) * Screen.height;

                Vector3 hipScreenPos    = new Vector3(newX, newY, trans.z);
                Vector3 hipWorldPos     = Camera.main.ScreenToWorldPoint(hipScreenPos);

                transform.position      = hipWorldPos;
            }
        }
    }
}
