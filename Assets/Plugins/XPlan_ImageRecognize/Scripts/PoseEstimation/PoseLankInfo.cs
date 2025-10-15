using Mediapipe.Tasks.Components.Containers;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor.Tilemaps;
using UnityEngine;
using XPlan.Utility;


namespace XPlan.ImageRecognize
{
    public class PTInfo
    {
        public Vector3 pos;
        public float visibility;
        public float presence;

        private readonly float alphaRise        = 0.6f;     // e.g. 0.5f（上升時追得快）
        private readonly float alphaFall        = 0.1f;     // e.g. 0.2f（下降時追得慢）
        private readonly float decayWhenMissing = 0.05f;    // e.g. 0.08f 每幀往 0 衰退量（raw==null）

        public float x { get => pos.x; }
        public float y { get => pos.y; }
        public float z { get => pos.z; }

        public PTInfo()
        {
            pos         = Vector3.zero;
            visibility  = 0f;
            presence    = 0f;
        }

        public PTInfo(Vector3 pos, float? visibility, float? presence)
        {
            this.pos        = pos;
            this.visibility = visibility ?? 0f;
            this.presence   = presence ?? 0f;
        }

        public void SetData(Vector3 pos, float? visibility, float? presence)
        {
            this.pos        = pos;
            this.visibility = SmoothValue(visibility, this.visibility);
            this.presence   = SmoothValue(presence, this.presence);
        }

        private float SmoothValue(float? targetRaw, float currRaw)
        {
            if (targetRaw.HasValue)
            {
                float x     = Mathf.Clamp01(targetRaw.Value);
                float alpha = x >= currRaw ? alphaRise : alphaFall;
                return Mathf.Lerp(currRaw, x, alpha);
            }
            else
            {
                // 缺資料時用「溫和衰退」而不是瞬間變 0
                return Mathf.Max(0f, currRaw - decayWhenMissing);
            }
        }

        public bool IsValid()
        {
            return visibility > 0.5f && presence > 0.5f;
        }
    }

    public class PoseLankInfo
    {
        // 原始（或平滑後）對外輸出的點位
        private List<PTInfo> posePtList;

        // 內部平滑狀態
        private List<PTInfo> prevSmoothed;
        private bool bHasPrev;

        /// <summary>
        /// 平滑強度 (0~1)。愈大代表愈貼近當前幀（更靈敏、較少延遲）
        /// 建議 0.4~0.8 之間，預設 0.6
        /// </summary>
        public float SmoothAlpha { get; set; } = 0.4f;

        public int UniqueID { get; set; }
        public Clock clock;
        public Action<int, List<PTInfo>> finishAction;

        /// <summary>
        /// 瞬切門檻（以「歸一化座標」長度衡量，0~sqrt(2)）
        /// 典型人體點位在 0~1 空間中移動，0.12~0.2 通常表現不錯。
        /// </summary>
        public float snapDistance       = 0.15f;
        private float snapDistSqr       = 0f;

        private float accumSkipTime     = 0f;
        private float lastUpdateTime    = 0f;

        public PoseLankInfo() 
        {
            posePtList      = new List<PTInfo>();
            prevSmoothed    = new List<PTInfo>();
            bHasPrev        = false;

            SmoothAlpha     = Mathf.Clamp01(SmoothAlpha);
            snapDistSqr     = snapDistance * snapDistance;
        }

