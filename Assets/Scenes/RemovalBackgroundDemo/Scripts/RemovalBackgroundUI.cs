using System;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#pragma warning disable IDE0065
using Color = UnityEngine.Color;
#pragma warning restore IDE0065

using Mediapipe;
using Mediapipe.Unity;

using XPlan.ImageRecognize;
using XPlan.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class RemovalBackgroundUI : UIBase
    {
        [SerializeField] private RawImage screen;
        [SerializeField] private Shader maskShader;
        [SerializeField] private Color maskColor                    = new Color(1f, 1f, 1f, 0f);
        [SerializeField, Range(0, 1)] private float maskThredhold   = 0.6f;

        private Material maskMaterial;
        private GraphicsBuffer maskBuffer;
        private float[] maskArray;

        private void Awake()
        {
            /******************************
             * 初始化
             * ****************************/
            maskMaterial = new Material(maskShader)
            {
                renderQueue = (int)RenderQueue.Transparent
            };

            maskMaterial.SetTexture("_MainTex", screen.texture);
            maskMaterial.SetTexture("_MaskTex", CreateMonoColorTexture(maskColor));
            maskMaterial.SetFloat("_Threshold", maskThredhold);

            screen.material = maskMaterial;

            /******************************
             * UI Listener
             * ****************************/
            ListenCall<ImageSource>(UICommand.InitScreen, InitUI);
            ListenCall<float[]>(UICommand.UpdateMask, UpdateMask);
        }

        private void LateUpdate()
        {
            if (maskBuffer != null && maskArray != null)
            {
                maskBuffer.SetData(maskArray);
            }
        }
        private void OnDestroy()
        {
            if (maskBuffer != null)
            {
                maskBuffer.Release();
            }
            maskArray = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maskMaterial != null && !UnityEditor.PrefabUtility.IsPartOfAnyPrefab(this))
            {
                maskMaterial.SetTexture("_MaskTex", CreateMonoColorTexture(maskColor));
                maskMaterial.SetFloat("_Threshold", maskThredhold);
            }
        }
#endif

        public void UpdateMask(float[] maskArray)
        {
            this.maskArray = maskArray;
        }

        private void InitUI(ImageSource imageSource)
        {
            // 執行命令的UI，一定要設定為enable
            gameObject.SetActive(true);

            float width     = imageSource.textureWidth;
            float height    = imageSource.textureHeight;

            // 設定 Screen Size
            screen.texture                  = imageSource.GetCurrentTexture();
            screen.rectTransform.sizeDelta  = new Vector2(width, height);
            AspectRatioFitter ratioFitter   = screen.GetComponent<AspectRatioFitter>();

            if (ratioFitter == null)
            {
                ratioFitter = screen.gameObject.AddComponent<AspectRatioFitter>();
            }

            if (ratioFitter != null)
            {
                AspectRatioFitter.AspectMode mode   = AspectRatioFitter.AspectMode.HeightControlsWidth;
                float aspectRatio                   = width / height;

                ratioFitter.aspectRatio = aspectRatio;
                ratioFitter.aspectMode  = mode;
            }

            // 設定 Material
            maskMaterial.SetInt("_Width", (int)width);
            maskMaterial.SetInt("_Height", (int)height);

            if (maskBuffer != null)
            {
                maskBuffer.Release();
            }

            int stride  = Marshal.SizeOf(typeof(float));
            maskBuffer  = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)width * (int)height, stride);
            
            maskMaterial.SetBuffer("_MaskBuffer", maskBuffer);
        }

        private Texture2D CreateMonoColorTexture(Color color)
        {
            Texture2D texture       = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            Color32 textureColor    = new Color32((byte)(255 * color.r), (byte)(255 * color.g), (byte)(255 * color.b), (byte)(255 * color.a));
            texture.SetPixels32(new Color32[] { textureColor });
            texture.Apply();

            return texture;
        }
    }
}
