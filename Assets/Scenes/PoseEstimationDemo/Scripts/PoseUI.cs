using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using XPlan.Recycle;
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

        [SerializeField] private RawImage screen;

        private static int MaxPose              = 3;
        private static int PerPoseMaxPointNum   = 33;

        private List<UIPoint> pointList;
        private List<UILine> lineList;
        private bool bReceiveData;
        private float showTime;
        private float width;
        private float height;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            pointList           = new List<UIPoint>();
            lineList            = new List<UILine>();
            bReceiveData        = false;
            width               = 0f;
            height              = 0f;

            for (int i = 0; i < PerPoseMaxPointNum * MaxPose; ++i)
            {
                GameObject pointGO = GameObject.Instantiate(pointPrefab);
                pointList.Add(pointGO.GetComponent<UIPoint>());
                pointRoot.AddChild(pointGO);
            }

            for (int i = 0; i < MaxPose; ++i)
            {
                for (int j = 0; j < CommonDefine.Connections.Count; ++j)
                {
                    GameObject lineGO   = GameObject.Instantiate(linePrefab);
                    UILine uiLine       = lineGO.GetComponent<UILine>();

                    var pair            = CommonDefine.Connections[j];
                    int idx1            = pair.Item1 + i * PerPoseMaxPointNum;
                    int idx2            = pair.Item2 + i * PerPoseMaxPointNum;

                    uiLine.startPT      = pointList[idx1];
                    uiLine.endPT        = pointList[idx2];

                    lineList.Add(uiLine);
                    lineRoot.AddChild(lineGO);
                }
            }

            ListenCall<(List<Vector3>, bool)>(UICommand.UpdatePos, (param) => 
            {
                List<Vector3> posList   = param.Item1;
                bool bMirror            = param.Item2;

                if (posList == null || width.Equals(0f) || height.Equals(0f))
                {
                    return;
                }

                bReceiveData    = true;
                showTime        = 0f;

                //Debug.Log($"Count: {modelCount} Len: {posList.Count}");

                for (int i = 0; i < pointList.Count; ++i)
                {
                    pointList[i].Enable = i < posList.Count;

                    if (pointList[i].Enable)
                    {
                        Vector3 mediapipeXYZ = posList[i];
                        pointList[i].SetPos(mediapipeXYZ, width, height, bMirror);                        
                    }
                }
            });
        }

        private void Update()
        {
            if (bReceiveData)
            {
                showTime += Time.deltaTime;

                if (showTime > 0.3f)
                {
                    bReceiveData = false;
                }
            }

            pointRoot.SetActive(bReceiveData);
            lineRoot.SetActive(bReceiveData);

            width   = screen.rectTransform.sizeDelta.x;
            height  = screen.rectTransform.sizeDelta.y;
        }
    }
}
