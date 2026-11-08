using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Core.Webcam
{
    /// <summary>One hand as seen by a webcam tracker (MediaPipe layout: 21 joints, same order as <see cref="HandJoint"/>).</summary>
    public sealed class WebcamHandObservation
    {
        public bool Present;
        /// <summary>Tracker's own label: true = it thinks this is a LEFT hand (before calibration swap).</summary>
        public bool LabelLeft;
        public float Score;
        /// <summary>Image landmarks, aspect-corrected (u in [0, aspect], v in [0, 1], v down).</summary>
        public readonly Vector2[] Image = new Vector2[HandData.JointCount];
        /// <summary>Metric 3D landmarks in camera axes (x right, y down, z away from camera), origin near the hand centre.</summary>
        public readonly Vector3[] World = new Vector3[HandData.JointCount];

        public void CopyFrom(WebcamHandObservation o)
        {
            Present = o.Present;
            LabelLeft = o.LabelLeft;
            Score = o.Score;
            System.Array.Copy(o.Image, Image, Image.Length);
            System.Array.Copy(o.World, World, World.Length);
        }

        /// <summary>Image position of the palm centre (mean of wrist + four knuckles, like HandShape).</summary>
        public Vector2 PalmCenterImage =>
            (Image[0] + Image[5] + Image[9] + Image[13] + Image[17]) * 0.2f;

        /// <summary>Metric palm centre in the world-landmark frame.</summary>
        public Vector3 PalmCenterWorld =>
            (World[0] + World[5] + World[9] + World[13] + World[17]) * 0.2f;
    }

    /// <summary>Turns webcam observations into table-space <see cref="HandData"/> (pure maths, unit tested).</summary>
    public static class WebcamHandMapper
    {
        /// <summary>Which side the observation really is, after the calibration's handedness swap.</summary>
        public static HandSide SideOf(WebcamHandObservation obs, WebcamCalibration cal)
        {
            bool left = obs.LabelLeft ^ cal.swapHandedness;
            return left ? HandSide.Left : HandSide.Right;
        }

        /// <summary>
        /// Camera-frame depth (metres) of the palm centre, before hand-scale correction.
        /// Pinhole: (u − cu) = f·(tx + wx)/(tz + wz), same for v. Rearranged this is linear in the
        /// unknown translation T = (tx, ty, tz) of the world-landmark origin:
        ///   f·tx − (u − cu)·tz = (u − cu)·wz − f·wx      and      f·ty − (v − cv)·tz = (v − cv)·wz − f·wy
        /// 42 equations, 3 unknowns → least squares via the 3×3 normal equations. Exact for an ideal
        /// pinhole and correct landmarks, and unaffected by how the hand is tilted.
        /// </summary>
        public static float RawPalmDepth(WebcamHandObservation obs, float focal, Vector2 principal)
        {
            double a00 = 0, a01 = 0, a02 = 0, a11 = 0, a12 = 0, a22 = 0, b0 = 0, b1 = 0, b2 = 0;
            for (int i = 0; i < HandData.JointCount; i++)
            {
                double du = obs.Image[i].x - principal.x;
                double dv = obs.Image[i].y - principal.y;
                Vector3 w = obs.World[i];
                // row (f, 0, -du) · T = du·wz − f·wx
                double r1 = du * w.z - focal * w.x;
                a00 += focal * focal; a02 += -focal * du; a22 += du * du; b0 += focal * r1; b2 += -du * r1;
                // row (0, f, -dv) · T = dv·wz − f·wy
                double r2 = dv * w.z - focal * w.y;
                a11 += focal * focal; a12 += -focal * dv; a22 += dv * dv; b1 += focal * r2; b2 += -dv * r2;
            }
            // Symmetric 3×3 solve (Cramer).
            double det = a00 * (a11 * a22 - a12 * a12) - a01 * (a01 * a22 - a12 * a02) + a02 * (a01 * a12 - a11 * a02);
            if (System.Math.Abs(det) < 1e-12) return 0f;
            double tz = (a00 * (a11 * b2 - b1 * a12) - a01 * (a01 * b2 - b1 * a02) + b0 * (a01 * a12 - a11 * a02)) / det;
            return (float)(tz + obs.PalmCenterWorld.z);
        }

        /// <summary>
        /// Writes joints in table space: palm centre along its image ray at the solved depth, hand
        /// shape from the metric world landmarks rotated from camera axes into table axes.
        /// </summary>
        public static void Map(WebcamHandObservation obs, WebcamCalibration cal, HandData dst)
        {
            if (obs == null || !obs.Present || cal == null)
            {
                dst.SetUntracked();
                return;
            }

            float depth = cal.handScale * RawPalmDepth(obs, cal.focal, cal.PrincipalPoint);
            if (depth <= 1e-3f) depth = cal.cameraDistance - cal.referenceHeight;
            Vector3 palm = cal.ImageToTableAtDepth(obs.PalmCenterImage, depth);
            cal.ImageAxes(out var right, out var down, out var forward);

            var w = obs.World;
            Vector3 worldPalm = obs.PalmCenterWorld;
            for (int i = 0; i < HandData.JointCount; i++)
            {
                Vector3 d = w[i] - worldPalm;
                dst.Joints[i] = palm + right * d.x + down * d.y + forward * d.z;
            }

            dst.Tracked = true;
            dst.Confidence = Mathf.Clamp01(obs.Score);
            Vector3 fwd = dst.Joints[(int)HandJoint.MiddleMcp] - dst.Joints[(int)HandJoint.Wrist];
            dst.WristRotation = fwd.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
        }
    }
}
