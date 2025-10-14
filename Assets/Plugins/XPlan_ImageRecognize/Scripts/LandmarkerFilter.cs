using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Unity;

using System;
using System.Collections.Generic;
using UnityEngine;

using XPlan.Utility;

using Rect = UnityEngine.Rect;

namespace XPlan.ImageRecognize
{
    public static class LandmarkerFilter
    {
        public static bool IsPoseValid(this NormalizedLandmarks landmarks)
        {
            /**********************************************
             * 1. 可信任關節比例
             * *******************************************/
            if (!IsValidJoints(landmarks))
            {
                return false;
            }

            /**********************************************
             * 2. 檢查身體面相
             * *******************************************/
            if (!IsFaceFront(landmarks))
            {
                return false;
            }

            /**********************************************
             * 3. 外框大小
             * *******************************************/
            if (!IsBBoxValid(landmarks))
            {
                return false;
            }

            return true;
        }

        private static float[] maskArray = null;

        public static bool IsMaskValid(this Mediapipe.Image maskImg, float minCoverage = 0.02f, float maxCoverage = 0.35f, float threshold = 0.5f)
        {
            /**********************************************
             * 遮罩覆蓋度計算
             **********************************************/
            if (maskImg == null || maskImg.isDisposed)
                return false;

            // 取得遮罩畫面尺寸
            int width       = maskImg.Width();
            int height      = maskImg.Height();
            int totalPixels = width * height;
            
            if(maskArray == null || maskArray.Length != totalPixels)
            {
                maskArray = new float[totalPixels];
            }

            // 取出遮罩像素資料
            // Mediapipe.Image 通常是單通道灰階 (float or uint8)
            // 這裡轉成 Texture2D 來存取，或直接用 Unsafe API 讀 buffer
            // 這裡假設你已在 CPU 上可讀（非 GPU buffer）
            // ↓ 用 ImageFrame 版本更保險
            if (!maskImg.TryReadChannelNormalized(0, maskArray, false))
            {
                return true;
            }

            // 統計大於閾值的像素數
            int humanPixels = 0;
            for (int i = 0; i < maskArray.Length; i++)
            {
                if (maskArray[i] >= threshold)
                {
                    humanPixels++;
                }
            }

            // 計算遮罩覆蓋比例
            float maskCoverage = (float)humanPixels / totalPixels;

            /**********************************************
             * 有效性判斷
             **********************************************/
            return maskCoverage >= minCoverage && maskCoverage <= maxCoverage;
        }

        public static bool IsValidJoints(this NormalizedLandmarks landmarks, float visibility = 0.5f, float presence = 0.5f)
        {
            bool bIsNoseValid           = landmarks.IsValidJoint(BodyPoseType.Nose, visibility, presence);
            bool bIsLeftEyeValid        = landmarks.IsValidJoint(BodyPoseType.LeftEyeOuter, visibility, presence);
            bool bIsRightEyeValid       = landmarks.IsValidJoint(BodyPoseType.RightEyeOuter, visibility, presence);
            bool bIsLeftShoulderValid   = landmarks.IsValidJoint(BodyPoseType.LeftShoulder, visibility, presence);
            bool bIsRightShoulderValid  = landmarks.IsValidJoint(BodyPoseType.RightShoulder, visibility, presence);
            bool bIsLeftHipValid        = landmarks.IsValidJoint(BodyPoseType.LeftHip, visibility, presence);
            bool bIsRightHipValid       = landmarks.IsValidJoint(BodyPoseType.RightHip, visibility, presence);


            // === 頭部：三取二 ===
            int headCount = 0;
            if (bIsNoseValid) headCount++;
            if (bIsLeftEyeValid) headCount++;
            if (bIsRightEyeValid) headCount++;
            bool headOk = headCount >= 2;

            // === 軀幹：總數≥2 ===
            int torsoCount      = (bIsLeftShoulderValid ? 1 : 0) + (bIsRightShoulderValid ? 1 : 0)
                                + (bIsLeftHipValid ? 1 : 0) + (bIsRightHipValid ? 1 : 0);
            bool torsoOk        = torsoCount >= 2;

            return headOk && torsoOk;
        }

        public static bool IsValidJoint(this NormalizedLandmarks landmarks, BodyPoseType type, float visibility = 0.5f, float presence = 0.5f)
        {
            NormalizedLandmark landmark = landmarks.landmarks[(int)type];

            return landmark.visibility > visibility && landmark.presence > presence;
        }

