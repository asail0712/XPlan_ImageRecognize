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
    public class BodyRecord
    {
        public Vector3 targetPos;
        public float time;
        public Transform bodyTransform;

        public void FlushData(float lastUpdateTime, float MaxUpdateTime, float smoothFactor, bool useUnscaledTime)
        {
            bool isSmoothAllowed = (Time.time - lastUpdateTime) <= MaxUpdateTime;

            bodyTransform.position = isSmoothAllowed && smoothFactor != 0f ?
                    Vector3.Lerp(bodyTransform.position, targetPos, smoothFactor * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime)) : targetPos;
        }
    }

    public class JointRecord
    {
        public Transform boneTransform;
        public float time;
        public KinectInterop.JointType joint;
        public Quaternion jointQuartern;

        public void FlushData(float lastUpdateTime, float MaxUpdateTime, float smoothFactor, bool useUnscaledTime)
        {

            // Smoothly transition to the new rotation
            bool isSmoothAllowed = (Time.time - lastUpdateTime) <= MaxUpdateTime;

            if (isSmoothAllowed && smoothFactor != 0f)
                boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, jointQuartern, smoothFactor * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime));
            else
                boneTransform.rotation = jointQuartern;
        }
    }

    /// <summary>
    /// Avatar controller is the component that transfers the captured user motion to a humanoid model (avatar).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AvatarControllerWithMP : AvatarController, INotifyReceiver
    {
        [SerializeField] public float jointDelayTime = 0.15f;
        [SerializeField] public float moveDelayTime = 0.25f;
        public Func<string> GetLazyZoneID { get; set; }

        public bool bKinectMoving = true;
        private Vector3 hipPos;

        private Queue<JointRecord> jointRecordQueue;
        private Queue<BodyRecord> bodyRecordQueue;

        public override void Awake()
        {
            base.Awake();

            jointRecordQueue    = new Queue<JointRecord>();
            bodyRecordQueue     = new Queue<BodyRecord>();

            NotifySystem.Instance.RegisterNotify<HipPositionMsg>(this, (msgReceiver) =>
            {
                HipPositionMsg msg  = msgReceiver.GetMessage<HipPositionMsg>();
                hipPos              = msg.hipPos;
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
                DoMoveAvatarWithDelay(UserID, trans);
            }
            else
            {
                float newX = (mirroredMovement ? 1 - hipPos.x : hipPos.x) * Screen.width;
                float newY = (1 - hipPos.y) * Screen.height;

                Vector3 hipScreenPos    = new Vector3(newX, newY, trans.z);
                Vector3 hipWorldPos     = Camera.main.ScreenToWorldPoint(hipScreenPos);
                bodyRecordQueue.Enqueue(new BodyRecord() 
                {
                    time            = Time.time,
                    targetPos       = hipWorldPos,
                    bodyTransform   = transform,
                });
            }
        }

        private void DoMoveAvatarWithDelay(ulong UserID, Vector3 trans)
        {
            BodyRecord bodyRecord = new BodyRecord();

            if (flipLeftRight)
                trans.x = -trans.x;

            if (posRelativeToCamera)
            {
                if (posRelOverlayColor)
                {
                    // disable grounded feet
                    if (groundedFeet)
                    {
                        groundedFeet = false;
                    }

                    // use the color overlay position
                    int sensorIndex = kinectManager.GetPrimaryBodySensorIndex();

                    Rect backgroundRect = posRelativeToCamera.pixelRect;
                    PortraitBackground portraitBack = PortraitBackground.Instance;

                    if (portraitBack && portraitBack.enabled)
                    {
                        backgroundRect = portraitBack.GetBackgroundRect();
                    }

                    trans = kinectManager.GetJointPosColorOverlay(UserID, (int)KinectInterop.JointType.Pelvis, sensorIndex, posRelativeToCamera, backgroundRect);
                }
                else
                {
                    // move according to the camera
                    Vector3 bodyRootPos = bodyRoot != null ? bodyRoot.position : transform.position;
                    Vector3 userLocalPos = kinectManager.GetUserKinectPosition(UserID, true);
                    trans = posRelativeToCamera.transform.TransformPoint(userLocalPos);
                    //Debug.Log("  trans: " + trans + ", localPos: " + userLocalPos + ", camPos: " + posRelativeToCamera.transform.position);

                    if (!horizontalMovement)
                    {
                        trans = new Vector3(bodyRootPos.x, trans.y, bodyRootPos.z);
                    }

                    if (verticalMovement)
                    {
                        trans.y -= hipCenterDist;
                    }
                    else
                    {
                        trans.y = bodyRootPos.y;
                    }
                }

                if (flipLeftRight)
                    trans.x = -trans.x;

                if (posRelOverlayColor || !offsetCalibrated)
                {
                    bodyRecord.time             = Time.time;
                    bodyRecord.targetPos        = trans;
                    bodyRecord.bodyTransform    = bodyRoot != null ? bodyRoot : transform;

                    bodyRootPosition = trans;
                    //Debug.Log($"BodyRootPos set: {trans:F2}");

                    // reset the body offset
                    offsetCalibrated = false;
                }
            }

            // invert the z-coordinate, if needed
            if (posRelativeToCamera && posRelInvertedZ)
            {
                trans.z = -trans.z;
            }

            if (!offsetCalibrated)
            {
                offsetPos.x = trans.x;  // !mirroredMovement ? trans.x * moveRate : -trans.x * moveRate;
                offsetPos.y = trans.y;  // trans.y * moveRate;
                offsetPos.z = !mirroredMovement && !posRelativeToCamera ? -trans.z : trans.z;  // -trans.z * moveRate;

                offsetCalibrated = posRelativeToCamera || GetUserHipAngle(UserID) >= 170f;
                //Debug.LogWarning($"{gameObject.name} offset: {offsetPos:F2}, calibrated: {offsetCalibrated}, hipAngle: {GetUserHipAngle(UserID):F1}");
            }

            // transition to the new position
            Vector3 targetPos = bodyRootPosition + Kinect2AvatarPos(trans, verticalMovement, horizontalMovement);
            //Debug.Log("  targetPos: " + targetPos + ", trans: " + trans + ", offsetPos: " + offsetPos + ", bodyRootPos: " + bodyRootPosition);

            if (isRigidBody && !verticalMovement)
            {
                // workaround for obeying the physics (e.g. gravity falling)
                targetPos.y = bodyRoot != null ? bodyRoot.position.y : transform.position.y;
            }

            // fixed bone indices - thanks to Martin Cvengros!
            var biShoulderL = GetBoneIndexByJoint(KinectInterop.JointType.ShoulderLeft, false);  // you may replace 'false' with 'mirroredMovement'
            var biShoulderR = GetBoneIndexByJoint(KinectInterop.JointType.ShoulderRight, false);  // you may replace 'false' with 'mirroredMovement'
            var biPelvis = GetBoneIndexByJoint(KinectInterop.JointType.Pelvis, false);  // you may replace 'false' with 'mirroredMovement'
            var biNeck = GetBoneIndexByJoint(KinectInterop.JointType.Neck, false);  // you may replace 'false' with 'mirroredMovement'

            // added by r618
            if (horizontalMovement && horizontalOffset != 0f &&
                bones[biShoulderL] != null && bones[biShoulderR] != null)
            {
                // { 5, HumanBodyBones.LeftUpperArm},
                // { 11, HumanBodyBones.RightUpperArm},
                //Vector3 dirSpine = bones[5].position - bones[11].position;
                Vector3 dirShoulders = bones[biShoulderR].position - bones[biShoulderL].position;
                targetPos += dirShoulders.normalized * horizontalOffset;
            }

            if (verticalMovement && verticalOffset != 0f &&
                bones[biPelvis] != null && bones[biNeck] != null)
            {
                Vector3 dirSpine = bones[biNeck].position - bones[biPelvis].position;
                targetPos += dirSpine.normalized * verticalOffset;
            }

            if (horizontalMovement && forwardOffset != 0f &&
                bones[biPelvis] != null && bones[biNeck] != null && bones[biShoulderL] != null && bones[biShoulderR] != null)
            {
                Vector3 dirSpine = (bones[biNeck].position - bones[biPelvis].position).normalized;
                Vector3 dirShoulders = (bones[biShoulderR].position - bones[biShoulderL].position).normalized;
                Vector3 dirForward = Vector3.Cross(dirShoulders, dirSpine).normalized;

                targetPos += dirForward * forwardOffset;
            }

            if (groundedFeet && verticalMovement)  // without vertical movement, grounding produces an ever expanding jump up & down
            {
                float fNewDistance = GetCorrDistanceToGround();
                float fNewDistanceTime = useUnscaledTime ? Time.unscaledTime : Time.time;
                //Vector3 lastTargetPos = targetPos;

                if (Mathf.Abs(fNewDistance) >= MaxFootDistanceGround && Mathf.Abs(fFootDistance + fNewDistance) < 1f)  // limit the correction to 1 meter
                {
                    if ((fNewDistanceTime - fFootDistanceTime) >= MaxFootDistanceTime)
                    {
                        fFootDistance += fNewDistance;
                        fFootDistanceTime = fNewDistanceTime;

                        vFootCorrection = initialUpVector * fFootDistance;

                        //Debug.Log($"****{leftFoot.name} pos: {leftFoot.position}, ini: {leftFootPos}, dif: {leftFoot.position - leftFootPos}\n" +
                        //    $"****{rightFoot.name} pos: {rightFoot.position}, ini: {rightFootPos}, dif: {rightFoot.position - rightFootPos}\n" +
                        //    $"****footDist: {fNewDistance:F2}, footCorr: {vFootCorrection}, {transform.name} pos: {transform.position}");
                    }
                }
                else
                {
                    fFootDistanceTime = fNewDistanceTime;
                }

                targetPos += vFootCorrection;
                //Debug.Log($"Gnd targetPos: {targetPos}, lastPos: {lastTargetPos}, vFootCorrection: {vFootCorrection}\nfFootDistance: {fFootDistance:F2}, fNewDistance: {fNewDistance:F2}, upVector: {initialUpVector}, distTime: {(fNewDistanceTime - fFootDistanceTime):F3}");
            }

            bodyRecord.time             = Time.time;
            bodyRecord.targetPos        = targetPos;
            bodyRecord.bodyTransform    = bodyRoot != null ? bodyRoot : transform;

            bodyRecordQueue.Enqueue(bodyRecord);
        }

        protected override void TransformBone(ulong userId, KinectInterop.JointType joint, int boneIndex, bool flip)
        {
            Transform boneTransform = bones[boneIndex];
            if (boneTransform == null || kinectManager == null)
                return;

            int iJoint = (int)joint;
            if ((iJoint < 0) || (kinectManager.GetJointTrackingState(userId, iJoint) < KinectInterop.TrackingState.Tracked))
                return;

            // Get Kinect joint orientation
            Quaternion jointRotation = kinectManager.GetJointOrientation(userId, iJoint, flip);
            if (jointRotation == Quaternion.identity && !IsLegJoint(joint))
                return;

            // calculate the new orientation
            Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

            if (externalRootMotion)
            {
                newRotation = transform.rotation * newRotation;
            }

            jointRecordQueue.Enqueue(new JointRecord()
            {
                boneTransform   = boneTransform,
                time            = Time.time,
                joint           = joint,
                jointQuartern   = newRotation,
            });
        }

        private void MoveJoint()
        {
            JointRecord jointRecord = jointRecordQueue.Peek();

            while((Time.time - jointRecord.time) > jointDelayTime)
            {
                jointRecord = jointRecordQueue.Dequeue();

                jointRecord.FlushData(lastUpdateTime, MaxUpdateTime, smoothFactor, useUnscaledTime);

                if(jointRecordQueue.Count == 0)
                {
                    break;
                }

                jointRecord = jointRecordQueue.Peek();
            }
        }

        private void MoveBody()
        {
            BodyRecord bodyRecord = bodyRecordQueue.Peek();

            while ((Time.time - bodyRecord.time) > moveDelayTime)
            {
                bodyRecord = bodyRecordQueue.Dequeue();

                bodyRecord.FlushData(lastUpdateTime, MaxUpdateTime, smoothFactor, useUnscaledTime);

                if (bodyRecordQueue.Count == 0)
                {
                    break;
                }

                bodyRecord = bodyRecordQueue.Peek();
            }
        }

        public override void UpdateAvatar(ulong UserID)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (kinectManager == null)
            {
                kinectManager = KinectManager.Instance;
            }

            // move the avatar to its Kinect position
            if (!externalRootMotion)
            {
                MoveAvatar(UserID);
            }

            if(jointRecordQueue != null && jointRecordQueue.Count > 0)
            {
                //Debug.Log(jointRecordQueue.Count);

                MoveJoint();
            }

            if(bodyRecordQueue != null && bodyRecordQueue.Count > 0)
            {
                MoveBody();
            }

            // check for sharp pelvis rotations
            float pelvisAngle = GetPelvisAngle(UserID, false);

            if (!poseApplied || pelvisAngle < SHARP_ROT_ANGLE)
            {
                // rotate the avatar bones
                for (var boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (!bones[boneIndex] || isBoneDisabled[boneIndex])  // check for missing or disabled bones
                        continue;

                    bool flip = !(mirroredMovement ^ flipLeftRight);
                    if (boneIndex2JointMap.ContainsKey(boneIndex))
                    {
                        KinectInterop.JointType joint = flip ? boneIndex2JointMap[boneIndex] : boneIndex2MirrorJointMap[boneIndex];

                        if (externalHeadRotation && joint == KinectInterop.JointType.Head)   // skip head if moved externally
                        {
                            continue;
                        }

                        if (externalHandRotations &&    // skip hands if moved externally
                            (joint == KinectInterop.JointType.WristLeft || joint == KinectInterop.JointType.WristRight ||
                                joint == KinectInterop.JointType.HandLeft || joint == KinectInterop.JointType.HandRight))
                        {
                            continue;
                        }

                        TransformBone(UserID, joint, boneIndex, flip);
                    }                    
                }
            }

            // save pelvis rotation
            SavePelvisRotation(UserID);

            // user pose has been applied
            poseApplied = true;

            // update time
            lastUpdateTime = Time.time;
        }
    }
}
