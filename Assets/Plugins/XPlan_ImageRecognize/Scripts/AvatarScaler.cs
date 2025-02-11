using System.Collections.Generic;
using UnityEngine;

using Google.Protobuf.Collections;
using Mediapipe;

namespace XPlan.ImageRecognize
{
    public class AvatarScaler : MonoBehaviour
    {
        [Tooltip("Whether the avatar is facing the player or not.")]
        public bool mirroredAvatar = false;

        [Tooltip("Body scale factor (incl. arms and legs) that may be used for fine tuning of model-scale.")]
        [Range(0.0f, 20.0f)]
        public float bodyScaleFactor = 1f;

        [Tooltip("Body width scale factor that may be used for fine tuning of model-width scale.")]
        [Range(0.0f, 20.0f)]
        public float bodyWidthFactor = 0f;
        
        [Tooltip("Additional scale factor for arms that may be used for fine tuning of model arm-scale.")]
        [Range(0.0f, 2.0f)]
        public float armScaleFactor = 0f;

        [Tooltip("Additional scale factor for legs that may be used for fine tuning of model leg-scale.")]
        [Range(0.0f, 2.0f)]
        public float legScaleFactor = 0f;

        [Tooltip("Scale smoothing factor used in case of continuous scaling.")]
        public float smoothFactor = 5f;

        // mesh renderer
        private SkinnedMeshRenderer meshRenderer = null;

        // model transforms for scaling
        private Transform bodyScaleTransform;

        private Transform leftShoulderScaleTransform;
        private Transform leftElbowScaleTransform;
        private Transform rightShoulderScaleTransform;
        private Transform rightElbowScaleTransform;
        private Transform leftHipScaleTransform;
        private Transform leftKneeScaleTransform;
        private Transform rightHipScaleTransform;
        private Transform rightKneeScaleTransform;

        private Vector3 modelBodyScale = Vector3.one;
        private Vector3 modelLeftShoulderScale = Vector3.one;
        private Vector3 modelLeftElbowScale = Vector3.one;
        private Vector3 modelRightShoulderScale = Vector3.one;
        private Vector3 modelRightElbowScale = Vector3.one;
        private Vector3 modelLeftHipScale = Vector3.one;
        private Vector3 modelLeftKneeScale = Vector3.one;
        private Vector3 modelRightHipScale = Vector3.one;
        private Vector3 modelRightKneeScale = Vector3.one;

        // model bone sizes and original scales
        private float modelBodyHeight = 0f;
        private float modelBodyWidth = 0f;
        private float modelLeftUpperArmLength = 0f;
        private float modelLeftLowerArmLength = 0f;
        private float modelRightUpperArmLength = 0f;
        private float modelRightLowerArmLength = 0f;
        private float modelLeftUpperLegLength = 0f;
        private float modelLeftLowerLegLength = 0f;
        private float modelRightUpperLegLength = 0f;
        private float modelRightLowerLegLength = 0f;

        // user body lengths
        private bool bGotUserBodySize = false;
        private bool bGotUserArmsSize = false;
        private bool bGotUserLegsSize = false;

        // user bone sizes
        private float userBodyHeight = 0f;
        private float userBodyWidth = 0f;
        private float leftUpperArmLength = 0f;
        private float leftLowerArmLength = 0f;
        private float rightUpperArmLength = 0f;
        private float rightLowerArmLength = 0f;
        private float leftUpperLegLength = 0f;
        private float leftLowerLegLength = 0f;
        private float rightUpperLegLength = 0f;
        private float rightLowerLegLength = 0f;

        // user bone scale factors
        private float fScaleBodyHeight = 0f;
        private float fScaleBodyWidth = 0f;
        private float fScaleLeftUpperArm = 0f;
        private float fScaleLeftLowerArm = 0f;
        private float fScaleRightUpperArm = 0f;
        private float fScaleRightLowerArm = 0f;
        private float fScaleLeftUpperLeg = 0f;
        private float fScaleLeftLowerLeg = 0f;
        private float fScaleRightUpperLeg = 0f;
        private float fScaleRightLowerLeg = 0f;

        private List<Vector3> jointPosList;

        // used by category selector
        [System.NonSerialized]
        public bool scalerInited = false;

