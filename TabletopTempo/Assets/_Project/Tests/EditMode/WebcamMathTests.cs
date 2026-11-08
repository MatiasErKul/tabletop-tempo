using NUnit.Framework;
using TabletopTempo.Core.Input;
using TabletopTempo.Core.Webcam;
using UnityEngine;

namespace TabletopTempo.Tests
{
    /// <summary>
    /// Webcam → table mapping against a simulated pinhole camera, so it is tested without MediaPipe
    /// or a real webcam.
    /// </summary>
    public class WebcamMathTests
    {
        /// <summary>Pinhole camera above the table looking straight down. Image top = far side (+Z), right = +X.</summary>
        private sealed class OverheadCam
        {
            public Vector3 Pos = new Vector3(0.02f, 0.50f, 0.22f);
            public float F = 1.0f;          // focal length in image heights (~53° vertical FOV)
            public float Aspect = 4f / 3f;

            public Vector2 Project(Vector3 p)
            {
                float depth = Pos.y - p.y;
                return new Vector2(Aspect * 0.5f + F * (p.x - Pos.x) / depth, 0.5f - F * (p.z - Pos.z) / depth);
            }

            // Camera axes: x = image right = +X, y = image down = −Z, z = away from camera = −Y.
            public Vector3 ToCamAxes(Vector3 d) => new Vector3(d.x, -d.z, -d.y);
        }

        /// <param name="worldScale">Simulates MediaPipe mis-estimating hand size (its metric landmarks scaled).</param>
        private static WebcamHandObservation Observe(OverheadCam cam, HandData h, float worldScale = 1f)
        {
            var o = new WebcamHandObservation { Present = true, LabelLeft = h.Side == HandSide.Left, Score = 0.9f };
            Vector3 origin = h.Joints[0];
            for (int i = 0; i < HandData.JointCount; i++)
            {
                o.Image[i] = cam.Project(h.Joints[i]);
                o.World[i] = cam.ToCamAxes(h.Joints[i] - origin) * worldScale;
            }
            return o;
        }

        private static HandData Hand(HandSide side, Vector3 palm, Quaternion rot, float curl = 0f)
        {
            var h = new HandData(side);
            SyntheticHand.Pose(h, side, palm, rot, curl);
            return h;
        }

        /// <summary>Calibrates like a player: fingertip on each target, palm resting referenceHeight above the table.</summary>
        private static WebcamCalibration Calibrate(OverheadCam cam, float worldScale = 1f)
        {
            var cal = new WebcamCalibration
            {
                mount = WebcamMount.Overhead,
                cameraDistance = cam.Pos.y,
                aspect = cam.Aspect,
                swapHandedness = false,
            };
            var targets = WebcamCalibration.Targets(WebcamMount.Overhead);
            var img = new Vector2[3];
            var hands = new WebcamHandObservation[3];
            for (int i = 0; i < 3; i++)
            {
                img[i] = cam.Project(targets[i]); // fingertip touching the target
                var h = Hand(HandSide.Right, targets[i] + new Vector3(0f, cal.referenceHeight, -0.09f), SyntheticHand.PalmDown);
                hands[i] = Observe(cam, h, worldScale);
            }
            Assert.IsTrue(cal.Solve(img, targets, hands), "calibration should solve");
            return cal;
        }

        [Test]
        public void Affine_MapsTablePointsBack()
        {
            var cam = new OverheadCam();
            var cal = Calibrate(cam);
            foreach (var p in new[] { new Vector3(0.1f, 0, 0.3f), new Vector3(-0.15f, 0, 0.05f), new Vector3(0.0f, 0, 0.2f) })
            {
                Vector3 back = cal.ImageToTableOnPlane(cam.Project(p));
                Assert.Less(Vector3.Distance(back, p), 0.001f, $"{p} → {back}");
            }
            Assert.Less(Vector3.Distance(cal.CameraPosition, cam.Pos), 0.002f, "camera position from calibration");
        }

        [Test]
        public void Collinear_CalibrationIsRejected()
        {
            var cal = new WebcamCalibration();
            var img = new[] { new Vector2(0.1f, 0.1f), new Vector2(0.2f, 0.2f), new Vector2(0.3f, 0.3f) };
            Assert.IsFalse(cal.Solve(img, WebcamCalibration.Targets(WebcamMount.Overhead), null));
            Assert.IsFalse(cal.valid);
        }

        [TestCase(0.025f)]
        [TestCase(0.10f)]
        [TestCase(0.25f)]
        public void FlatHand_HeightAndJointsRecovered(float palmHeight)
        {
            var cam = new OverheadCam();
            var cal = Calibrate(cam);
            var truth = Hand(HandSide.Right, new Vector3(0.05f, palmHeight, 0.18f), SyntheticHand.PalmDown);
            var mapped = new HandData(HandSide.Right);
            WebcamHandMapper.Map(Observe(cam, truth), cal, mapped);

            HandShape.Compute(truth, HandShapeTuning.Default);
            HandShape.Compute(mapped, HandShapeTuning.Default);
            Assert.AreEqual(palmHeight, mapped.PalmCenter.y, 0.01f, "palm height");
            Assert.Less(Vector3.Distance(mapped.PalmCenter, truth.PalmCenter), 0.01f, "palm position");
            for (int i = 0; i < HandData.JointCount; i++)
                Assert.Less(Vector3.Distance(mapped.Joints[i], truth.Joints[i]), 0.015f, $"joint {(HandJoint)i}");
        }

