using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class MonitorUI : UIBase
    {
        [SerializeField] private GameObject monitorPrefab;
        [SerializeField] private GameObject monitorRoot;
        [SerializeField] private RawImage screen;

        private List<UIPoseMonitor> monitorList;

        private static int MaxPose = 3;

        private void Awake()
        {
            monitorList = new List<UIPoseMonitor>();

            for(int i = 0; i < MaxPose; ++i)
            {
                GameObject monitorGO    = GameObject.Instantiate(monitorPrefab);
                UIPoseMonitor monitor   = monitorGO.GetComponent<UIPoseMonitor>();
                monitor.uniqueID        = i;

                monitorList.Add(monitor);
                monitorRoot.AddChild(monitorGO);
            }

            ListenCall<PoseMonitorData>(UICommand.UpdateMonitorData, (data) =>
            {
                int idx = monitorList.FindIndex(x => x.uniqueID == data.uniqueID);
                
                if (monitorList.IsValidIndex(idx))
                {
                    monitorList[idx].SetData(data, screen.rectTransform.sizeDelta.x, screen.rectTransform.sizeDelta.y);
                }
            });
        }
    }
}
