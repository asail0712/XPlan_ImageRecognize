using System;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using com.rfilkov.kinect;
using com.rfilkov.components;

namespace XPlan.ImageRecognize
{
    /// <summary>
    /// ModelSelector controls the virtual model selection, as well as instantiates and sets up the selected model to overlay the user.
    /// </summary>
    public class ModelSelector : MonoBehaviour
    {
        [SerializeField]
        private GameObject defaultPrefab;

        [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
        public int playerIndex = 0;

        [Tooltip("Makes the initial model position relative to this camera, to be equal to the player's position, relative to the sensor.")]
        public Camera modelRelativeToCamera = null;

        [Tooltip("Camera used to estimate the overlay position of the model over the background.")]
        public Camera foregroundCamera;

        [Tooltip("Whether the scale is updated continuously or just once, after the calibration pose.")]
        public bool continuousScaling = true;

        [Tooltip("Full body scale factor (incl. height, arms and legs) that might be used for fine tuning of body-scale.")]
        [Range(0.0f, 2.0f)]
        public float bodyScaleFactor = 1.0f;

        [Tooltip("Body width scale factor that might be used for fine tuning of the width scale. If set to 0, the body-scale factor will be used for the width, too.")]
        [Range(0.0f, 2.0f)]
        public float bodyWidthFactor = 1.0f;

        [Tooltip("Additional scale factor for arms that might be used for fine tuning of arm-scale.")]
        [Range(0.0f, 2.0f)]
        public float armScaleFactor = 1.0f;

        [Tooltip("Additional scale factor for legs that might be used for fine tuning of leg-scale.")]
        [Range(0.0f, 2.0f)]
        public float legScaleFactor = 1.0f;

        [Tooltip("Horizontal offset of the avatar with respect to the position of user's spine-base.")]
        [Range(-0.5f, 0.5f)]
        public float horizontalOffset = 0f;

        [Tooltip("Vertical offset of the avatar with respect to the position of user's spine-base.")]
        [Range(-0.5f, 0.5f)]
        public float verticalOffset = 0f;

        [Tooltip("Forward (Z) offset of the avatar with respect to the position of user's spine-base.")]
        [Range(-0.5f, 0.5f)]
        public float forwardOffset = 0f;

        [Tooltip("Smoothing factor used for avatar movements and joint rotations.")]
        public float smoothFactor = 0f;

        [Tooltip("Avatar Controller Move Delay time")]
        [SerializeField] private float moveDelayTime = 0.25f;

        [Tooltip("Avatar Controller Joint Delay time")]
        [SerializeField] private float jointDelayTime = 0.15f;

        [Tooltip("Whether to apply the humanoid model's muscle limits to the avatar, or not.")]
        private bool applyMuscleLimits = false;

        [SerializeField] private Text infoText;
        [SerializeField] private float bodyImageScaleRatio;

        static private int SerialNumber     = 0;
        private List<GameObject> modelList  = null;
        private float curScaleFactor        = 0f;
        private float curModelOffset        = 0f;
        private bool bKinectMoving          = true;

        private void Awake()
        {
            // save current scale factors and model offsets
            curScaleFactor  = bodyScaleFactor + bodyWidthFactor + armScaleFactor + legScaleFactor;
            curModelOffset  = horizontalOffset + verticalOffset + forwardOffset + (applyMuscleLimits ? 1f : 0f);
            modelList       = new List<GameObject>();
        }

        void Update()
        {
            KinectManager kinectManager = KinectManager.Instance;

            if (kinectManager == null
                || !kinectManager.IsInitialized()
                || !kinectManager.IsUserDetected(playerIndex))
            {
                return;
            }

            // 有defaultPrefab的情況下 要先生一個default model出來
            if (modelList.Count == 0 && defaultPrefab != null)
            {
                AddModel(defaultPrefab);
            }

            foreach (GameObject selModel in modelList)
            {
                if (selModel != null)
                {
                    // update model settings as needed
                    float curMuscleLimits   = applyMuscleLimits ? 1f : 0f;
                    float updModelOffset    = horizontalOffset + verticalOffset + forwardOffset + curMuscleLimits;

                    if (Mathf.Abs(curModelOffset - updModelOffset) >= 0.001f)
                    {
                        // update model offsets
                        curModelOffset      = updModelOffset;
                        AvatarController ac = selModel.GetComponent<AvatarController>();

                        if (ac != null)
                        {
                            ac.horizontalOffset     = horizontalOffset;
                            ac.verticalOffset       = verticalOffset;
                            ac.forwardOffset        = forwardOffset;
                            ac.applyMuscleLimits    = applyMuscleLimits;
                        }
                    }

                    if (Mathf.Abs(curScaleFactor - (bodyScaleFactor + bodyWidthFactor + armScaleFactor + legScaleFactor)) >= 0.001f)
                    {
                        // update scale factors
                        curScaleFactor          = bodyScaleFactor + bodyWidthFactor + armScaleFactor + legScaleFactor;
                        AvatarScalerPlus scaler = selModel.GetComponent<AvatarScalerPlus>();

                        if (scaler != null)
                        {
                            scaler.continuousScaling    = continuousScaling;
                            scaler.bodyScaleFactor      = bodyScaleFactor;
                            scaler.bodyWidthFactor      = bodyWidthFactor;
                            scaler.armScaleFactor       = armScaleFactor;
                            scaler.legScaleFactor       = legScaleFactor;
                        }
                    }

                    AvatarControllerWithMP acWMP = selModel.GetComponent<AvatarControllerWithMP>();
                    if (acWMP != null)
                    {
                        acWMP.bKinectMoving = bKinectMoving;
                    }
                }
            }

#if DEBUG
            if (Input.GetKeyDown(KeyCode.Q))
            {
                bKinectMoving = !bKinectMoving;
            }

            //// move avatar transform
            if (infoText != null)
            {
                if (bKinectMoving)
                {
                    infoText.text = "Kinect Moving";
                }
                else
                {
                    infoText.text = "Media Pipe Moving";
                }
            }
#endif //DEBUG
        }

        // sets the selected dressing model as user avatar
        private bool LoadClothesModel(GameObject model, out GameObject clothesModel)
        {
            if(model == null)
            {
                clothesModel = null;
                return false;
            }

            clothesModel        = (GameObject)GameObject.Instantiate(model, Vector3.zero, Quaternion.Euler(0, 180f, 0));
            clothesModel.name   = "Model_" + (SerialNumber++).ToString();

            AvatarControllerWithMP ac = clothesModel.GetComponent<AvatarControllerWithMP>();
            if (ac == null)
            {
                ac                      = clothesModel.AddComponent<AvatarControllerWithMP>();
                ac.playerIndex          = playerIndex;

                ac.mirroredMovement     = true;
                ac.verticalMovement     = true;
                ac.horizontalMovement   = true;
                ac.applyMuscleLimits    = applyMuscleLimits;

                ac.horizontalOffset     = horizontalOffset;
                ac.verticalOffset       = verticalOffset;
                ac.forwardOffset        = forwardOffset;
                ac.smoothFactor         = smoothFactor;
                ac.moveDelayTime        = moveDelayTime;
                ac.jointDelayTime       = jointDelayTime;
            }

            ac.posRelativeToCamera      = modelRelativeToCamera;
            ac.posRelOverlayColor       = (foregroundCamera != null);
            ac.Update();

            AvatarScalerPlus scaler         = clothesModel.GetComponent<AvatarScalerPlus>();

            if (scaler == null)
            {
                scaler                      = clothesModel.AddComponent<AvatarScalerPlus>();
                scaler.playerIndex          = playerIndex;
                scaler.mirroredAvatar       = true;
                scaler.minUserDistance      = KinectManager.Instance.minUserDistance;

                scaler.continuousScaling    = continuousScaling;
                scaler.bodyScaleFactor      = bodyImageScaleRatio != 0f ? bodyScaleFactor / bodyImageScaleRatio: bodyScaleFactor;
                scaler.bodyWidthFactor      = bodyImageScaleRatio != 0f ? bodyWidthFactor / bodyImageScaleRatio: bodyWidthFactor;
                scaler.armScaleFactor       = armScaleFactor;
                scaler.legScaleFactor       = legScaleFactor;
            }

            scaler.foregroundCamera = foregroundCamera;

            return true;
        }

        public void ModifyDelayTime(float moveDelayTime, float jointDelayTime)
        {
            this.moveDelayTime  = moveDelayTime;
            this.jointDelayTime = jointDelayTime;
        }

        public GameObject AddModel(GameObject prefab)
        {
            if (LoadClothesModel(prefab, out GameObject clothesPrefab))
            {
                modelList.Add(clothesPrefab);

                return clothesPrefab;
            }

            return null;
        }

        public void RemoveModel(GameObject model)
        {
            if(modelList.Contains(model))
            {
                modelList.Remove(model);
            }

            GameObject.Destroy(model);
        }
    }
}
