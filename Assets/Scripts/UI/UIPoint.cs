using System;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class UIPoint : MonoBehaviour
    {
        [SerializeField] public Color color = Color.yellow;

        private Image img;
        private Vector3 dispVec;
        public float lastUpdateTime;

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

        public bool Enable { get { return gameObject.activeSelf; } }


        private void Awake()
        {
            dispVec     = Vector3.zero;
            img         = GetComponent<Image>();
            img.color   = color;
        }

        public void SetPos(Vector3 pos, float w, float h, bool bMirror)
        {
            float xDisp = (bMirror? (0.5f - pos.x) : (pos.x - 0.5f)) * w;  // 以中心為原點
            float yDisp = (0.5f - pos.y) * h;  // 垂直方向轉換 + 以中心為原點
            float zDisp = 0f;

            dispVec         = new Vector3(xDisp, yDisp, zDisp);
            lastUpdateTime  = Time.time;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            transform.localPosition = dispVec;
            
            if(Time.time - lastUpdateTime > 0.2f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
