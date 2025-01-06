using UnityEngine;

namespace XPlan.MediaPipe
{
    public enum ImageSourceType
    {
        WebCamera,
        Kinect,
    }

    public static class UICommand
    {
        public const string InitScreen = "InitScreen";
        public const string UpdateMask = "UpdateMask";
    }
}
