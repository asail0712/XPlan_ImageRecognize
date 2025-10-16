using Mediapipe.Unity;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using XPlan.Recycle;
using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseUI : UIBase
    {
        [SerializeField] private GameObject pointPrefab;
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private GameObject pointRoot;
        [SerializeField] private GameObject lineRoot;
        [SerializeField] private RawImage screen;

        private static int MaxPose              = 3;
        private static int PerPoseMaxPointNum   = 33;

        // screen
        private List<UIPoint> pointList;
        private List<UILine> lineList;
        private bool bReceiveData;
        private float showTime;
        private float screenWidth;
        private float screenHeight;
        private bool bMirror;

        // mask
        [SerializeField] private Shader maskShader;
        [SerializeField] private float maskThreshold    = 0.9f;
        [SerializeField] private Color maskColor        = Color.green;

        private GameObject maskObject;
        private RawImage maskScreen;
        private Material _prevMaterial;
        private Material maskMat;
        private GraphicsBuffer maskBuffer;
        private float[] maskArray;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            pointList       = new List<UIPoint>();
            lineList        = new List<UILine>();
            bReceiveData    = false;
            maskArray       = null;
            maskBuffer      = null;
            maskMat         = null;
            maskScreen      = null;

            screenWidth     = 0f;
            screenHeight    = 0f;

            // 生成所有point
            for (int i = 0; i < PerPoseMaxPointNum * MaxPose; ++i)
            {
                GameObject pointGO = GameObject.Instantiate(pointPrefab);
                pointList.Add(pointGO.GetComponent<UIPoint>());
                pointRoot.AddChild(pointGO);
            }

            for (int i = 0; i < MaxPose; ++i)
            {
                // 生成所有line
                for (int j = 0; j < CommonDefine.Connections.Count; ++j)
                {
                    GameObject lineGO   = GameObject.Instantiate(linePrefab);
                    UILine uiLine       = lineGO.GetComponent<UILine>();

                    var pair = CommonDefine.Connections[j];
                    int idx1 = pair.Item1 + i * PerPoseMaxPointNum;
                    int idx2 = pair.Item2 + i * PerPoseMaxPointNum;

                    uiLine.startPT  = pointList[idx1];
                    uiLine.endPT    = pointList[idx2];

                    lineList.Add(uiLine);
                    lineRoot.AddChild(lineGO);
                }
            }

            ListenCall<ImageSource>(UICommand.InitScreen, (imgSource) =>
            {
                InitMask(screen, imgSource.textureWidth, imgSource.textureHeight);
            });

            ListenCall<(int, List<PTInfo>, bool)>(UICommand.UpdatePose, (param) =>
            {
                int index           = param.Item1;
                List<PTInfo> ptList = param.Item2;
                bMirror             = param.Item3;

                if (ptList == null || screenWidth.Equals(0f) || screenHeight.Equals(0f) || index >= MaxPose)
                {
                    return;
                }

                bReceiveData    = true;
                showTime        = 0f;

                //Debug.Log($"Count: {modelCount} Len: {posList.Count}");

                for (int i = 0; i < PerPoseMaxPointNum; ++i)
                {
                    int pointIndex = i + index * PerPoseMaxPointNum;
                    
                    if (i < ptList.Count && ptList[i].IsValid())
                    {
                        pointList[pointIndex].SetPos(ptList[i].pos, screenWidth, screenHeight, bMirror);
                    }
                }
            });

            ListenCall<Mediapipe.Image>(UICommand.UpdatePoseMask, (maskImg) =>
            {
                if (maskImg == null)
                {
                    Array.Clear(maskArray, 0, maskArray.Length);
                    return;
                }

                if (maskArray != null)
                {
                    maskImg.TryReadChannelNormalized(0, maskArray, bMirror);
                }
            });
        }

        private void Update()
        {
            if (bReceiveData)
            {
                showTime += Time.deltaTime;

                if (showTime > 0.3f)
                {
                    bReceiveData = false;
                }
            }

            pointRoot.SetActive(bReceiveData);
            lineRoot.SetActive(bReceiveData);

            if (screenWidth != screen.rectTransform.sizeDelta.x || screenHeight != screen.rectTransform.sizeDelta.y)
            {
                screenWidth     = screen.rectTransform.sizeDelta.x;
                screenHeight    = screen.rectTransform.sizeDelta.y;                
            }

            if (maskScreen != null)
            {
                maskScreen.rectTransform.sizeDelta = new Vector2(screenWidth, screenHeight);
            }

            if (maskMat != null && maskBuffer != null && maskArray != null)
            {
                ApplyMaterial(maskMat);
                maskBuffer.SetData(maskArray);
            }
        }

        public void InitMask(RawImage screen, int width, int height)
        {
            if (maskObject != null)
            {
                Destroy(maskObject);
            }

            // copy the target screen to overlay mask.
            maskObject          = new GameObject("Mask Screen");
            maskObject.transform.SetParent(screen.transform, false);
            maskObject.transform.SetAsFirstSibling();

            maskScreen          = maskObject.AddComponent<RawImage>();
            maskScreen.rectTransform.sizeDelta = screen.rectTransform.sizeDelta;
            maskScreen.color    = new Color(1, 1, 1, 1);

            maskMat = new Material(maskShader)
            {
                renderQueue = (int)RenderQueue.Transparent
            };

            maskMat.SetTexture("_MainTex", maskScreen.texture);
            ApplyMaskTexture(maskColor);
            maskMat.SetInt("_Width", width);
            maskMat.SetInt("_Height", height);
            ApplyThreshold(maskThreshold);

            InitMaskBuffer(width, height);
        }

        private void ApplyMaskTexture(Color maskColor)
        {
            if (maskMat != null)
            {
                maskMat.SetTexture("_MaskTex",  CreateMonoColorTexture(maskColor));
            }
        }

        private void ApplyThreshold(float threshold)
        {
            if (maskMat != null)
            {
                maskMat.SetFloat("_Threshold", threshold);
            }
        }

        private Texture2D CreateMonoColorTexture(Color color)
        {
            var texture         = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var textureColor    = new Color32((byte)(255 * color.r), (byte)(255 * color.g), (byte)(255 * color.b), (byte)(255 * color.a));
            texture.SetPixels32(new Color32[] { textureColor });
            texture.Apply();

            return texture;
        }

        private void InitMaskBuffer(int width, int height)
        {
            if (maskBuffer != null)
            {
                maskBuffer.Release();
            }

            var stride  = Marshal.SizeOf(typeof(float));
            maskBuffer  = new GraphicsBuffer(GraphicsBuffer.Target.Structured, width * height, stride);
            maskMat.SetBuffer("_MaskBuffer", maskBuffer);

            maskArray   = new float[width * height];
        }

        private void ApplyMaterial(Material material)
        {
            if (_prevMaterial == null)
            {
                // backup
                _prevMaterial = maskScreen.material;
            }
            if (maskScreen.material != material)
            {
                maskScreen.material = material;
            }
        }
    }
}
