using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class UIPoseMonitor : MonoBehaviour
    {
        public int uniqueID;
        public float lastUpdateTime;

        [SerializeField] private Text angleTxt;
        [SerializeField] private Text maskCoverageTxt;
        [SerializeField] private Text areaTxt;

        [SerializeField] private GameObject linePrefab;
        [SerializeField] private GameObject pointPrefab;

        [SerializeField] private List<UILine> lineList;
        [SerializeField] private List<UIPoint> pointList;

        private const int SizeOfBounding = 4;

        private void Awake()
        {
            lineList    = new List<UILine>();
            pointList   = new List<UIPoint>();

            // 生成 bounding box 物件
            for (int i = 0; i < SizeOfBounding; ++i)
            {
                GameObject pointGO  = GameObject.Instantiate(pointPrefab);
                GameObject lineGO   = GameObject.Instantiate(linePrefab);

                lineList.Add(lineGO.GetComponent<UILine>());
                pointList.Add(pointGO.GetComponent<UIPoint>());
            }

            for (int i = 0; i < SizeOfBounding; ++i)
            {
                UIPoint point   = pointList[i];
                point.color     = Color.white;

                UILine line     = lineList[i];
                line.color      = Color.white;

                int startIdx    = i;
                int endIdx      = (i + 1) % SizeOfBounding;

                line.startPT    = pointList[startIdx];
                line.endPT      = pointList[endIdx];
            }
        }

        private void Start()
        {
            for (int i = 0; i < SizeOfBounding; ++i)
            {
                GameObject pointGO  = lineList[i].gameObject;
                GameObject lineGO   = pointList[i].gameObject;

                gameObject.transform.parent.gameObject.AddChild(pointGO);
                gameObject.transform.parent.gameObject.AddChild(lineGO);
            }
        }

        public void SetData(PoseMonitorData data, float width, float height)
        {
            gameObject.SetActive(true);

            uniqueID                = data.uniqueID;
            lastUpdateTime          = Time.time;
            
            angleTxt.text           = $"面向角度 {data.faceAng.ToString("0.0")}";
            maskCoverageTxt.text    = $"遮罩覆蓋率 {(data.maskConverage * 100f).ToString("0.0")}%";
            areaTxt.text            = $"ROI面積為 {data.roiArea.ToString("0.000")}";

            Rect rect               = new Rect();
            rect.min                = PosToScreen(data.rect.min, (int)width, (int)height, data.bMirror);
            rect.max                = PosToScreen(data.rect.max, (int)width, (int)height, data.bMirror);
            
            transform.localPosition = rect.center;

            UIPoint point1 = pointList[0];
            point1.SetPos(new Vector3(data.rect.xMin, data.rect.yMin, 0), (int)width, (int)height, data.bMirror);
            UIPoint point2 = pointList[1];
            point2.SetPos(new Vector3(data.rect.xMin, data.rect.yMax, 0), (int)width, (int)height, data.bMirror);
            UIPoint point3 = pointList[2];
            point3.SetPos(new Vector3(data.rect.xMax, data.rect.yMax, 0), (int)width, (int)height, data.bMirror);
            UIPoint point4 = pointList[3];
            point4.SetPos(new Vector3(data.rect.xMax, data.rect.yMin, 0), (int)width, (int)height, data.bMirror);
        }

        private Vector2 PosToScreen(Vector2 pos, float w, float h, bool bMirror)
        {
            float xDisp = (bMirror ? (0.5f - pos.x) : (pos.x - 0.5f)) * w;  // 以中心為原點
            float yDisp = (0.5f - pos.y) * h;  // 垂直方向轉換 + 以中心為原點

            return new Vector2(xDisp, yDisp);
        }

        private void Update()
        {
            if(Time.time - lastUpdateTime > 0.15f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
