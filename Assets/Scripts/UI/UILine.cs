using UnityEngine;
using UnityEngine.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class UILine : Graphic
    {
        public UIPoint startPT  = null;
        public UIPoint endPT    = null;
        public float thickness  = 5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (startPT == null || endPT == null)
            {
                return;
            }

            if (!startPT.Enable || !endPT.Enable)
            {
                return; // 不呼叫 SetVerticesDirty
            }

            Vector2 start   = startPT.Vec2D;
            Vector2 end     = endPT.Vec2D;

            Vector2 dir     = (end - start).normalized;
            Vector2 normal  = new Vector2(-dir.y, dir.x) * thickness * 0.5f;

            UIVertex v      = UIVertex.simpleVert;
            v.color         = color;

            v.position      = start - normal; vh.AddVert(v);
            v.position      = start + normal; vh.AddVert(v);
            v.position      = end + normal; vh.AddVert(v);
            v.position      = end - normal; vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void Update()
        {
            SetVerticesDirty();
        }
    }
}
