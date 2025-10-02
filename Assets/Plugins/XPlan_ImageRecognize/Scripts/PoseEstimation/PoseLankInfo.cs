using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;
using UnityEngine;
using XPlan.Utility;


namespace XPlan.ImageRecognize
{
    public class PoseLankInfo
    {
        // 原始（或平滑後）對外輸出的點位
        private List<Vector3> posePtList;

        // 內部平滑狀態
        private List<Vector3> prevSmoothed;
        private bool bHasPrev;

        /// <summary>
        /// 平滑強度 (0~1)。愈大代表愈貼近當前幀（更靈敏、較少延遲）
        /// 建議 0.4~0.8 之間，預設 0.6
        /// </summary>
        private float SmoothAlpha { get; set; } = 0.4f;

        /// <summary>
        /// 瞬切門檻（以「歸一化座標」長度衡量，0~sqrt(2)）
        /// 典型人體點位在 0~1 空間中移動，0.12~0.2 通常表現不錯。
        /// </summary>
        private const float snapDistance    = 0.15f;
        private const float snapDistSqr     = snapDistance * snapDistance;

        public PoseLankInfo() 
        {
            posePtList      = new List<Vector3>();
            prevSmoothed    = new List<Vector3>();
            bHasPrev        = false;

            SmoothAlpha     = Mathf.Clamp01(SmoothAlpha);
        }

        public void AddFrameLandmarks(List<NormalizedLandmark> landmarkList, bool bVisualZ)
        {
            // 先將這一幀的 raw 轉成 Vector3
            int n = landmarkList?.Count ?? 0;
            if (n == 0)
            {
                posePtList.Clear();
                // 不清 prev，因為可能只是短暫偵測不到
                return;
            }

            // 確保容器長度
            EnsureSize(posePtList, n);
            EnsureSize(prevSmoothed, n);

            // 第一次：直接吃 raw 當作起點
            if (!bHasPrev)
            {
                for (int i = 0; i < n; ++i)
                {
                    var lmk         = landmarkList[i];
                    var raw         = new Vector3(lmk.x, lmk.y, bVisualZ ? lmk.z : 0f);
                    prevSmoothed[i] = raw;
                    posePtList[i]   = raw;
                }
                bHasPrev = true;
                return;
            }

            for (int i = 0; i < n; ++i)
            {
                var lmk     = landmarkList[i];
                var raw     = new Vector3(lmk.x, lmk.y, bVisualZ ? lmk.z : 0f);
                var prev    = prevSmoothed[i];

                // 判斷距離（2D 或 3D）
                float distSqr;
                if (bVisualZ)
                {
                    distSqr = (raw - prev).sqrMagnitude;
                }
                else
                {
                    Vector2 raw2    = new Vector2(raw.x, raw.y);
                    Vector2 prev2   = new Vector2(prev.x, prev.y);
                    distSqr         = (raw2 - prev2).sqrMagnitude;
                }

                Vector3 smoothed;
                if (distSqr > snapDistSqr)
                {
                    // 大位移：瞬切過去，視為換目標
                    smoothed = raw;
                }
                else
                {
                    // 正常微動：指數平滑
                    smoothed = Vector3.Lerp(prev, raw, SmoothAlpha);
                }

                prevSmoothed[i] = smoothed;
                posePtList[i]   = smoothed;
            }
        }

        public void ClearLandmarks()
        {
            posePtList.Clear();
        }

        public List<Vector3> GetPtList()
        {
            return posePtList;
        }

        public Vector3 GetHipCenter()
        {
            if (posePtList == null || posePtList.Count == 0)
            {
                return Vector3.zero;
            }

            int leftHipIdx  = (int)BodyPoseType.LeftHip;
            int rightHipIdx = (int)BodyPoseType.RightHip;

            if (!posePtList.IsValidIndex(leftHipIdx) 
            || !posePtList.IsValidIndex(rightHipIdx))
            {
                return Vector3.zero;
            }

            return (posePtList[leftHipIdx] + posePtList[rightHipIdx]) / 2f;
        }


        public float DisSqrToScreenCenter(bool bVisualZ)
        {
            if (posePtList == null || posePtList.Count == 0)
            {
                return float.MaxValue;
            }

            // 計算中心點
            Vector3 center = Vector3.zero;
            foreach (Vector3 pt in posePtList)
            {
                center += new Vector3(pt.x, pt.y, bVisualZ?pt.z:0f);
            }
            center /= posePtList.Count;

            // 因為螢幕中心是(0.5,0.5,0)，因此向量長度就是距離
            if(bVisualZ)
            {
                return (center - new Vector3(0.5f, 0.5f, 0f)).sqrMagnitude;
            }
            else
            {
                return (new Vector2(center.x, center.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
            }
        }

        private static void EnsureSize(List<Vector3> list, int size)
        {
            if (list.Count < size)
            {
                int add = size - list.Count;
                for (int i = 0; i < add; ++i) 
                {
                    list.Add(Vector3.zero);
                }
            }
            else if (list.Count > size)
            {
                list.RemoveRange(size, list.Count - size);
            }
        }

    }
}
