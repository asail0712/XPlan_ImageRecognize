using Mediapipe;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;

namespace XPlan.ImageRecognize
{
    public static class LandmarkConverter
    {
        public static Mediapipe.Landmark ToMpLandmark(Landmark src)
        {
            return new Mediapipe.Landmark()
            {
                X           = src.x,
                Y           = src.y,
                Z           = src.z,
                Visibility  = src.visibility.GetValueOrDefault()
            };
        }

        public static List<Mediapipe.Landmark> ToMpLandmarkList(this IEnumerable<Landmark> srcList)
        {
            var result = new List<Mediapipe.Landmark>();
            foreach (var lmk in srcList)
            {
                result.Add(ToMpLandmark(lmk));
            }
            return result;
        }

        public static Mediapipe.Landmark ToMpLandmark(this Vector3 src)
        {
            return new Mediapipe.Landmark()
            {
                X           = src.x,
                Y           = src.y,
                Z           = src.z,
                Visibility  = default
            };
        }

        public static List<Mediapipe.Landmark> ToMpLandmarkList(this List<Vector3> srcList)
        {
            var result = new List<Mediapipe.Landmark>();
            foreach (var lmk in srcList)
            {
                result.Add(ToMpLandmark(lmk));
            }
            return result;
        }

        private static Texture2D _tex;
        private static byte[] _rowBuf;

        /// <summary>
        /// 將 Mediapipe.Image 轉成 Unity Texture2D (RGBA32/RGB24/R8)，
        /// 若圖在 GPU 會自動 ConvertToCpu() 後再複製。
        /// 回傳可直接指定到 RawImage.texture 的貼圖。
        /// </summary>
        public static Texture2D ToTexture2D(this Image img)
        {
            if (img == null)
            {
                return null;
            }

            // 若在 GPU，先搬到 CPU
            if (img.UsesGpu())
            {
                // ConvertToCpu() 成功才有辦法鎖 Pixels()
                if (!img.ConvertToCpu())
                {
                    Debug.LogWarning("ConvertToCpu() failed.");
                    return null;
                }
            }

            int w       = img.Width();
            int h       = img.Height();
            int step    = img.Step();       // 每列的 byte 數（可能 > w*channels，有 padding）
            int ch      = img.Channels();   // 1 / 3 / 4 常見
            int dstBpp  = ch;               // 每像素 byte 數（簡化：假設 8-bit 通道）

            // 選擇 Unity Texture 格式（簡化推論；若你確定 ImageFormat，可用更精準 mapping）
            TextureFormat tf;
            switch (ch)
            {
                case 4: tf = TextureFormat.RGBA32; break;
                case 3: tf = TextureFormat.RGB24; break;
                case 1: tf = TextureFormat.R8; break;  // 2021+ 支援；老版可改 Alpha8
                default:
                    Debug.LogWarning($"Unsupported channel count: {ch}");
                    return null;
            }

            // 建立/重用貼圖
            if (_tex == null || _tex.width != w || _tex.height != h || _tex.format != tf)
            {
                _tex = new Texture2D(w, h, tf, false, false); // 最後一個參數 false = sRGB（依專案色彩空間調整）
            }

            // 準備一個完整貼圖大小的緩衝
            int dstStride = w * dstBpp;
            int totalSize = dstStride * h;

            if (_rowBuf == null || _rowBuf.Length < totalSize)
            {
                _rowBuf = new byte[totalSize];
            }

            // 透過 PixelWriteLock 取得像素指標
            using (var lockPixels = new PixelWriteLock(img))
            {
                IntPtr srcBase = lockPixels.Pixels();
                if (srcBase == IntPtr.Zero)
                {
                    Debug.LogWarning("PixelWriteLock.Pixels() returned null.");
                    return null;
                }

                // 逐行拷貝（處理 step 與 padding）
                for (int y = 0; y < h; y++)
                {
                    IntPtr srcRow = srcBase + y * step;
                    int dstOffset = y * dstStride;
                    Marshal.Copy(srcRow, _rowBuf, dstOffset, dstStride);
                }
            }

            // 填入 Texture2D
            _tex.LoadRawTextureData(_rowBuf);
            _tex.Apply(false);
            return _tex;
        }

        public static byte[] ConvertImageToRawBytes(this Image img, out int w, out int h, out TextureFormat tf)
        {
            // 若在 GPU，先搬到 CPU
            if (img.UsesGpu())
            {
                // ConvertToCpu() 成功才有辦法鎖 Pixels()
                if (!img.ConvertToCpu())
                {
                    Debug.LogWarning("ConvertToCpu() failed.");
                    w   = 0;
                    h   = 0;
                    tf  = TextureFormat.Alpha8;

                    return null;
                }
            }

            w           = img.Width();
            h           = img.Height();
            int step    = img.Step();
            int ch      = img.Channels();
            int dstBpp  = ch;

            switch (ch)
            {
                case 4: tf = TextureFormat.RGBA32; break;
                case 3: tf = TextureFormat.RGB24; break;
                case 1: tf = TextureFormat.R8; break;
                default: throw new NotSupportedException($"Unsupported channels: {ch}");
            }

            int dstStride = w * dstBpp;
            byte[] buffer = new byte[dstStride * h];

            using (var lockPixels = new PixelWriteLock(img))
            {
                IntPtr srcBase = lockPixels.Pixels();
                if (srcBase == IntPtr.Zero)
                {
                    throw new Exception("PixelWriteLock.Pixels() returned null");
                }

                //for (int y = 0; y < h; y++)
                //{
                //    IntPtr srcRow = srcBase + y * step;
                //    int dstOffset = y * dstStride;

                //    // 正確用法：從 unmanaged -> managed 陣列
                //    Marshal.Copy(srcRow, buffer, dstOffset, dstStride);
                //}
                for (int y = 0; y < h; y++)
                {
                    IntPtr srcRow   = srcBase + y * step;
                    int dstY        = (h - 1 - y);
                    Marshal.Copy(srcRow, buffer, dstY * w, w);   // 每列 w 個 byte
                }
            }

            return buffer;
        }

    }
}
