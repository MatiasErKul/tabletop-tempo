using UnityEngine;

namespace TabletopTempo.Core.Input
{
    /// <summary>
    /// Builds plausible 21-joint hands from a few parameters. Used by unit tests (scripted claps,
    /// pounds, taps…), the ghost-hand demos and as a stand-in when no tracker is available.
    /// Canonical pose: right hand, palm down (palm normal −Y), fingers along +Z, thumb toward −X.
    /// </summary>
    public static class SyntheticHand
    {
        // Knuckle (MCP) positions relative to the wrist, metres.
        private static readonly Vector3[] Mcp =
        {
            new Vector3(-0.040f, -0.008f, 0.050f), // thumb MCP (thumb chain handled separately)
            new Vector3(-0.025f, 0f, 0.085f),      // index
            new Vector3(-0.005f, 0f, 0.088f),      // middle
            new Vector3(0.014f, 0f, 0.084f),       // ring
            new Vector3(0.031f, 0f, 0.075f),       // little
        };

        private static readonly float[][] Segments =
        {
            new[] { 0.032f, 0.028f, 0.024f },     // thumb: mcp→ip→tip (+cmc separately)
            new[] { 0.040f, 0.024f, 0.020f },     // index: mcp→pip→dip→tip
            new[] { 0.044f, 0.028f, 0.022f },
            new[] { 0.041f, 0.026f, 0.021f },
            new[] { 0.032f, 0.019f, 0.018f },
        };

        private static readonly Vector3 ThumbCmc = new Vector3(-0.020f, -0.005f, 0.025f);
        private static readonly Vector3 CanonicalPalmCenter = (Vector3.zero + Mcp[1] + Mcp[2] + Mcp[3] + Mcp[4]) * 0.2f;

        /// <summary>Common orientations (palm normal direction in table space).</summary>
        public static Quaternion PalmDown => Quaternion.identity;
        public static Quaternion PalmUp => Quaternion.Euler(0f, 0f, 180f);

        /// <summary>Palm facing the other hand (for claps): right palm faces −X (toward left), left palm faces +X.</summary>
        public static Quaternion PalmFacingInward(HandSide side) =>
            Quaternion.Euler(0f, 0f, side == HandSide.Right ? -90f : 90f);

        /// <summary>
        /// Writes a full pose into <paramref name="h"/>.
        /// </summary>
        /// <param name="palmCenter">Where the palm centre should end up (table space).</param>
        /// <param name="orientation">Hand orientation applied to the canonical palm-down pose.</param>
        /// <param name="curl">0 = flat/straight fingers, 1 = fist.</param>
        public static void Pose(HandData h, HandSide side, Vector3 palmCenter, Quaternion orientation, float curl,
            float confidence = 1f)
        {
            h.Side = side;
            h.Tracked = true;
            h.Confidence = confidence;
            float mirror = side == HandSide.Left ? -1f : 1f;
            var j = h.Joints;

            // Local (canonical) joints, before mirroring/orientation.
            j[0] = Vector3.zero;
            // Thumb: cmc fixed, mcp/ip/tip curl a little toward the palm.
            j[1] = ThumbCmc;
            Chain(j, 2, Mcp[0], new Vector3(-0.35f, -0.1f, 1f).normalized, Segments[0], curl * 0.6f, includeBase: true, joints: 3);
            for (int f = 1; f < 5; f++)
            {
                int baseIdx = 1 + f * 4; // 5, 9, 13, 17
                Chain(j, baseIdx, Mcp[f], Vector3.forward, Segments[f], curl, includeBase: true, joints: 4);
            }

            // Mirror for the left hand, orient, then move so the palm centre lands at palmCenter.
            Vector3 pc = new Vector3(CanonicalPalmCenter.x * mirror, CanonicalPalmCenter.y, CanonicalPalmCenter.z);
            for (int i = 0; i < HandData.JointCount; i++)
            {
                Vector3 p = j[i];
                p.x *= mirror;
                j[i] = palmCenter + orientation * (p - pc);
            }
            h.WristRotation = orientation;
        }

        /// <summary>Lays out a finger chain starting at <paramref name="basePos"/>, curling around the local X axis.</summary>
        private static void Chain(Vector3[] j, int firstIndex, Vector3 basePos, Vector3 dir, float[] seg, float curl,
            bool includeBase, int joints)
        {
            // Bend per joint at full curl: MCP 75°, PIP 100°, DIP 70°.
            float[] bend = { 75f, 100f, 70f };
            int k = firstIndex;
            Vector3 p = basePos;
            if (includeBase) j[k++] = p;
            Quaternion rot = Quaternion.identity;
            int segCount = joints - 1;
            for (int s = 0; s < segCount; s++)
            {
                rot = rot * Quaternion.Euler(bend[s] * curl, 0f, 0f); // +X pitch: +Z bends toward −Y (palm side)
                p += rot * dir * seg[s];
                j[k++] = p;
            }
        }
    }
}
