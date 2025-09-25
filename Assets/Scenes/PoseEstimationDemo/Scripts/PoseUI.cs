using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseUI : UIBase
    {
        [SerializeField] private GameObject pointPrefab;
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private GameObject pointRoot;
        [SerializeField] private GameObject lineRoot;

        private static int MaxPointNum = 33;

        private List<UIPoint> pointList;
        private List<UILine> lineList;
        private bool bReceiveData;
        private float showTime;

        private Rect rect;
        private float w;
        private float h;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            pointList       = new List<UIPoint>();
            lineList        = new List<UILine>();
            bReceiveData    = false;

            rect        = transform.parent.gameObject.GetComponent<RectTransform>().rect;
            w           = rect.width;
            h           = rect.height;

            for (int i = 0; i < MaxPointNum; ++i)
            {
                GameObject pointGO = GameObject.Instantiate(pointPrefab);
                pointList.Add(pointGO.GetComponent<UIPoint>());
                pointRoot.AddChild(pointGO);
            }

            for (int i = 0; i < CommonDefine.Connections.Count; ++i)
            {
                GameObject lineGO = GameObject.Instantiate(linePrefab);                
                lineList.Add(lineGO.GetComponent<UILine>());
                lineRoot.AddChild(lineGO);
            }

            ListenCall<List<Vector3>>(UICommand.UpdatePos, (posList) => 
            {
                if (posList == null || MaxPointNum != posList.Count)
                {
                    return;
                }

                bReceiveData    = true;
                showTime        = 0f;

                for (int i = 0; i < MaxPointNum; ++i)
                {
                    Vector3 mediapipeXYZ = posList[i];

                    pointList[i].SetPos(mediapipeXYZ, w, h);
                }

                for(int i = 0; i < CommonDefine.Connections.Count; ++i)
                {
                    var pair    = CommonDefine.Connections[i];
                    int idx1    = pair.Item1;
                    int idx2    = pair.Item2;

                    UILine line     = lineList[i];
                    line.start      = pointList[idx1].Vec2D;
                    line.end        = pointList[idx2].Vec2D;
                    line.thickness  = 3f;
                }
            });
        }

        private void Update()
        {
            if(bReceiveData)
            {
                showTime += Time.deltaTime;

                if(showTime > 0.3f)
                {
                    bReceiveData = false;
                }
            }

            pointRoot.SetActive(bReceiveData);
            lineRoot.SetActive(bReceiveData);
        }
    }
}
