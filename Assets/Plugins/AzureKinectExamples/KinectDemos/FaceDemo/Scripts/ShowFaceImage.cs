using UnityEngine;
using System.Collections;
using com.rfilkov.kinect;

namespace com.rfilkov.components
{
    public class ShowFaceImage : MonoBehaviour
    {
        [Tooltip("Depth sensor index - 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
        public int playerIndex = 0;

        [Tooltip("Width of the rectangle around the head joint, in meters.")]
        public float faceWidth = 0.3f;
        [Tooltip("Height of the rectangle around the head joint, in meters.")]
        public float faceHeight = 0.3f;

        [Tooltip("Game object renderer, used to display the face image as 2D texture.")]
        public Renderer targetObject;


        //private Renderer targetRenderer;
        private Rect faceRect;
        private Texture2D faceTex;

        private KinectManager kinectManager;
        private BackgroundRemovalManager backManager;

        private RenderTexture colorCamTex = null;
        private bool bSkipUpdatingForegroundTex = false;


        /// <summary>
        /// Gets the face rectangle, in pixels.
        /// </summary>
        /// <returns>The face rectangle.</returns>
        public Rect GetFaceRect()
        {
            return faceRect;
        }

        /// <summary>
        /// Gets the tracked face texture.
        /// </summary>
        /// <returns>The face texture.</returns>
        public Texture GetFaceTex()
        {
            return faceTex;
        }


        void Start()
        {
            // get KM instance
            kinectManager = KinectManager.Instance;
            var colorScale = kinectManager.GetColorImageScale(sensorIndex);

            // init face texture
            faceTex = new Texture2D(100, 100, TextureFormat.ARGB32, false);

            // get background-removal manager instance
            backManager = FindObjectOfType<BackgroundRemovalManager>();

            // setup renderer texture
            if (!targetObject)
            {
                targetObject = GetComponent<Renderer>();
            }

            if (targetObject && targetObject.material)
            {
                targetObject.material.SetTextureScale("_MainTex", new Vector2(colorScale.x, colorScale.y));
            }
        }

        void OnDestroy()
        {
            colorCamTex?.Release();
            colorCamTex = null;
        }

        void Update()
        {
            if (!kinectManager || !kinectManager.IsInitialized())
                return;

            ulong userId = kinectManager.GetUserIdByIndex(playerIndex);
            if (userId == 0)
            {
                if (targetObject && targetObject.material && targetObject.material.mainTexture != null)
                {
                    targetObject.material.mainTexture = null;
                }

                return;
            }

            if (backManager && backManager.IsBackgroundRemovalInited())
            {
                if(!bSkipUpdatingForegroundTex)
                {
                    // use foreground image
                    Texture bmForeTex = backManager.GetForegroundTex();

                    if (bmForeTex is RenderTexture)
                    {
                        colorCamTex = (RenderTexture)bmForeTex;
                        bSkipUpdatingForegroundTex = (colorCamTex != null);
                    }
                    else
                    {
                        if (colorCamTex == null)
                        {
                            colorCamTex = new RenderTexture(bmForeTex.width, bmForeTex.height, 0);
                        }

                        Graphics.Blit(bmForeTex, colorCamTex);
                    }
                }
            }
            else
            {
                // use color camera image
                var colorTex = kinectManager.GetColorImageTex(sensorIndex);

                if (colorCamTex == null)
                {
                    colorCamTex = new RenderTexture(colorTex.width, colorTex.height, 0);
                }

                Graphics.Blit(colorTex, colorCamTex);
            }

            // estimate the rectangle around face
            faceRect = GetFaceRect(userId);

            if (faceRect.width > 0 && faceRect.height > 0)
            {
                int faceX = (int)faceRect.x;
                int faceY = (int)faceRect.y;
                int faceW = (int)faceRect.width;
                int faceH = (int)faceRect.height;

                if (faceX < 0) faceX = 0;
                if (faceY < 0) faceY = 0;

                if (colorCamTex)
                {
                    if ((faceX + faceW) > colorCamTex.width) faceW = colorCamTex.width - faceX;
                    if ((faceY + faceH) > colorCamTex.height) faceH = colorCamTex.height - faceY;
                }

                if (faceTex.width != faceW || faceTex.height != faceH)
                {
                    faceTex.Reinitialize(faceW, faceH);
                }

                if (colorCamTex)
                {
                    KinectInterop.RenderTex2Tex2D(colorCamTex, faceX, colorCamTex.height - faceY - faceH, faceW, faceH, ref faceTex);
                }

                if (targetObject && !targetObject.gameObject.activeSelf)
                {
                    targetObject.gameObject.SetActive(true);
                }

                if (targetObject && targetObject.material && targetObject.material.mainTexture == null)
                {
                    targetObject.material.mainTexture = faceTex;
                }

                // don't rotate the transform - mesh follows the head rotation
                if (targetObject.transform.rotation != Quaternion.identity)
                {
                    targetObject.transform.rotation = Quaternion.identity;
                }
            }
            else
            {
                if (targetObject && targetObject.gameObject.activeSelf)
                {
                    targetObject.gameObject.SetActive(false);
                }

                if (targetObject && targetObject.material && targetObject.material.mainTexture != null)
                {
                    targetObject.material.mainTexture = null;
                }
            }
        }

        // estimates the face rectangle around central face joint (nose or head)
        private Rect GetFaceRect(ulong userId)
        {
            Rect faceJointRect = new Rect();

            int headJoint = (int)KinectInterop.JointType.Head;
            if (kinectManager.IsJointTracked(userId, (int)KinectInterop.JointType.Nose))
            {
                headJoint = (int)KinectInterop.JointType.Nose;
            }

            if (kinectManager.IsJointTracked(userId, headJoint))
            {
                Vector3 posHeadRaw = kinectManager.GetJointKinectPosition(userId, headJoint, false);

                if (posHeadRaw != Vector3.zero)
                {
                    Vector2 posDepthHead = kinectManager.MapSpacePointToDepthCoords(sensorIndex, posHeadRaw);
                    ushort depthHead = kinectManager.GetDepthForPixel(sensorIndex, (int)posDepthHead.x, (int)posDepthHead.y);

                    Vector3 sizeHalfFace = new Vector3(faceWidth / 2f, faceHeight / 2f, 0f);
                    Vector3 posFaceRaw1 = posHeadRaw - sizeHalfFace;
                    Vector3 posFaceRaw2 = posHeadRaw + sizeHalfFace;

                    Vector2 posDepthFace1 = kinectManager.MapSpacePointToDepthCoords(sensorIndex, posFaceRaw1);
                    Vector2 posDepthFace2 = kinectManager.MapSpacePointToDepthCoords(sensorIndex, posFaceRaw2);

                    if (posDepthFace1 != Vector2.zero && posDepthFace2 != Vector2.zero && depthHead > 0)
                    {
                        Vector2 posColorFace1 = kinectManager.MapDepthPointToColorCoords(sensorIndex, posDepthFace1, depthHead);
                        Vector2 posColorFace2 = kinectManager.MapDepthPointToColorCoords(sensorIndex, posDepthFace2, depthHead);

                        if (!float.IsInfinity(posColorFace1.x) && !float.IsInfinity(posColorFace1.y) &&
                           !float.IsInfinity(posColorFace2.x) && !float.IsInfinity(posColorFace2.y))
                        {
                            faceJointRect.x = posColorFace1.x < posColorFace2.x ? posColorFace1.x : posColorFace2.x;  // posColorFace1.x
                            faceJointRect.y = posColorFace1.y < posColorFace2.y ? posColorFace1.y : posColorFace2.y;  // posColorFace2.y;
                            faceJointRect.width = Mathf.Abs(posColorFace2.x - posColorFace1.x);
                            faceJointRect.height = Mathf.Abs(posColorFace2.y - posColorFace1.y);
                        }

                        //Debug.Log($"posHead: {posHeadRaw}, dPosHead: {posDepthHead}, posFace1: {posFaceRaw1}, posFace2: {posFaceRaw2}\n" +
                        //    $"dPosFace1: {posDepthFace1}, dPosFace2: {posDepthFace2}, cPosFace1: {posColorFace1}, cPosFace2: {posColorFace2}\nrectFace: {faceJointRect}");
                    }
                }
            }

            return faceJointRect;
        }

    }
}
