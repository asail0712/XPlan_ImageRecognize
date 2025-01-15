using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if OPENCV_PLUS
using OpenCvSharp;
#endif //USE_OPENCV_PLUS

using UnityEngine;

namespace XPlan.ImageRecognize
{
#if OPENCV_PLUS
    public class FindFaceParam
    {
        public CascadeClassifier cascade    = null;
        public int minNeighbor              = 5;
        public double scalorFactor          = 1.03;
        public int cameraIdx                = 0;
        public HaarDetectionType type       = HaarDetectionType.ScaleImage;
        public Size minSize                 = new Size(250, 250);
        public Size maxSize                 = new Size(400, 400);
    }

    public static class ImageUtility
    {        
        // 影片介紹
        // https://www.youtube.com/watch?v=lXvt66A0i3Q
        // https://github.com/opencv/opencv/tree/master/data/haarcascades

        static public bool FindFace(out List<OpenCvSharp.Rect> rectList, Texture2D texture2D, FindFaceParam param)
        {
            Mat frame                   = OpenCvSharp.Unity.TextureToMat(texture2D);
            OpenCvSharp.Rect[] faces    = param.cascade.DetectMultiScale(frame
                    , param.scalorFactor
                    , param.minNeighbor
                    , param.type
                    , param.minSize, param.maxSize);

            bool bFind  = faces.Length > 0;
            rectList    = faces.ToList();

            return bFind;
        }

        static public bool FindFace(out List<OpenCvSharp.Rect> rectList, WebCamTexture webCamTexture, FindFaceParam param)
        {
            if (param.cascade == null || !webCamTexture.didUpdateThisFrame)
            {
                rectList = null;
                return false;
            }

            Mat frame                   = OpenCvSharp.Unity.TextureToMat(webCamTexture);
            OpenCvSharp.Rect[] faces    = param.cascade.DetectMultiScale(frame
                    , param.scalorFactor
                    , param.minNeighbor
                    , param.type
                    , param.minSize, param.maxSize);

            bool bFind  = faces.Length > 0;
            rectList    = faces.ToList();

            return bFind;
        }
    }
#endif //OPENCV_PLUS
}
