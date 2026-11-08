using System;
using UnityEngine;

namespace TabletopTempo.Core.Input
{
    /// <summary>Tunable thresholds for hand-shape scores (ScriptableObject-backed in Runtime).</summary>
    [Serializable]
    public struct HandShapeTuning
    {
        [Tooltip("Tip-to-wrist / chain-length ratio of a straight finger.")]
        public float straightRatio;
        [Tooltip("Ratio of a fully curled finger.")]
        public float curledRatio;
        [Tooltip("Fingertip distance from the palm plane (m) at which a hand stops counting as flat.")]
        public float flatPlaneTolerance;
        [Tooltip("Thumb–index distance (m) that counts as a full pinch.")]
        public float pinchClosed;
        [Tooltip("Thumb–index distance (m) that counts as no pinch.")]
        public float pinchOpen;

        public static HandShapeTuning Default => new HandShapeTuning
        {
            straightRatio = 0.92f,
            curledRatio = 0.55f,
            flatPlaneTolerance = 0.035f,
            pinchClosed = 0.015f,
            pinchOpen = 0.06f,
        };
    }

    /// <summary>Pure geometry: palm frame, curls, fist / flat / pinch scores.</summary>
    public static class HandShape
    {
        private static readonly int[][] Fingers =
        {
            new[] { 1, 2, 3, 4 },     // thumb: cmc, mcp, ip, tip
            new[] { 5, 6, 7, 8 },     // index
            new[] { 9, 10, 11, 12 },  // middle
            new[] { 13, 14, 15, 16 }, // ring
            new[] { 17, 18, 19, 20 }, // little
        };

        public static void Compute(HandData h, in HandShapeTuning t)
        {
            if (!h.Tracked) return;
            var j = h.Joints;
            Vector3 wrist = j[(int)HandJoint.Wrist];
            Vector3 indexMcp = j[(int)HandJoint.IndexMcp];
            Vector3 middleMcp = j[(int)HandJoint.MiddleMcp];
            Vector3 ringMcp = j[(int)HandJoint.RingMcp];
            Vector3 littleMcp = j[(int)HandJoint.LittleMcp];

            h.PalmCenter = (wrist + indexMcp + middleMcp + ringMcp + littleMcp) * 0.2f;
            h.PalmForward = SafeNormalize(middleMcp - wrist, Vector3.forward);

            // Unity is left-handed: cross(index-wrist, little-wrist) points out of the BACK of a right hand
            // and out of the PALM of a left hand. Flip for the right hand so PalmNormal is always palm-side.
            Vector3 n = Vector3.Cross(indexMcp - wrist, littleMcp - wrist);
            if (h.Side == HandSide.Right) n = -n;
            h.PalmNormal = SafeNormalize(n, Vector3.down);

            float curlSum = 0f;
            for (int f = 0; f < 5; f++)
            {
                // Straightness = base→tip distance / length of the joint chain (1 = straight, ~0.4 = curled).
                var idx = Fingers[f];
                Vector3 b = j[idx[0]], p1 = j[idx[1]], p2 = j[idx[2]], tip = j[idx[3]];
                float chain = Vector3.Distance(b, p1) + Vector3.Distance(p1, p2) + Vector3.Distance(p2, tip);
                float ratio = chain > 1e-5f ? Vector3.Distance(b, tip) / chain : 1f;
                float curl = Mathf.Clamp01((t.straightRatio - ratio) / Mathf.Max(1e-4f, t.straightRatio - t.curledRatio));
                h.Curl[f] = curl;
                if (f > 0) curlSum += curl;
            }

            float fingersCurl = curlSum / 4f;
            h.FistScore = fingersCurl;

            // Flat: fingers straight AND fingertips near the palm plane.
            float planeDist = 0f;
            for (int f = 1; f < 5; f++)
                planeDist += Mathf.Abs(Vector3.Dot(j[Fingers[f][3]] - h.PalmCenter, h.PalmNormal));
            planeDist /= 4f;
            float planar = 1f - Mathf.Clamp01(planeDist / Mathf.Max(1e-4f, t.flatPlaneTolerance));
            h.FlatScore = (1f - fingersCurl) * planar;

            float pinchDist = Vector3.Distance(j[(int)HandJoint.ThumbTip], j[(int)HandJoint.IndexTip]);
            h.PinchStrength = 1f - Mathf.Clamp01((pinchDist - t.pinchClosed) / Mathf.Max(1e-4f, t.pinchOpen - t.pinchClosed));
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            float m = v.magnitude;
            return m > 1e-6f ? v / m : fallback;
        }
    }
}
