using System;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class UIPoint : MonoBehaviour
    {
        private Vector3 dispVec;

        public float X
        {
            get => dispVec.x;
        }

        public float Y
        {
            get => dispVec.y;
        }

        public Vector2 Vec2D
        {
            get => new Vector2(X, Y);
        }


        private void Awake()
        {
            dispVec = Vector3.zero;
        }

        public void SetPos(Vector3 pos, float w, float h)
        {
            float xDisp = (pos.x - 0.5f) * w;  // 以中心為原點
            float yDisp = (0.5f - pos.y) * h;  // 垂直方向轉換 + 以中心為原點
            float zDisp = 0f;

            dispVec     = new Vector3(xDisp, yDisp, zDisp);
        }

        private void Update()
        {
            transform.localPosition = dispVec;
        }
    }
}
