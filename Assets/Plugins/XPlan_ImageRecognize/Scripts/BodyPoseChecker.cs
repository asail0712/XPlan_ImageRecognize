using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;
using UnityEngine;

namespace XPlan.ImageRecognize
{
    public enum BodyPoseType
    {
        Nose            = 0,
        LeftEyeInner,
        LeftEye,
        LeftEyeOuter,
        RightEyeInner,
        RightEye,
        RightEyeOuter,

        LeftEar,
        RightEar,
        MouthLeft,
        MouthRight,

        LeftShoulder,
        RightShoulder,
        LeftElbow,
        RightElbow,
        LeftWrist,
        RightWrist,

        LeftPinky,
        RightPinky,
        LeftIndex,
        RightIndex,
        LeftThumb,
        RightThumb,

        LeftHip,
        RightHip,
        LeftKnee,
        RightKnee,

        LeftAnkle,
        RightAnkle,
        LeftHeel,
        RightHeel,
        LeftFootIndex,
        RightFootIndex,

        NumOtType
    }

    public static class BodyPoseChecker
    {
        public static bool IsRightBicepsCurl(this List<Vector3> bodyPoseList, float angle = 90f)
        {
            if (bodyPoseList == null || bodyPoseList.Count != (int)BodyPoseType.NumOtType)
            {
                return false;
            }

            Vector3 rightShoulder   = bodyPoseList[(int)BodyPoseType.RightShoulder];
            Vector3 rightElbow      = bodyPoseList[(int)BodyPoseType.RightElbow];
            Vector3 rightWrist      = bodyPoseList[(int)BodyPoseType.RightWrist];

            // 基本資料健檢（避免 NaN / Infinity）
            if (!IsValid(rightShoulder) || !IsValid(rightElbow) || !IsValid(rightWrist))
            {
                return false;
            }

            // 1) 計算右肘角度（UpperArm: Shoulder->Elbow，Forearm: Wrist->Elbow）
            float elbowDeg      = AngleAt(rightShoulder, rightElbow, rightWrist); // 以「肘」為角點
            bool elbowFlexed    = elbowDeg <= angle;

            // 2) 手腕需接近右肩：距離 < 手臂長度的一定比例
            float upperArmLen   = Vector3.Distance(rightShoulder, rightElbow);
            float forearmLen    = Vector3.Distance(rightElbow, rightWrist);
            float armLenRef     = upperArmLen + forearmLen;

            if (armLenRef <= Mathf.Epsilon)
            {
                return false;
            }

            float wristToShoulder   = Vector3.Distance(rightWrist, rightShoulder);
            bool wristNearShoulder  = wristToShoulder <= armLenRef * 0.9f; // 0.9 可依畫面比例微調(0.8~1.1)

            // （可選）手腕高度至少不低於肘部，避免下垂卻夾角小的誤判(hip以上是負，以下是正)
            bool wristNotBelowElbow = rightWrist.y <= rightElbow.y;

            return elbowFlexed && wristNearShoulder && wristNotBelowElbow;
        }

        public static bool IsLeftBicepsCurl(this List<Vector3> bodyPoseList, float angle = 90f)
        {
            if (bodyPoseList == null || bodyPoseList.Count != (int)BodyPoseType.NumOtType)
            {
                return false;
            }

            Vector3 leftShoulder    = bodyPoseList[(int)BodyPoseType.LeftShoulder];
            Vector3 leftElbow       = bodyPoseList[(int)BodyPoseType.LeftElbow];
            Vector3 leftWrist       = bodyPoseList[(int)BodyPoseType.LeftWrist];

            // 基本資料健檢（避免 NaN / Infinity）
            if (!IsValid(leftShoulder) || !IsValid(leftElbow) || !IsValid(leftWrist))
            {
                return false;
            }

            // 1) 計算左肘角度（UpperArm: Shoulder->Elbow，Forearm: Wrist->Elbow）
            float elbowDeg      = AngleAt(leftShoulder, leftElbow, leftWrist); // 以「肘」為角點
            bool elbowFlexed    = elbowDeg <= angle;

            // 2) 手腕需接近左肩：距離 < 手臂長度的一定比例
            float upperArmLen   = Vector3.Distance(leftShoulder, leftElbow);
            float forearmLen    = Vector3.Distance(leftElbow, leftWrist);
            float armLenRef     = upperArmLen + forearmLen;  // 取較長段當基準
            
            if (armLenRef <= Mathf.Epsilon)
            {
                return false;
            }

            float wristToShoulder   = Vector3.Distance(leftWrist, leftShoulder);
            bool wristNearShoulder  = wristToShoulder <= armLenRef * 0.9f; // 0.9 可依畫面比例微調(0.8~1.1)

            // （可選）手腕高度至少不低於肘部，避免下垂卻夾角小的誤判(hip以上是負，以下是正)
            bool wristNotBelowElbow = leftWrist.y <= leftElbow.y;

            bool b = elbowFlexed && wristNearShoulder && wristNotBelowElbow;
      
            return elbowFlexed && wristNearShoulder && wristNotBelowElbow;
        }

        private static float AngleAt(in Vector3 a, in Vector3 b, in Vector3 c)
        {
            Vector3 v1 = (a - b);
            Vector3 v2 = (c - b);
            
            if (v1.sqrMagnitude < 1e-8f || v2.sqrMagnitude < 1e-8f)
            {
                return 180f;
            }

            v1.Normalize(); 
            v2.Normalize();
            float dot = Mathf.Clamp(Vector3.Dot(v1, v2), -1f, 1f);
            
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        private static bool IsValid(in Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
