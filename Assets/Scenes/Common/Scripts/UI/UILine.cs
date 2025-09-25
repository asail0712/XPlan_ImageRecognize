using UnityEngine;
using UnityEngine.UI;

namespace XPlan.ImageRecognize.Demo
{
    public class UILine : Graphic
    {
        public Vector2 start    = Vector2.zero;
        public Vector2 end      = new Vector2(100, 100);
        public float thickness  = 5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

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
