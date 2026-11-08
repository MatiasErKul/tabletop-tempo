using NUnit.Framework;
using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Tests
{
    public class HandShapeTests
    {
        private static HandData Posed(HandSide side, Quaternion rot, float curl)
        {
            var h = new HandData(side);
            SyntheticHand.Pose(h, side, new Vector3(0.1f, 0.05f, 0.2f), rot, curl);
            HandShape.Compute(h, HandShapeTuning.Default);
            return h;
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void PalmDown_NormalPointsDown(HandSide side)
        {
            var h = Posed(side, SyntheticHand.PalmDown, 0f);
            Assert.Less(Vector3.Angle(h.PalmNormal, Vector3.down), 15f, $"{side} palm normal {h.PalmNormal}");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void PalmUp_NormalPointsUp(HandSide side)
        {
            var h = Posed(side, SyntheticHand.PalmUp, 0f);
            Assert.Less(Vector3.Angle(h.PalmNormal, Vector3.up), 15f, $"{side} palm normal {h.PalmNormal}");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void ClapPose_PalmsFaceEachOther(HandSide side)
        {
            var h = Posed(side, SyntheticHand.PalmFacingInward(side), 0f);
            Vector3 inward = side == HandSide.Left ? Vector3.right : Vector3.left;
            Assert.Less(Vector3.Angle(h.PalmNormal, inward), 20f, $"{side} palm normal {h.PalmNormal}");
        }

        [Test]
        public void PalmCenter_LandsWhereRequested()
        {
            var h = Posed(HandSide.Right, SyntheticHand.PalmDown, 0f);
            Assert.Less(Vector3.Distance(h.PalmCenter, new Vector3(0.1f, 0.05f, 0.2f)), 0.002f);
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void FlatHand_IsFlat_NotFist(HandSide side)
        {
            var h = Posed(side, SyntheticHand.PalmDown, 0f);
            Assert.Greater(h.FlatScore, 0.8f, "flat");
            Assert.Less(h.FistScore, 0.2f, "fist");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void CurledHand_IsFist_NotFlat(HandSide side)
        {
            var h = Posed(side, SyntheticHand.PalmDown, 1f);
            Assert.Greater(h.FistScore, 0.8f, "fist");
            Assert.Less(h.FlatScore, 0.3f, "flat");
        }

        [Test]
        public void HalfCurl_IsInBetween()
        {
            var h = Posed(HandSide.Right, SyntheticHand.PalmDown, 0.5f);
            Assert.That(h.FistScore, Is.InRange(0.2f, 0.8f));
        }

        [Test]
        public void ThumbTouchingIndex_IsPinch()
        {
            var h = Posed(HandSide.Right, SyntheticHand.PalmDown, 0f);
            Assert.Less(h.PinchStrength, 0.5f, "open hand is not a pinch");
            h.Joints[(int)HandJoint.ThumbTip] = h.Joints[(int)HandJoint.IndexTip] + new Vector3(0.005f, 0, 0);
            HandShape.Compute(h, HandShapeTuning.Default);
            Assert.Greater(h.PinchStrength, 0.9f);
        }

        [Test]
        public void Untracked_IsLeftAlone()
        {
            var h = new HandData(HandSide.Left) { Tracked = false, FistScore = 0.123f };
            HandShape.Compute(h, HandShapeTuning.Default);
            Assert.AreEqual(0.123f, h.FistScore);
        }
    }

    public class HandHistoryTests
    {
        [Test]
        public void Push_ComputesPalmVelocity()
        {
            var hist = new HandFrameHistory(8);
            var f = new HandFrame();
            for (int i = 0; i < 3; i++)
            {
                f.Time = i * 0.01;
                SyntheticHand.Pose(f.Right, HandSide.Right, new Vector3(0, 0.2f - 0.01f * i, 0), SyntheticHand.PalmDown, 0f);
                HandShape.Compute(f.Right, HandShapeTuning.Default);
                f.Left.SetUntracked();
                hist.Push(f);
            }
            Assert.AreEqual(-1f, hist.Latest.Right.PalmVelocity.y, 0.05f, "moving down at 1 m/s");
            Assert.AreEqual(Vector3.zero, hist.Latest.Left.PalmVelocity);
        }

        [Test]
        public void RingBuffer_WrapsAndKeepsOrder()
        {
            var hist = new HandFrameHistory(4);
            var f = new HandFrame();
            for (int i = 0; i < 10; i++)
            {
                f.Time = i;
                hist.Push(f);
            }
            Assert.AreEqual(4, hist.Count);
            Assert.AreEqual(9.0, hist.Get(0).Time);
            Assert.AreEqual(6.0, hist.Get(3).Time);
            Assert.IsNull(hist.Get(4));
            Assert.AreEqual(7.0, hist.AtOrBefore(7.5).Time);
        }

        [Test]
        public void Push_DoesNotAlias_SourceFrame()
        {
            var hist = new HandFrameHistory(4);
            var f = new HandFrame { Time = 1 };
            hist.Push(f);
            f.Time = 2;
            Assert.AreEqual(1.0, hist.Latest.Time);
        }
    }

    public class TableSpaceTests
    {
        [Test]
        public void RoundTrip()
        {
            var t = new TableSpace(new Vector3(0.3f, 0.74f, 0.5f), 30f);
            var w = new Vector3(0.1f, 0.9f, 0.7f);
            Assert.Less(Vector3.Distance(w, t.ToWorld(t.ToTable(w))), 1e-5f);
        }

        [Test]
        public void Origin_MapsToZero_AndUpStaysUp()
        {
            var t = new TableSpace(new Vector3(1f, 0.74f, 2f), 90f);
            Assert.Less(t.ToTable(new Vector3(1f, 0.74f, 2f)).magnitude, 1e-5f);
            Assert.Less(Vector3.Angle(t.DirToTable(Vector3.up), Vector3.up), 0.01f);
            // 90° yaw: the table's +Z (away from player) is world +X.
            Assert.Less(Vector3.Distance(t.ToTable(new Vector3(2f, 0.74f, 2f)), new Vector3(0, 0, 1)), 1e-5f);
        }
    }

    public class ReplaySessionTests
    {
        [Test]
        public void TextRoundTrip_PreservesFrames()
        {
            var s = new ReplaySession { Source = "test", Note = "two frames" };
            var f = new HandFrame { Time = 0.5 };
            SyntheticHand.Pose(f.Left, HandSide.Left, new Vector3(-0.1f, 0.1f, 0.2f), SyntheticHand.PalmDown, 0.3f);
            f.Right.SetUntracked();
            s.Add(f);
            f.Time = 0.6;
            s.Add(f);

            var back = ReplaySession.FromText(s.ToText());
            Assert.AreEqual("test", back.Source);
            Assert.AreEqual("two frames", back.Note);
            Assert.AreEqual(2, back.Frames.Count);
            Assert.AreEqual(0.6, back.Frames[1].Time, 1e-6);
            Assert.IsTrue(back.Frames[0].Left.Tracked);
            Assert.IsFalse(back.Frames[0].Right.Tracked);
            Assert.Less(Vector3.Distance(back.Frames[0].Left.Joints[8], s.Frames[0].Left.Joints[8]), 0.0002f);
        }

        [Test]
        public void BadLine_Throws()
        {
            Assert.Throws<System.FormatException>(() => ReplaySession.FromText("# ttrec 1\n0.1 1 2 3\n"));
        }

        [Test]
        public void Provider_PlaysFramesByTime()
        {
            var s = new ReplaySession();
            var f = new HandFrame();
            for (int i = 0; i < 5; i++)
            {
                f.Time = i * 0.1;
                f.Left.Confidence = i / 10f;
                s.Add(f);
            }
            var p = new ReplayHandProvider(s);
            var outFrame = new HandFrame();
            p.Write(outFrame, 100.0);             // starts playback at input time 100
            Assert.AreEqual(0f, outFrame.Left.Confidence, 1e-6);
            p.Write(outFrame, 100.25);
            Assert.AreEqual(0.2f, outFrame.Left.Confidence, 1e-6);
            Assert.AreEqual(100.25, outFrame.Time, 1e-9, "time is re-stamped onto the input clock");
            p.Write(outFrame, 101.0);
            Assert.IsTrue(p.Finished);
        }
    }
}
