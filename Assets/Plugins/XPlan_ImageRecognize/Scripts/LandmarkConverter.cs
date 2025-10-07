using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;

using UnityEngine;

using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;

namespace XPlan.ImageRecognize
{
    public static class LandmarkConverter
    {
        public static Mediapipe.Landmark ToMpLandmark(Landmark src)
        {
            return new Mediapipe.Landmark()
            {
                X           = src.x,
                Y           = src.y,
                Z           = src.z,
                Visibility  = src.visibility.GetValueOrDefault()
            };
        }

        public static List<Mediapipe.Landmark> ToMpLandmarkList(this IEnumerable<Landmark> srcList)
        {
            var result = new List<Mediapipe.Landmark>();
            foreach (var lmk in srcList)
            {
                result.Add(ToMpLandmark(lmk));
            }
            return result;
        }

        public static Mediapipe.Landmark ToMpLandmark(this Vector3 src)
        {
            return new Mediapipe.Landmark()
            {
                X = src.x,
                Y = src.y,
                Z = src.z,
                Visibility = default
            };
        }

        public static List<Mediapipe.Landmark> ToMpLandmarkList(this List<Vector3> srcList)
        {
            var result = new List<Mediapipe.Landmark>();
            foreach (var lmk in srcList)
            {
                result.Add(ToMpLandmark(lmk));
            }
            return result;
        }
    }
}