        public void SetLandmark(RepeatedField<NormalizedLandmark> nlList)
        {
            jointPosList.Clear();

            for (int i = 0; i < nlList.Count; ++i)
            {
                Vector3 pos = new Vector3(nlList[i].X, nlList[i].Y, nlList[i].Z);

                jointPosList.Add(pos);
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            jointPosList = new List<Vector3>();

            // get model transforms
            Animator animatorComponent = GetComponent<Animator>();
            // get mesh renderer
            meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            // use the root transform for body scale
            bodyScaleTransform = transform;

            if (animatorComponent && animatorComponent.GetBoneTransform(HumanBodyBones.Hips))
            {
                //bodyHipsTransform = animatorComponent.GetBoneTransform (HumanBodyBones.Hips);

                leftShoulderScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftElbowScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightShoulderScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightElbowScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.RightLowerArm);

                leftHipScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                leftKneeScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightHipScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                rightKneeScaleTransform = animatorComponent.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            else
            {
                // needed transforms could not be found
                return;
            }

            // get model bone scales
            modelBodyScale = bodyScaleTransform ? bodyScaleTransform.localScale : Vector3.one;

            modelLeftShoulderScale = leftShoulderScaleTransform ? leftShoulderScaleTransform.localScale : Vector3.one;
            modelLeftElbowScale = leftElbowScaleTransform ? leftElbowScaleTransform.localScale : Vector3.one;
            modelRightShoulderScale = rightShoulderScaleTransform ? rightShoulderScaleTransform.localScale : Vector3.one;
            modelRightElbowScale = rightElbowScaleTransform ? rightElbowScaleTransform.localScale : Vector3.one;

            modelLeftHipScale = leftHipScaleTransform ? leftHipScaleTransform.localScale : Vector3.one;
            modelLeftKneeScale = leftKneeScaleTransform ? leftKneeScaleTransform.localScale : Vector3.one;
            modelRightHipScale = rightHipScaleTransform ? rightHipScaleTransform.localScale : Vector3.one;
            modelRightKneeScale = rightKneeScaleTransform ? rightKneeScaleTransform.localScale : Vector3.one;

            if (animatorComponent && animatorComponent.GetBoneTransform(HumanBodyBones.Hips))
            {
                GetModelBodyHeight(animatorComponent, ref modelBodyHeight, ref modelBodyWidth);
                //Debug.Log (string.Format("MW: {0:F3}, MH: {1:F3}", modelBodyWidth, modelBodyHeight));

                GetModelBoneLength(animatorComponent, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, ref modelLeftUpperArmLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, ref modelLeftLowerArmLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, ref modelRightUpperArmLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, ref modelRightLowerArmLength);

                GetModelBoneLength(animatorComponent, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, ref modelLeftUpperLegLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, ref modelLeftLowerLegLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, ref modelRightUpperLegLength);
                GetModelBoneLength(animatorComponent, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, ref modelRightLowerLegLength);

                scalerInited = true;
            }

            // update the scale immediately
            Update();
        }

        // Update is called once per frame
        void Update()
        {
            if(jointPosList.Count == 0)
            {
                return;
            }

            GetUserBodySize(true, true, true);
            ScaleAvatar(smoothFactor, false);
        }


        // gets the the actual sizes of the user bones
        public void GetUserBodySize(bool bBody, bool bArms, bool bLegs)
        {
            if (bBody)
            {
                bGotUserBodySize = GetUserBodyHeight(bodyScaleFactor, bodyWidthFactor, ref userBodyHeight, ref userBodyWidth);
            }

            if (bArms)
            {
                bool gotLeftArmSize = GetUserBoneLength(11, 13, armScaleFactor, ref leftUpperArmLength);
                gotLeftArmSize &= GetUserBoneLength(13, 15, armScaleFactor, ref leftLowerArmLength);
                bool gotRightArmSize = GetUserBoneLength(12, 14, armScaleFactor, ref rightUpperArmLength);
                gotRightArmSize &= GetUserBoneLength(14, 16, armScaleFactor, ref rightLowerArmLength);

                bGotUserArmsSize = gotLeftArmSize | gotRightArmSize;
                if (bGotUserArmsSize)
                {
                    EqualizeBoneLength(ref leftUpperArmLength, ref rightUpperArmLength);
                    EqualizeBoneLength(ref leftLowerArmLength, ref rightLowerArmLength);
                }
            }

            if (bLegs)
            {
                bool gotLeftLegSize = GetUserBoneLength(23, 25, legScaleFactor, ref leftUpperLegLength);
                gotLeftLegSize &= GetUserBoneLength(25, 27, legScaleFactor, ref leftLowerLegLength);
                bool gotRightLegSize = GetUserBoneLength(24, 26, legScaleFactor, ref rightUpperLegLength);
                gotRightLegSize &= GetUserBoneLength(26, 28, legScaleFactor, ref rightLowerLegLength);

                bGotUserLegsSize = gotLeftLegSize | gotRightLegSize;
                if (bGotUserLegsSize)
                {
                    EqualizeBoneLength(ref leftUpperLegLength, ref rightUpperLegLength);
                    EqualizeBoneLength(ref leftLowerLegLength, ref rightLowerLegLength);
                }
            }
        }

        // scales the avatar as needed
        public void ScaleAvatar(float fSmooth, bool bInitialScale)
        {
            // scale body
            if (bodyScaleFactor > 0f && bGotUserBodySize)
            {
                SetupBodyScale(bodyScaleTransform, modelBodyScale, modelBodyHeight, modelBodyWidth, userBodyHeight, userBodyWidth,
                    fSmooth, ref fScaleBodyHeight, ref fScaleBodyWidth);
            }

            // scale arms
            if (/**bInitialScale &&*/ armScaleFactor > 0f && bGotUserArmsSize)
            {
                float fLeftUpperArmLength = !mirroredAvatar ? leftUpperArmLength : rightUpperArmLength;
                SetupBoneScale(leftShoulderScaleTransform, modelLeftShoulderScale, modelLeftUpperArmLength,
                    fLeftUpperArmLength, fScaleBodyHeight, fSmooth, ref fScaleLeftUpperArm);

                float fLeftLowerArmLength = !mirroredAvatar ? leftLowerArmLength : rightLowerArmLength;
                SetupBoneScale(leftElbowScaleTransform, modelLeftElbowScale, modelLeftLowerArmLength,
                    fLeftLowerArmLength, fScaleLeftUpperArm, fSmooth, ref fScaleLeftLowerArm);

                float fRightUpperArmLength = !mirroredAvatar ? rightUpperArmLength : leftUpperArmLength;
                SetupBoneScale(rightShoulderScaleTransform, modelRightShoulderScale, modelRightUpperArmLength,
                    fRightUpperArmLength, fScaleBodyHeight, fSmooth, ref fScaleRightUpperArm);

                float fRightLowerArmLength = !mirroredAvatar ? rightLowerArmLength : leftLowerArmLength;
                SetupBoneScale(rightElbowScaleTransform, modelRightElbowScale, modelLeftLowerArmLength,
                    fRightLowerArmLength, fScaleRightUpperArm, fSmooth, ref fScaleRightLowerArm);
            }

            // scale legs
            if (/**bInitialScale &&*/ legScaleFactor > 0 && bGotUserLegsSize)
            {
                float fLeftUpperLegLength = !mirroredAvatar ? leftUpperLegLength : rightUpperLegLength;
                SetupBoneScale(leftHipScaleTransform, modelLeftHipScale, modelLeftUpperLegLength,
                    fLeftUpperLegLength, fScaleBodyHeight, fSmooth, ref fScaleLeftUpperLeg);

                float fLeftLowerLegLength = !mirroredAvatar ? leftLowerLegLength : rightLowerLegLength;
                SetupBoneScale(leftKneeScaleTransform, modelLeftKneeScale, modelLeftLowerLegLength,
                    fLeftLowerLegLength, fScaleLeftUpperLeg, fSmooth, ref fScaleLeftLowerLeg);

                float fRightUpperLegLength = !mirroredAvatar ? rightUpperLegLength : leftUpperLegLength;
                SetupBoneScale(rightHipScaleTransform, modelRightHipScale, modelRightUpperLegLength,
                    fRightUpperLegLength, fScaleBodyHeight, fSmooth, ref fScaleRightUpperLeg);

                float fRightLowerLegLength = !mirroredAvatar ? rightLowerLegLength : leftLowerLegLength;
                SetupBoneScale(rightKneeScaleTransform, modelRightKneeScale, modelRightLowerLegLength,
                    fRightLowerLegLength, fScaleRightUpperLeg, fSmooth, ref fScaleRightLowerLeg);
            }
        }

        private bool GetUserBodyHeight(float scaleFactor, float widthFactor, ref float height, ref float width)
        {
            height = 0f;
            width = 0f;

            Vector3 posHipLeft = GetJointPosition(23);
            Vector3 posHipRight = GetJointPosition(24);
            Vector3 posShoulderLeft = GetJointPosition(11);
            Vector3 posShoulderRight = GetJointPosition(12);

            if (posHipLeft != Vector3.zero && posHipRight != Vector3.zero &&
               posShoulderLeft != Vector3.zero && posShoulderRight != Vector3.zero)
            {
                Vector3 posHipCenter = (posHipLeft + posHipRight) / 2f;
                Vector3 posShoulderCenter = (posShoulderLeft + posShoulderRight) / 2f;
                //height = (posShoulderCenter.y - posHipCenter.y) * scaleFactor;


                //Debug.Log($"Body Height: {(posShoulderCenter - posHipCenter).magnitude}");
                //Debug.Log($"Shoulder Width: {(posShoulderRight - posShoulderLeft).magnitude}");

                height = (posShoulderCenter - posHipCenter).magnitude * scaleFactor;
                width = (posShoulderRight - posShoulderLeft).magnitude * widthFactor;

                return true;
            }

            return false;
        }

        private bool GetUserBoneLength(int baseJoint, int endJoint, float scaleFactor, ref float length)
        {
            length = 0f;

            Vector3 vPos1 = GetJointPosition(baseJoint);
            Vector3 vPos2 = GetJointPosition(endJoint);

            if (vPos1 != Vector3.zero && vPos2 != Vector3.zero)
            {
                length = (vPos2 - vPos1).magnitude * scaleFactor;
                return true;
            }

            return false;
        }

        private Vector3 GetJointPosition(int joint)
        {
            return jointPosList[joint];
        }

        private bool GetModelBoneLength(Animator animatorComponent, HumanBodyBones baseJoint, HumanBodyBones endJoint, ref float length)
        {
            length = 0f;

            if (animatorComponent)
            {
                Transform joint1 = animatorComponent.GetBoneTransform(baseJoint);
                Transform joint2 = animatorComponent.GetBoneTransform(endJoint);

                if (joint1 && joint2)
                {
                    length = (joint2.position - joint1.position).magnitude;
                    return true;
                }
            }

            return false;
        }

        private bool GetModelBodyHeight(Animator animatorComponent, ref float height, ref float width)
        {
            height = 0f;

            if (animatorComponent)
            {
                //Transform hipCenter = animatorComponent.GetBoneTransform(HumanBodyBones.Hips);

                Transform leftUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperArm);

                Transform leftUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform rightUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperLeg);

                if (leftUpperArm && rightUpperArm && leftUpperLeg && rightUpperLeg)
                {
                    Vector3 posShoulderCenter = (leftUpperArm.position + rightUpperArm.position) / 2f;
                    Vector3 posHipCenter = (leftUpperLeg.position + rightUpperLeg.position) / 2f;  // hipCenter.position

                    //height = (posShoulderCenter.y - posHipCenter.y);
                    height = (posShoulderCenter - posHipCenter).magnitude;
                    width = (rightUpperArm.position - leftUpperArm.position).magnitude;

                    return true;
                }
            }

