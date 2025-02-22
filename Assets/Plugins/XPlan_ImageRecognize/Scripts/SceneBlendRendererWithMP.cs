using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;
using com.rfilkov.components;

using XPlan.Observe;
using XPlan.Recycle;
using System;

namespace XPlan.ImageRecognize
{
    public class TextureData : IPoolable
    {
        public float imageTime;
        public Texture image;

        public void InitialPoolable() 
        {
            KinectInterop.SensorData sensorData = KinectManager.Instance.GetSensorData(0);
            image = new Texture2D(sensorData.colorImageTexture.width, sensorData.colorImageTexture.height, TextureFormat.BGRA32, false);
        }
        public void ReleasePoolable() 
        {
            GameObject.Destroy(image);

            image = null;
        }

        public void OnSpawn() { }
        public void OnRecycle() { }

    }

    /// <summary>
    /// SceneBlendRenderer provides volumetric rendering and lighting of the real environment, as seen by the sensor's color camera.
    /// </summary>
    public class SceneBlendRendererWithMP : MonoBehaviour, INotifyReceiver
    {
        [Tooltip("Added depth distance between the real environment and the virtual environment, in meters.")]
        [Range(-0.5f, 0.5f)]
        public float depthDistance = 0.1f;

        [Tooltip("Index of the depth sensor that generates the color camera background. 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("Depth value in meters, used for invalid depth points.")]
        public float invalidDepthValue = 5f;

        [Tooltip("Whether to maximize the rendered object on the screen, or not.")]
        private bool maximizeOnScreen = true;

        [Tooltip("Whether to apply per-pixel lighting on the foreground, or not.")]
        public bool applyLighting = false;

        [Tooltip("Camera used to scale the mesh, to fill the camera's background. If left empty, it will default to the main camera in the scene.")]
        public Camera foregroundCamera;

        [Tooltip("Background image (if any) that needs to be overlayed by this blend renderer.")]
        public UnityEngine.UI.RawImage backgroundImage;

        [Tooltip("Delay Time to show image.")]
        [Range(0f, 5f)]
        public float delayTime = 0.2f;

        [Tooltip("Texture Pool Size")]
        [Range(5f, 30f)]
        public int poolSize = 5;

        // references to KM and data
        private KinectManager kinectManager = null;
        private KinectInterop.SensorData sensorData = null;
        private Material matRenderer = null;

        // depth image buffer (in depth camera resolution)
        private ComputeBuffer depthImageBuffer = null;

        // textures
        //private Texture alphaTex = null;
        private Texture colorTex = null;
        private Queue<TextureData> colorTexQueue = null;

        // lighting
        private FragmentLighting lighting = new FragmentLighting();

        // saved screen width & height
        private int lastScreenW = 0;
        private int lastScreenH = 0;
        private int lastColorW = 0;
        private int lastColorH = 0;
        private float lastAnchorPos = 0f;
        private Vector3 initialScale = Vector3.one;

        // distances
        private float distToBackImage = 0f;
        private float distToTransform = 0f;

        public Func<string> GetLazyZoneID { get; set; }

        private Texture2D alphaTex2D = null;
        private Color32[] colorArray = null;

        private int width;
        private int height;
        private bool bRefreshColor;

        private void Awake()
        {
            bRefreshColor   = false;

            NotifySystem.Instance.RegisterNotify<ColorMaskMsg>(this, (msgReceiver) =>
            {
                bRefreshColor       = true;

                ColorMaskMsg msg    = msgReceiver.GetMessage<ColorMaskMsg>();
                colorArray          = msg.maskColorArray;
                width               = msg.width;
                height              = msg.height;
            });
        }

        void Start()
        {
            kinectManager = KinectManager.Instance;
            initialScale = transform.localScale;

            // get distance to back image
            if (backgroundImage)
            {
                Canvas canvas = backgroundImage.canvas;

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    distToBackImage = canvas.planeDistance;
                else
                    distToBackImage = 0f;
            }

            // get distance to transform
            distToTransform = transform.localPosition.z;

            // set renderer material
            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer)
            {
                Shader blendShader = Shader.Find("KinectWithMediaPipe/ForegroundBlendShader");
                if (blendShader != null)
                {
                    matRenderer = new Material(blendShader);
                    meshRenderer.material = matRenderer;
                }
            }

            // get sensor data
            if (kinectManager && kinectManager.IsInitialized())
            {
                sensorData = kinectManager.GetSensorData(sensorIndex);
            }

            if (foregroundCamera == null)
            {
                foregroundCamera = Camera.main;
            }

            // find scene lights
            Light[] sceneLights = GameObject.FindObjectsOfType<Light>();
            lighting.SetLightsAndBounds(sceneLights, transform.position, new Vector3(20f, 20f, 20f));

            colorTexQueue = new Queue<TextureData>();            
        }


        void OnDestroy()
        {
            if (sensorData != null && sensorData.colorDepthBuffer != null)
            {
                sensorData.colorDepthBuffer.Release();
                sensorData.colorDepthBuffer = null;
            }

            if (depthImageBuffer != null)
            {
                //depthImageCopy = null;

                depthImageBuffer.Release();
                depthImageBuffer = null;
            }

            // release lighting resources
            lighting.ReleaseResources();

            while(colorTexQueue.Count > 0)
            {
                GameObject.DestroyImmediate(colorTexQueue.Dequeue().image);
            }

            RecyclePool<TextureData>.UnregisterType();
        }

        void Update()
        {
            if (sensorData != null && sensorData.colorImageTexture != null)
            {
                if (colorTex == null)
                {
                    colorTex = new Texture2D(sensorData.colorImageTexture.width, sensorData.colorImageTexture.height, TextureFormat.BGRA32, false);

                    matRenderer.SetInt("_TexResX", colorTex.width);
                    matRenderer.SetInt("_TexResY", colorTex.height);

                    matRenderer.SetTexture("_ColorTex", colorTex);

                    List<TextureData> textureList = new List<TextureData>();

                    // 放幾個備用
                    for (int i = 0; i < poolSize; ++i)
                    {
                        TextureData currData    = new TextureData();
                        currData.image          = new Texture2D(sensorData.colorImageTexture.width, sensorData.colorImageTexture.height, TextureFormat.BGRA32, false);

                        textureList.Add(currData);
                    }

                    RecyclePool<TextureData>.RegisterType(textureList);
                }

                TextureData currTexture = RecyclePool<TextureData>.SpawnOne();
                currTexture.imageTime   = Time.time;
                
                Graphics.CopyTexture(sensorData.colorImageTexture, currTexture.image);

                colorTexQueue.Enqueue(currTexture);
            }

            if (colorTexQueue.Count > 0)
            {
                List<TextureData> recycleList   = new List<TextureData>();

                TextureData firstData           = colorTexQueue.Peek();
                bool bDeQ                       = false;

                while (Time.time - firstData.imageTime > delayTime)
                {
                    bDeQ        = true;
                    firstData   = colorTexQueue.Dequeue();

                    recycleList.Add(firstData);
                }

                if (bDeQ && firstData.image != null)
                {
                    Graphics.CopyTexture(firstData.image, colorTex);
                }

                RecyclePool<TextureData>.RecycleList(recycleList);

                //Debug.Log($" Color Texture Num : {colorTexQueue.Count}");
                //Debug.Log($" Pool Num : {RecyclePool<TextureData>.GetPoolNum()}");
                //Debug.Log($" Total Num : {RecyclePool<TextureData>.GetTotalNum()}");
            }

            // 設定 alphaTex2D
            if (colorArray != null && bRefreshColor)
            {
                bRefreshColor = false;

                if (alphaTex2D == null)
                {
                    alphaTex2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    matRenderer.SetTexture("_AlphaTex", alphaTex2D);
                }

                alphaTex2D.SetPixels32(0, 0, width, height, colorArray);
                alphaTex2D.Apply();
            }


            if (colorTex == null)
                return;

            int bufferLength = sensorData.colorImageWidth * sensorData.colorImageHeight / 2;
            if (sensorData.colorDepthBuffer == null || sensorData.colorDepthBuffer.count != bufferLength)
            {
                sensorData.colorDepthBuffer = new ComputeBuffer(bufferLength, sizeof(uint));
                matRenderer.SetBuffer("_DepthMap", sensorData.colorDepthBuffer);
                //Debug.Log("Created colorDepthBuffer with len: " + bufferLength);
            }

            matRenderer.SetFloat("_DepthDistance", depthDistance);
            matRenderer.SetFloat("_InvDepthVal", invalidDepthValue);

            int curScreenW = foregroundCamera ? foregroundCamera.pixelWidth : Screen.width;
            int curScreenH = foregroundCamera ? foregroundCamera.pixelHeight : Screen.height;
            if (lastScreenW != curScreenW || lastScreenH != curScreenH || lastColorW != sensorData.colorImageWidth || lastColorH != sensorData.colorImageHeight)
            {
                ScaleRendererTransform(curScreenW, curScreenH);
            }

            Vector2 anchorPos = backgroundImage ? backgroundImage.rectTransform.anchoredPosition : Vector2.zero;
            float curAnchorPos = anchorPos.x + anchorPos.y;  // Mathf.Abs(anchorPos.x) + Mathf.Abs(anchorPos.y);
            if (Mathf.Abs(curAnchorPos - lastAnchorPos) >= 20f)
            {
                //Debug.Log("anchorPos: " + anchorPos + ", curAnchorPos: " + curAnchorPos + ", lastAnchorPos: " + lastAnchorPos + ", diff: " + Mathf.Abs(curAnchorPos - lastAnchorPos));
                CenterRendererTransform(anchorPos, curAnchorPos);
            }

            // update lighting parameters
            lighting.UpdateLighting(matRenderer, applyLighting);
        }

        // scales the renderer's transform properly
        private void ScaleRendererTransform(int curScreenW, int curScreenH)
        {
            lastScreenW = curScreenW;
            lastScreenH = curScreenH;
            lastColorW = sensorData.colorImageWidth;
            lastColorH = sensorData.colorImageHeight;

            Vector3 localScale = transform.localScale;

            if (maximizeOnScreen && foregroundCamera)
            {
                float objectZ = distToTransform;  // transform.localPosition.z;  // the transform should be a child of the camera
                float screenW = foregroundCamera.pixelWidth;
                float screenH = foregroundCamera.pixelHeight;

                if (backgroundImage)
                {
                    PortraitBackground portraitBack = backgroundImage.gameObject.GetComponent<PortraitBackground>();

                    if (portraitBack != null)
                    {
                        Rect backRect = portraitBack.GetBackgroundRect();
                        screenW = backRect.width;
                        screenH = backRect.height;
                    }
                }

                Vector3 vLeft = foregroundCamera.ScreenToWorldPoint(new Vector3(0f, screenH / 2f, objectZ));
                Vector3 vRight = foregroundCamera.ScreenToWorldPoint(new Vector3(screenW, screenH / 2f, objectZ));
                float distLeftRight = (vRight - vLeft).magnitude;

                Vector3 vBottom = foregroundCamera.ScreenToWorldPoint(new Vector3(screenW / 2f, 0f, objectZ));
                Vector3 vTop = foregroundCamera.ScreenToWorldPoint(new Vector3(screenW / 2f, screenH, objectZ));
                float distBottomTop = (vTop - vBottom).magnitude;

                localScale.x = distLeftRight / initialScale.x;
                localScale.y = distBottomTop / initialScale.y;
                //Debug.Log("SceneRenderer scale: " + localScale + ", screenW: " + screenW + ", screenH: " + screenH + ", objZ: " + objectZ +
                //    "\nleft: " + vLeft + ", right: " + vRight + ", bottom: " + vBottom + ", vTop: " + vTop +
                //    "\ndH: " + distLeftRight + ", dV: " + distBottomTop + ", initialScale: " + initialScale);
            }

            // scale according to color-tex resolution
            //localScale.y = localScale.x * colorTex.height / colorTex.width;

            // apply color image scale
            Vector3 colorImageScale = kinectManager.GetColorImageScale(sensorIndex);
            if (colorImageScale.x < 0f)
                localScale.x = -localScale.x;
            if (colorImageScale.y < 0f)
                localScale.y = -localScale.y;

            transform.localScale = localScale;
        }

        // centers the renderer's transform, according to the background image
        private void CenterRendererTransform(Vector2 anchorPos, float curAnchorPos)
        {
            lastAnchorPos = curAnchorPos;

            if (foregroundCamera && distToBackImage > 0f)
            {
                float objectZ = distToTransform;  // transform.localPosition.z;  // the transform should be a child of the camera
                float screenW = sensorData.colorImageWidth;  // foregroundCamera.pixelWidth;
                float screenH = sensorData.colorImageHeight;  // foregroundCamera.pixelHeight;

                Vector2 screenCenter = new Vector2(screenW / 2f, screenH / 2f);
                Vector2 anchorScaled = new Vector2(anchorPos.x * distToTransform / distToBackImage, anchorPos.y * distToTransform / distToBackImage);
                Vector3 vCenter = foregroundCamera.ScreenToWorldPoint(new Vector3(screenCenter.x + anchorScaled.x, screenCenter.y + anchorScaled.y, objectZ));
                transform.position = vCenter;

                //Vector3 vLocalPos = transform.localPosition;
                //string sLocalPos = string.Format("({0:F3}, {1:F3}, {2:F3})", vLocalPos.x, vLocalPos.y, vLocalPos.z);
                //Debug.Log("SceneRenderer anchor: " + anchorPos + ", screenW: " + screenW + ", screenH: " + screenH + ", objZ: " + objectZ + ", localPos: " + sLocalPos);
            }
        }

    }
}
