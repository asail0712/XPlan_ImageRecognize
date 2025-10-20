using System.Collections.Generic;
using UnityEngine;
using XPlan.Interface;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseMonitorData
    {
        public int uniqueID;
        public float faceAng;
        public Rect rect;
        public float maskConverage;
        public bool bMirror;
        public float roiArea;

        public PoseMonitorData() { }

        public void SetData(PoseMonitorMsg msg)
        {
            uniqueID        = msg.uniqueID;
            faceAng         = msg.faceAng;
            maskConverage   = msg.maskConverage;
            rect            = msg.rect;
            bMirror         = msg.bMirror;
            roiArea         = msg.roiArea;
        }
    }

    public class MonitorInitial : LogicComponent
    {
        public MonitorInitial()
        {
            RegisterNotify<PoseMonitorMsg>((msg) =>
            {
                PoseMonitorData data = new PoseMonitorData();

                data.SetData(msg);

                DirectCallUI<PoseMonitorData>(UICommand.UpdateMonitorData, data);
            });
        }
    }
}