            return false;
        }


        private void EqualizeBoneLength(ref float boneLen1, ref float boneLen2)
        {
            if (boneLen1 < boneLen2)
            {
                boneLen1 = boneLen2;
            }
            else
            {
                boneLen2 = boneLen1;
            }
        }


        private bool SetupBoneScale(Transform scaleTrans, Vector3 modelBoneScale, float modelBoneLen, float userBoneLen, float parentScale, float fSmooth, ref float boneScale)
        {
            if (modelBoneLen > 0f && userBoneLen > 0f)
            {
                boneScale = userBoneLen / modelBoneLen;
            }

            float localScale = boneScale;
            if (boneScale > 0f && parentScale > 0f)
            {
                localScale = boneScale / parentScale;
            }

            if (scaleTrans && localScale > 0f)
            {
                if (fSmooth != 0f)
                    scaleTrans.localScale = Vector3.Lerp(scaleTrans.localScale, modelBoneScale * localScale, fSmooth * Time.deltaTime);
                else
                    scaleTrans.localScale = modelBoneScale * localScale;

                return true;
            }

            return false;
        }


        private bool SetupBodyScale(Transform scaleTrans, Vector3 modelBodyScale, float modelHeight, float modelWidth, float userHeight, float userWidth,
            float fSmooth, ref float heightScale, ref float widthScale)
        {
            if (modelHeight > 0f && userHeight > 0f)
            {
                heightScale = userHeight / modelHeight;
            }

            if (modelWidth > 0f && userWidth > 0f)
            {
                widthScale = userWidth / modelWidth;
            }
            else
            {
                widthScale = heightScale;
            }

            if (scaleTrans && heightScale > 0f && widthScale > 0f)
            {
                float depthScale = heightScale; // (heightScale + widthScale) / 2f;
                Vector3 newLocalScale = new Vector3(modelBodyScale.x * widthScale, modelBodyScale.y * heightScale, modelBodyScale.z * depthScale);

                if (fSmooth != 0f)
                    scaleTrans.localScale = Vector3.Lerp(scaleTrans.localScale, newLocalScale, fSmooth * Time.deltaTime);
                else
                    scaleTrans.localScale = newLocalScale;

                return true;
            }

            return false;
        }
    }
}
