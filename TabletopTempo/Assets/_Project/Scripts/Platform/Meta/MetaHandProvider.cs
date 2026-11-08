using System.Collections.Generic;
using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Platform.Meta
{
    /// <summary>
    /// Reads Meta hand tracking (OVRHand + OVRSkeleton, OpenXR 26-joint layout) and maps it to our
    /// 21-joint <see cref="HandData"/> in world (tracking) space. Also drives the XR Simulator's hands,
    /// which come through the same OpenXR path (only 4 poses there: menus, not gameplay).
    /// </summary>
    public sealed class MetaHandProvider : MonoBehaviour, IHandFrameProvider
    {
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;

        private OVRSkeleton _leftSkeleton, _rightSkeleton;
        private readonly Transform[] _leftMap = new Transform[HandData.JointCount];
        private readonly Transform[] _rightMap = new Transform[HandData.JointCount];
        private bool _leftMapped, _rightMapped;

        // Our joint index → OpenXR bone id.
        private static readonly OVRSkeleton.BoneId[] Map =
        {
            OVRSkeleton.BoneId.XRHand_Wrist,
            OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, OVRSkeleton.BoneId.XRHand_ThumbProximal,
            OVRSkeleton.BoneId.XRHand_ThumbDistal, OVRSkeleton.BoneId.XRHand_ThumbTip,
            OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.XRHand_IndexIntermediate,
            OVRSkeleton.BoneId.XRHand_IndexDistal, OVRSkeleton.BoneId.XRHand_IndexTip,
            OVRSkeleton.BoneId.XRHand_MiddleProximal, OVRSkeleton.BoneId.XRHand_MiddleIntermediate,
            OVRSkeleton.BoneId.XRHand_MiddleDistal, OVRSkeleton.BoneId.XRHand_MiddleTip,
            OVRSkeleton.BoneId.XRHand_RingProximal, OVRSkeleton.BoneId.XRHand_RingIntermediate,
            OVRSkeleton.BoneId.XRHand_RingDistal, OVRSkeleton.BoneId.XRHand_RingTip,
            OVRSkeleton.BoneId.XRHand_LittleProximal, OVRSkeleton.BoneId.XRHand_LittleIntermediate,
            OVRSkeleton.BoneId.XRHand_LittleDistal, OVRSkeleton.BoneId.XRHand_LittleTip,
        };

        public string Name => "meta-hands";
        public bool OutputsTableSpace => false;

        public void Bind(OVRHand left, OVRHand right)
        {
            leftHand = left;
            rightHand = right;
            _leftMapped = _rightMapped = false;
        }

        private void Awake()
        {
            if (leftHand == null || rightHand == null) AutoBind();
        }

        private void AutoBind()
        {
            foreach (var h in FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var skel = h.GetComponent<OVRSkeleton>();
                if (skel == null) continue;
                if (skel.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft || skel.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft) leftHand = h;
                else rightHand = h;
            }
        }

        public bool Write(HandFrame target, double inputTime)
        {
            target.Time = inputTime;
            WriteHand(leftHand, ref _leftSkeleton, _leftMap, ref _leftMapped, target.Left);
            WriteHand(rightHand, ref _rightSkeleton, _rightMap, ref _rightMapped, target.Right);
            return true;
        }

        private static void WriteHand(OVRHand hand, ref OVRSkeleton skeleton, Transform[] map, ref bool mapped, HandData dst)
        {
            if (hand == null || !hand.IsTracked)
            {
                dst.SetUntracked();
                return;
            }
            if (skeleton == null) skeleton = hand.GetComponent<OVRSkeleton>();
            if (skeleton == null || !skeleton.IsInitialized || !skeleton.IsDataValid)
            {
                dst.SetUntracked();
                return;
            }
            if (!mapped) mapped = BuildMap(skeleton.Bones, map);
            if (!mapped)
            {
                dst.SetUntracked();
                return;
            }

            dst.Tracked = true;
            dst.Confidence = hand.HandConfidence == OVRHand.TrackingConfidence.High ? 1f : 0.4f;
            for (int i = 0; i < HandData.JointCount; i++) dst.Joints[i] = map[i].position;
            dst.WristRotation = map[0].rotation;
        }

        private static bool BuildMap(IList<OVRBone> bones, Transform[] map)
        {
            if (bones == null || bones.Count == 0) return false;
            for (int i = 0; i < Map.Length; i++)
            {
                map[i] = null;
                for (int b = 0; b < bones.Count; b++)
                {
                    if (bones[b].Id == Map[i]) { map[i] = bones[b].Transform; break; }
                }
                if (map[i] == null) return false;
            }
            return true;
        }
    }
}
