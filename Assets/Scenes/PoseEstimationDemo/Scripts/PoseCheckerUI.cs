using Intel.RealSense;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using XPlan.UI;
using XPlan.Utility;

namespace XPlan.ImageRecognize.Demo
{
    public class PoseInfo
    {
        public float lastUpdateTime;
        public string poseName;

        public PoseInfo()
        {

        }
    }

    public class PoseCheckerUI : UIBase
    {
        [SerializeField] Text poseTxt;

        private List<PoseInfo> poseInfoList;

        private void Awake()
        {
            poseInfoList = new List<PoseInfo>();

            ListenCall<string>(UICommand.LeftBicepsCurl, PrintPose);
            ListenCall<string>(UICommand.LeftBicepsStraight, PrintPose);
            ListenCall<string>(UICommand.RightBicepsCurl, PrintPose);
            ListenCall<string>(UICommand.RightBicepsStraight, PrintPose);
        }

        private void PrintPose(string poseName)
        {
            int idx = poseInfoList.FindIndex(x => x.poseName == poseName);

            if(!poseInfoList.IsValidIndex(idx))
            {
                poseInfoList.Add(new PoseInfo()
                {
                    lastUpdateTime  = Time.time,
                    poseName        = poseName,
                });
            }
            else
            {
                poseInfoList[idx].lastUpdateTime = Time.time;
            }
        }

        private void Update()
        {
            string resultStr = "";

            for(int i = 0; i < poseInfoList.Count; ++i)
            {
                resultStr += poseInfoList[i].poseName + "  ,  ";
            }
  
            poseTxt.text = $"Curr Pose :  {resultStr}";

            for (int i = poseInfoList.Count - 1; i >= 0; --i)
            {
                if((Time.time - poseInfoList[i].lastUpdateTime) > 0.05f)
                {
                    poseInfoList.RemoveAt(i);
                }
            }
        }
    }
}
