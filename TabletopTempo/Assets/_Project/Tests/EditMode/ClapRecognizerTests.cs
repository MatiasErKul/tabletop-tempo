using System;
using System.Collections.Generic;
using NUnit.Framework;
using TabletopTempo.Core.Input;
using TabletopTempo.Core.Recognizers;
using TabletopTempo.Core.Rhythm;
using UnityEngine;

namespace TabletopTempo.Tests
{
    /// <summary>Clap detection from scripted hand trajectories (60 Hz, like hand tracking).</summary>
    public class ClapRecognizerTests
    {
        private const double Hz = 60.0;
        private static readonly Vector3 Center = new Vector3(0f, 0.25f, 0.25f);

        /// <summary>Runs <paramref name="distanceAt"/> (palm-centre gap over time) through the recognizer.</summary>
        private static List<MoveEvent> Run(Func<double, float> distanceAt, double seconds,
            Func<double, bool> tracked = null, bool palmsDown = false)
        {
            var rec = new ClapRecognizer();
            var events = new List<MoveEvent>();
            rec.Detected += events.Add;
            var f = new HandFrame();
            int frames = (int)(seconds * Hz);
            for (int i = 0; i < frames; i++)
            {
                double t = 10.0 + i / Hz;
                float d = distanceAt(t - 10.0);
                bool on = tracked == null || tracked(t - 10.0);
                f.Time = t;
                if (on)
                {
                    var rotL = palmsDown ? SyntheticHand.PalmDown : SyntheticHand.PalmFacingInward(HandSide.Left);
                    var rotR = palmsDown ? SyntheticHand.PalmDown : SyntheticHand.PalmFacingInward(HandSide.Right);
                    SyntheticHand.Pose(f.Left, HandSide.Left, Center + new Vector3(-d / 2f, 0, 0), rotL, 0.1f);
                    SyntheticHand.Pose(f.Right, HandSide.Right, Center + new Vector3(d / 2f, 0, 0), rotR, 0.1f);
                    HandShape.Compute(f.Left, HandShapeTuning.Default);
                    HandShape.Compute(f.Right, HandShapeTuning.Default);
                }
                else
                {
                    f.Left.SetUntracked();
                    f.Right.SetUntracked();
                }
                rec.Process(f);
            }
            for (int i = 0; i < events.Count; i++)
                events[i] = new MoveEvent(events[i].Move, events[i].Time - 10.0, events[i].Confidence);
            return events;
        }

        /// <summary>Approach at <paramref name="speed"/> m/s from 0.30 m, touch (0.03 m), bounce back.</summary>
        private static Func<double, float> SingleClap(float speed) => t =>
        {
            float d = 0.30f - speed * (float)t;
            if (d >= 0.03f) return d;
            float back = 0.03f + (0.03f - d); // reflect: separate at the same speed
            return Mathf.Min(back, 0.30f);
        };

        [Test]
        public void SingleClap_DetectedAtContactCrossing()
        {
            var ev = Run(SingleClap(1.0f), 1.0);
            Assert.AreEqual(1, ev.Count, "one clap");
            Assert.AreEqual(MoveId.Clap, ev[0].Move);
            double expected = (0.30 - ClapTuning.Default.touchDistance) / 1.0;
            Assert.AreEqual(expected, ev[0].Time, 0.008, "predicted touch time");
            Assert.Greater(ev[0].Confidence, 0.8f);
        }

        [Test]
        public void SlowlyBringingHandsTogether_IsNotAClap()
        {
            Assert.IsEmpty(Run(SingleClap(0.08f), 4.0));
        }

        [Test]
        public void HandsResting_Together_OnlyOneClap()
        {
            // Clap, then keep the hands together for 2 s.
            var ev = Run(t => Mathf.Max(0.03f, 0.30f - 1.0f * (float)t), 2.5);
            Assert.AreEqual(1, ev.Count);
        }

        [Test]
        public void EightClapsAt80Bpm_AllDetected_OnTime()
        {
            const double period = 0.75;
            // Hands meet at t = 0.375 + k * 0.75, moving at constant speed between 0.03 and 0.28 m
            // (claps hit at speed; they don't slow to a stop before touching).
            Func<double, float> d = t =>
            {
                double phase = ((t - 0.375) % period + period) % period / period; // 0 at each meeting
                return 0.03f + 0.25f * (float)(1.0 - Math.Abs(2.0 * phase - 1.0));
            };
            var ev = Run(d, 8 * period);
            Assert.AreEqual(8, ev.Count, "eight claps");
            for (int k = 0; k < 8; k++)
            {
                double meet = 0.375 + k * period;
                Assert.AreEqual(meet, ev[k].Time, 0.02, $"clap {k}");
            }
        }

        [Test]
        public void TrackingLostRightBeforeContact_IsPredicted()
        {
            // Fast approach at 1.2 m/s; tracking drops below 9 cm (self-occlusion) and returns 0.2 s later.
            var clap = SingleClap(1.2f);
            double lostAt = (0.30 - 0.09) / 1.2;
            var ev = Run(clap, 1.2, t => !(t > lostAt && t < lostAt + 0.2));
            Assert.AreEqual(1, ev.Count, "clap predicted through the dropout");
            double expected = (0.30 - ClapTuning.Default.touchDistance) / 1.2;
            Assert.AreEqual(expected, ev[0].Time, 0.03);
            Assert.Less(ev[0].Confidence, 0.8f, "predicted claps carry lower confidence");
        }

        [Test]
        public void TrackingLostAndNotBack_StillPredicted()
        {
            var clap = SingleClap(1.0f);
            double lostAt = (0.30 - 0.10) / 1.0;
            var ev = Run(clap, 1.0, t => t <= lostAt);
            Assert.AreEqual(1, ev.Count);
        }

        [Test]
        public void SlowHandsThenDropout_IsNotAClap()
        {
            var ev = Run(SingleClap(0.1f), 3.0, t => t < 2.0);
            Assert.IsEmpty(ev);
        }

        [Test]
        public void PalmsFacingSameWay_Bumping_IsRejected()
        {
            Assert.IsEmpty(Run(SingleClap(1.0f), 1.0, palmsDown: true));
        }
    }
}
