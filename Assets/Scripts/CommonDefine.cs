using RootMotion.Dynamics;
using System.Collections.Generic;
using UnityEngine;

namespace XPlan.ImageRecognize.Demo
{
    public static class CommonDefine
    {
        public static readonly List<(int, int)> Connections = new List<(int, int)>
        {
            // Left Eye
            (0, 1),
            (1, 2),
            (2, 3),
            (3, 7),
            // Right Eye
            (0, 4),
            (4, 5),
            (5, 6),
            (6, 8),
            // Lips
            (9, 10),
            // Left Arm
            (11, 13),
            (13, 15),
            // Left Hand
            (15, 17),
            (15, 19),
            (15, 21),
            (17, 19),
            // Right Arm
            (12, 14),
            (14, 16),
            // Right Hand
            (16, 18),
            (16, 20),
            (16, 22),
            (18, 20),
            // Torso
            (11, 12),
            (12, 24),
            (24, 23),
            (23, 11),
            // Left Leg
            (23, 25),
            (25, 27),
            (27, 29),
            (27, 31),
            (29, 31),
            // Right Leg
            (24, 26),
            (26, 28),
            (28, 30),
            (28, 32),
            (30, 32),
        };
    }

    public static class UICommand
    {
        public const string InitScreen          = "InitScreen";

        public const string UpdateMask          = "UpdateMask";
        public const string UpdatePose          = "UpdatePose";
        public const string UpdatePoseMask      = "UpdatePoseMask";


        public const string UpdateMonitorData   = "UpdateMonitorData";
        public const string ClearMonitorData    = "ClearMonitorData";        
    }
}

    