        public static bool IsFaceFront(this NormalizedLandmarks landmarks, float ang = 75f)
        {
            if (!landmarks.FindJointPos(BodyPoseType.LeftShoulder, out Vector3 leftShoulderPos))
            {
                return false;
            }

            if (!landmarks.FindJointPos(BodyPoseType.RightShoulder, out Vector3 rightShoulderPos))
            {
                return false;
            }

            if (!landmarks.FindJointPos(BodyPoseType.LeftHip, out Vector3 leftHipPos))
            {
                return false;
            }

            if (!landmarks.FindJointPos(BodyPoseType.RightHip, out Vector3 rightHipPos))
            {
                return false;
            }

            // 小於門檻視為「面向相機」
            return GetFaceFrontAngle(leftShoulderPos, rightShoulderPos, leftHipPos, rightHipPos) <= ang;
        }

        public static float GetFaceFrontAngle(Vector3 leftShoulderPos, Vector3 rightShoulderPos, Vector3 leftHipPos, Vector3 rightHipPos)
        {
            float facingAng     = 180f;

            // 核心向量
            Vector3 shoulderVec = (rightShoulderPos - leftShoulderPos);
            Vector3 hipCenter   = (leftHipPos + rightHipPos) * 0.5f;
            Vector3 shCenter    = (leftShoulderPos + rightShoulderPos) * 0.5f;
            Vector3 torsoUp     = (shCenter - hipCenter);

            // 基本健檢（避免零長向量造成 NaN）
            const float eps = 1e-6f;
            if (shoulderVec.sqrMagnitude < eps || torsoUp.sqrMagnitude < eps)
            {
                return facingAng;
            }

            shoulderVec.Normalize();
            torsoUp.Normalize();

            // 身體前向：肩線(左右) × 軀幹(下->上)
            // 注意：左右手標系/鏡像會影響前向正負，因此後面用 ±Z 兩側都試一次
            Vector3 bodyForward = Vector3.Cross(shoulderVec, torsoUp).normalized;

            // 與相機前向的夾角（相機朝 -Z 或 +Z 皆嘗試）
            float angToNegZ = Vector3.Angle(bodyForward, new Vector3(0f, 0f, -1f));
            float angToPosZ = Vector3.Angle(bodyForward, new Vector3(0f, 0f, 1f));
            facingAng       = Mathf.Min(angToNegZ, angToPosZ);

            // 小於門檻視為「面向相機」
            return facingAng;
        }

        public static bool IsBBoxValid(this NormalizedLandmarks landmarks, float minRatio = 0.01f, float maxRatio = 0.35f)
        {
            Rect rect       = GetBoundingBox(landmarks);
            float bboxArea  = rect.width * rect.height;

            return bboxArea >= minRatio && bboxArea <= maxRatio;
        }

        public static Rect GetBoundingBox(this NormalizedLandmarks landmarks)
        {            
            List<float> xAxisList = new List<float>();
            List<float> yAxisList = new List<float>();

            // 遍歷所有關節
            foreach (BodyPoseType joint in Enum.GetValues(typeof(BodyPoseType)))
            {
                if (!landmarks.FindJointPos(joint, out Vector3 pos))
                    continue; // 無效關節略過

                // 只取在畫面範圍內的點
                if (pos.x < 0f || pos.x > 1f || pos.y < 0f || pos.y > 1f)
                    continue;

                xAxisList.Add(pos.x);
                yAxisList.Add(pos.y);
            }

            var xAxisValue = Percentile(xAxisList, 0.1f, 0.9f);
            var yAxisValue = Percentile(yAxisList, 0.1f, 0.9f);

            // 若沒找到有效點，回傳空
            if (xAxisValue.Item1 > xAxisValue.Item2 || yAxisValue.Item1 > yAxisValue.Item2)
                return Rect.zero;

            return new Rect(xAxisValue.Item1, yAxisValue.Item1, xAxisValue.Item2 - xAxisValue.Item1, yAxisValue.Item2 - yAxisValue.Item1);
        }

        public static bool FindJointPos(this NormalizedLandmarks landmarks, BodyPoseType type, out Vector3 pos)
        {
            int idx = (int)type;
            pos     = Vector3.zero;

            if (!landmarks.landmarks.IsValidIndex(idx))
            {
                return false;
            }

            if (!landmarks.IsValidJoint(type))
            {
                return false;
            }

            pos = new Vector3(landmarks.landmarks[idx].x, landmarks.landmarks[idx].y, landmarks.landmarks[idx].z);

            return true;
        }
        /******************************************
         * 工具類
         * ***************************************/
        private static (float, float) Percentile(List<float> data, float minPercentile, float maxPercentile)
        {
            if (data == null || data.Count == 0) return (0f, 0f);

            data.Sort(); // 排序

            float minIndex  = (data.Count - 1) * Mathf.Clamp01(minPercentile);
            float maxIndex  = (data.Count - 1) * Mathf.Clamp01(maxPercentile);
            int lower       = Mathf.FloorToInt(minIndex);
            int upper       = Mathf.CeilToInt(maxIndex);

            float minValue  = data[lower];
            float maxValue  = data[upper];

            return (minValue, maxValue);
        }
    }
}
