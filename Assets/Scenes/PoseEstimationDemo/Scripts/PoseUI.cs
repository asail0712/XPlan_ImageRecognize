using System.Collections.Generic;

using UnityEngine;
using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseUI : UIBase
    {
        [SerializeField] private GameObject pointPrefab;
        [SerializeField] private GameObject pointRoot;

        private List<GameObject> poseGoList;
        private List<Vector3> posList;
        private static int MaxPointNum = 33;
        
        private Rect rect;
        private float w;
        private float h;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            poseGoList  = new List<GameObject>();
            posList     = null;
            
            rect        = transform.parent.gameObject.GetComponent<RectTransform>().rect;
            w           = rect.width;
            h           = rect.height;


            for (int i = 0; i < MaxPointNum; ++i)
            {
                GameObject pointGO = GameObject.Instantiate(pointPrefab);

                poseGoList.Add(pointGO);
                pointRoot.AddChild(pointGO);
            }

            ListenCall<List<Vector3>>(UICommand.UpdatePos, (posList) => 
            {
                if(posList.Count != MaxPointNum)
                {
                    return;
                }

                this.posList = posList;
            });
        }

        private void Update()
        {
            if(posList == null)
            {
                return;
            }

            for (int i = 0; i < MaxPointNum; ++i)
            {
                Vector3 mediapipeXYZ = posList[i];

                float xDisp = (mediapipeXYZ.x - 0.5f) * w;  // 以中心為原點
                float yDisp = (0.5f - mediapipeXYZ.y) * h;  // 垂直方向轉換 + 以中心為原點
                float zDisp = 0f;

                poseGoList[i].transform.localPosition = new Vector3(xDisp, yDisp, zDisp);
            }
        }
    }
}