        [Test]
        public void TiltedHand_ForeshorteningCorrected()
        {
            var cam = new OverheadCam();
            var cal = Calibrate(cam);
            // Fingers tilted 50° up toward the camera: naive 2D size would read as much farther away.
            var truth = Hand(HandSide.Left, new Vector3(-0.05f, 0.12f, 0.20f), Quaternion.Euler(-50f, 0f, 0f));
            var mapped = new HandData(HandSide.Left);
            WebcamHandMapper.Map(Observe(cam, truth), cal, mapped);
            HandShape.Compute(truth, HandShapeTuning.Default);
            HandShape.Compute(mapped, HandShapeTuning.Default);
            Assert.AreEqual(truth.PalmCenter.y, mapped.PalmCenter.y, 0.01f);
            Assert.Less(Vector3.Distance(truth.PalmCenter, mapped.PalmCenter), 0.015f);
        }

        [Test]
        public void HandScale_LearnedAtCalibration_CorrectsMediaPipeSizeError()
        {
            // MediaPipe thinks the hand is 12% smaller than it is: raw depths would be ~12% short.
            const float mpScale = 0.88f;
            var cam = new OverheadCam();
            var cal = Calibrate(cam, mpScale);
            Assert.AreEqual(1f / mpScale, cal.handScale, 0.03f, "hand scale");

            var truth = Hand(HandSide.Right, new Vector3(0.08f, 0.15f, 0.25f), SyntheticHand.PalmDown);
            var mapped = new HandData(HandSide.Right);
            WebcamHandMapper.Map(Observe(cam, truth, mpScale), cal, mapped);
            HandShape.Compute(truth, HandShapeTuning.Default);
            HandShape.Compute(mapped, HandShapeTuning.Default);
            Assert.AreEqual(truth.PalmCenter.y, mapped.PalmCenter.y, 0.015f);
        }

        [Test]
        public void Calibration_FocalLengthRecovered()
        {
            var cam = new OverheadCam();
            Assert.AreEqual(cam.F, Calibrate(cam).focal, 0.01f);
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void MappedFlatHand_KeepsPalmNormalAndFlatness(HandSide side)
        {
            // Guards the camera-axis → table-axis rotation (including chirality).
            var cam = new OverheadCam();
            var cal = Calibrate(cam);
            var mapped = new HandData(side);
            WebcamHandMapper.Map(Observe(cam, Hand(side, new Vector3(0f, 0.04f, 0.2f), SyntheticHand.PalmDown)), cal, mapped);
            HandShape.Compute(mapped, HandShapeTuning.Default);
            Assert.Less(Vector3.Angle(mapped.PalmNormal, Vector3.down), 15f, $"palm normal {mapped.PalmNormal}");
            Assert.Greater(mapped.FlatScore, 0.7f);
        }

        [Test]
        public void MappedFist_IsFist()
        {
            var cam = new OverheadCam();
            var cal = Calibrate(cam);
            var mapped = new HandData(HandSide.Right);
            WebcamHandMapper.Map(Observe(cam, Hand(HandSide.Right, new Vector3(0f, 0.06f, 0.2f), SyntheticHand.PalmDown, 1f)), cal, mapped);
            HandShape.Compute(mapped, HandShapeTuning.Default);
            Assert.Greater(mapped.FistScore, 0.8f);
        }

        [Test]
        public void HandednessSwap()
        {
            var obs = new WebcamHandObservation { Present = true, LabelLeft = true };
            Assert.AreEqual(HandSide.Left, WebcamHandMapper.SideOf(obs, new WebcamCalibration { swapHandedness = false }));
            Assert.AreEqual(HandSide.Right, WebcamHandMapper.SideOf(obs, new WebcamCalibration { swapHandedness = true }));
        }

        [Test]
        public void Absent_IsUntracked()
        {
            var h = new HandData(HandSide.Left) { Tracked = true };
            WebcamHandMapper.Map(new WebcamHandObservation { Present = false }, new WebcamCalibration(), h);
            Assert.IsFalse(h.Tracked);
        }

        [Test]
        public void CalibrationJson_RoundTrip()
        {
            var cal = Calibrate(new OverheadCam());
            var back = WebcamCalibration.FromJson(cal.ToJson());
            Assert.AreEqual(cal.m01, back.m01, 1e-6f);
            Assert.AreEqual(cal.focal, back.focal, 1e-6f);
            Assert.AreEqual(cal.handScale, back.handScale, 1e-6f);
            Assert.IsTrue(back.valid);
        }

        [Test]
        public void OneEuro_SmoothsJitter_ButFollowsMotion()
        {
            var f = new OneEuroFilter(1.5f, 4f);
            var rng = new System.Random(7);
            float maxDev = 0f;
            for (int i = 0; i < 120; i++)
            {
                float noisy = 1f + (float)(rng.NextDouble() - 0.5) * 0.01f; // ±5 mm jitter
                float y = f.Filter(noisy, i / 30.0);
                if (i > 30) maxDev = Mathf.Max(maxDev, Mathf.Abs(y - 1f));
            }
            Assert.Less(maxDev, 0.004f, "jitter reduced");

            // Fast move of 10 cm in one frame: must get most of the way within ~3 frames (low lag).
            float last = 0f;
            for (int i = 120; i < 124; i++) last = f.Filter(1.1f, i / 30.0);
            Assert.Greater(last, 1.07f, $"follows fast motion (got {last})");
        }
    }
}