        public void AddFrameLandmarks(List<NormalizedLandmark> landmarkList)
        {
            // 先將這一幀的 raw 轉成 Vector3
            int n = landmarkList?.Count ?? 0;
            if (n == 0)
            {
                posePtList.Clear();
                // 不清 prev，因為可能只是短暫偵測不到
                return;
            }

            lastUpdateTime = clock.Now;

            // 確保容器長度
            EnsureSize(posePtList, n);
            EnsureSize(prevSmoothed, n);

            // 第一次：直接吃 raw 當作起點
            if (!bHasPrev)
            {
                for (int i = 0; i < n; ++i)
                {
                    var lmk         = landmarkList[i];
                    var raw         = new Vector3(lmk.x, lmk.y, 0f);
                    prevSmoothed[i].SetData(raw, lmk.visibility, lmk.presence);
                    posePtList[i].SetData(raw, lmk.visibility, lmk.presence);
                }
                bHasPrev = true;
                return;
            }

            for (int i = 0; i < n; ++i)
            {
                var lmk     = landmarkList[i];
                var raw     = new Vector3(lmk.x, lmk.y, 0f);
                var prev    = prevSmoothed[i];

                // 判斷距離
                Vector2 raw2    = new Vector2(raw.x, raw.y);
                Vector2 prev2   = new Vector2(prev.x, prev.y);
                float distSqr   = (raw2 - prev2).sqrMagnitude;

                Vector3 smoothed;
                if (distSqr > snapDistSqr)
                {
                    // 大位移：瞬切過去，視為換目標
                    smoothed = raw;
                }
                else
                {
                    // 正常微動：指數平滑
                    smoothed = Vector3.Lerp(prev.pos, raw, SmoothAlpha);
                }

                prevSmoothed[i].SetData(smoothed, lmk.visibility, lmk.presence);
                posePtList[i].SetData(smoothed, lmk.visibility, lmk.presence);
            }
        }

        public void AddFrameLandmarks(List<Landmark> landmarkList)
        {
            // 先將這一幀的 raw 轉成 Vector3
            int n = landmarkList?.Count ?? 0;
            if (n == 0)
            {
                posePtList.Clear();
                // 不清 prev，因為可能只是短暫偵測不到
                return;
            }

            lastUpdateTime = clock.Now;

            // 確保容器長度
            EnsureSize(posePtList, n);
            EnsureSize(prevSmoothed, n);

            // 第一次：直接吃 raw 當作起點
            if (!bHasPrev)
            {
                for (int i = 0; i < n; ++i)
                {
                    var lmk         = landmarkList[i];
                    var raw         = new Vector3(lmk.x, lmk.y, lmk.z);
                    prevSmoothed[i].SetData(raw, lmk.visibility, lmk.presence);
                    posePtList[i].SetData(raw, lmk.visibility, lmk.presence);
                }
                bHasPrev = true;
                return;
            }

            for (int i = 0; i < n; ++i)
            {
                var lmk     = landmarkList[i];
                var raw     = new Vector3(lmk.x, lmk.y, lmk.z);
                var prev    = prevSmoothed[i];

                // 判斷距離
                Vector3 raw2    = new Vector3(raw.x, raw.y, raw.z);
                Vector3 prev2   = new Vector3(prev.x, prev.y, prev.z);
                float distSqr   = (raw2 - prev2).sqrMagnitude;

                Vector3 smoothed;
                if (distSqr > snapDistSqr)
                {
                    // 大位移：瞬切過去，視為換目標
                    smoothed = raw;
                }
                else
                {
                    // 正常微動：指數平滑
                    smoothed = Vector3.Lerp(prev.pos, raw, SmoothAlpha);
                }

                prevSmoothed[i].SetData(smoothed, lmk.visibility, lmk.presence);
                posePtList[i].SetData(smoothed, lmk.visibility, lmk.presence);
            }
        }

        public void ClearLandmarks(bool bImmediately = false)
        {
            // 超過一秒以上沒有資料 才要清除點資訊
            if(!bImmediately && clock.Now - lastUpdateTime < 1f)
            {
                return;
            }

            posePtList.Clear();
        }

        public List<PTInfo> GetPtList()
        {
            return posePtList;
        }

        public List<Vector3> GetVecList()
        {
            return posePtList.Select(x => x.pos).ToList();
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

            return (posePtList[leftHipIdx].pos + posePtList[rightHipIdx].pos) / 2f;
        }

        public float DisSqrToScreenCenter(bool bVisualZ)
        {
            if (posePtList == null || posePtList.Count == 0)
            {
                return float.MaxValue;
            }

            // 計算中心點
            Vector3 center = Vector3.zero;
            foreach (PTInfo pt in posePtList)
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

        public void FlushPose()
        {
            finishAction?.Invoke(UniqueID, posePtList);
        }

        private static void EnsureSize(List<PTInfo> list, int size)
        {
            if (list.Count < size)
            {
                int add = size - list.Count;
                for (int i = 0; i < add; ++i) 
                {
                    list.Add(new PTInfo());
                }
            }
            else if (list.Count > size)
            {
                list.RemoveRange(size, list.Count - size);
            }
        }

    }
}
