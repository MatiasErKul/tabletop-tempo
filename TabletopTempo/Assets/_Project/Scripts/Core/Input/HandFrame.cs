using System;
using UnityEngine;

namespace TabletopTempo.Core.Input
{
    public enum HandSide
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// 21 joints in MediaPipe order, so Meta hands, webcam hands and replays share one layout.
    /// Meta's 26 OpenXR joints map onto these (palm and metacarpals are dropped/derived).
    /// </summary>
    public enum HandJoint
    {
        Wrist = 0,
        ThumbCmc = 1, ThumbMcp = 2, ThumbIp = 3, ThumbTip = 4,
        IndexMcp = 5, IndexPip = 6, IndexDip = 7, IndexTip = 8,
        MiddleMcp = 9, MiddlePip = 10, MiddleDip = 11, MiddleTip = 12,
        RingMcp = 13, RingPip = 14, RingDip = 15, RingTip = 16,
        LittleMcp = 17, LittlePip = 18, LittleDip = 19, LittleTip = 20,
    }

    /// <summary>One hand at one moment. Positions are in table space (metres, Y up). Pre-allocated, copied in place.</summary>
    [Serializable]
    public sealed class HandData
    {
        public const int JointCount = 21;

        public HandSide Side;
        public bool Tracked;
        [Range(0, 1)] public float Confidence;
        public Vector3[] Joints = new Vector3[JointCount];
        public Quaternion WristRotation = Quaternion.identity;

        // ---- derived by HandShape.Compute (not serialized in replays)
        [NonSerialized] public Vector3 PalmCenter;
        /// <summary>Unit vector out of the palm side (for a flat hand on the table: down).</summary>
        [NonSerialized] public Vector3 PalmNormal;
        /// <summary>Unit vector wrist → middle knuckle.</summary>
        [NonSerialized] public Vector3 PalmForward;
        [NonSerialized] public Vector3 PalmVelocity;
        /// <summary>Per finger (thumb..little) 0 = straight, 1 = fully curled.</summary>
        [NonSerialized] public float[] Curl = new float[5];
        [NonSerialized] public float FistScore;
        [NonSerialized] public float FlatScore;
        [NonSerialized] public float PinchStrength;

        public HandData() { }
        public HandData(HandSide side) { Side = side; }

        public Vector3 this[HandJoint j]
        {
            get => Joints[(int)j];
            set => Joints[(int)j] = value;
        }

        public void CopyFrom(HandData o)
        {
            Side = o.Side;
            Tracked = o.Tracked;
            Confidence = o.Confidence;
            Array.Copy(o.Joints, Joints, JointCount);
            WristRotation = o.WristRotation;
            PalmCenter = o.PalmCenter;
            PalmNormal = o.PalmNormal;
            PalmForward = o.PalmForward;
            PalmVelocity = o.PalmVelocity;
            Array.Copy(o.Curl, Curl, 5);
            FistScore = o.FistScore;
            FlatScore = o.FlatScore;
            PinchStrength = o.PinchStrength;
        }

        public void SetUntracked()
        {
            Tracked = false;
            Confidence = 0f;
            PalmVelocity = Vector3.zero;
        }
    }

    /// <summary>Both hands at one moment. <see cref="Time"/> is on the input clock (seconds, DSP timeline).</summary>
    [Serializable]
    public sealed class HandFrame
    {
        public double Time;
        public HandData Left = new HandData(HandSide.Left);
        public HandData Right = new HandData(HandSide.Right);

        public HandData this[HandSide side] => side == HandSide.Left ? Left : Right;

        public void CopyFrom(HandFrame o)
        {
            Time = o.Time;
            Left.CopyFrom(o.Left);
            Right.CopyFrom(o.Right);
        }
    }
}
