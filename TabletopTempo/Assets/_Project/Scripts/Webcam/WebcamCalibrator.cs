using TabletopTempo.Core.Input;
using TabletopTempo.Core.Webcam;
using UnityEngine;

namespace TabletopTempo.Webcam
{
    /// <summary>
    /// Guided calibration (driven by the Webcam editor window or hotkeys):
    ///   0. "Hold up only your LEFT hand"  → fixes MediaPipe's left/right labels
    ///   1–3. "Touch target N with your index fingertip and hold still" → affine + size reference
    /// Each step captures automatically once the hand is stable for <see cref="HoldSeconds"/>.
    /// </summary>
    public sealed class WebcamCalibrator
    {
        public const float HoldSeconds = 0.8f;
        private const float StableImageRadius = 0.012f; // image heights

        private readonly WebcamMediaPipeProvider _provider;
        private WebcamCalibration _working;
        private readonly Vector2[] _image = new Vector2[3];
        private readonly WebcamHandObservation[] _hands = { new WebcamHandObservation(), new WebcamHandObservation(), new WebcamHandObservation() };
        private Vector2 _anchor;
        private float _stableSince = -1f;

        public int Step { get; private set; } = -1;   // -1 = idle, 0 = handedness, 1..3 = targets, 4 = done
        public bool Running => Step >= 0 && Step <= 3;
        public float HoldProgress { get; private set; }
        public string Message { get; private set; } = "Not calibrated.";

        public WebcamCalibrator(WebcamMediaPipeProvider provider)
        {
            _provider = provider;
            if (provider.Calibration != null && provider.Calibration.valid) Message = "Calibrated. Recalibrate if the camera moved.";
        }

        public void Begin(WebcamMount mount, float cameraDistance)
        {
            _working = new WebcamCalibration
            {
                mount = mount,
                cameraDistance = cameraDistance,
                referenceHeight = mount == WebcamMount.Overhead ? 0.025f : 0f,
                aspect = _provider.CameraTexture != null && _provider.CameraTexture.height > 0
                    ? _provider.CameraTexture.width / (float)_provider.CameraTexture.height : 4f / 3f,
            };
            Step = 0;
            _stableSince = -1f;
            Message = "Step 1/4: hold up only your LEFT hand, palm toward the camera.";
        }

        public void Cancel()
        {
            Step = -1;
            Message = "Calibration cancelled.";
        }

        public static string TargetDescription(WebcamMount mount, int i)
        {
            if (mount == WebcamMount.Overhead)
                return i switch
                {
                    0 => "the table about 20 cm LEFT of centre, near your edge",
                    1 => "the table about 20 cm RIGHT of centre, near your edge",
                    _ => "the table straight ahead, about 35 cm from your edge",
                };
            return i switch
            {
                0 => "the table about 20 cm LEFT of centre",
                1 => "the table about 20 cm RIGHT of centre",
                _ => "a point in the air 30 cm above the table centre",
            };
        }

        public void Tick(float now)
        {
            if (!Running) return;
            var obs = Single();
            if (obs == null)
            {
                _stableSince = -1f;
                HoldProgress = 0f;
                return;
            }

            Vector2 probe = Step == 0 ? obs.PalmCenterImage : obs.Image[(int)HandJoint.IndexTip];
            if (_stableSince < 0f || Vector2.Distance(probe, _anchor) > StableImageRadius)
            {
                _anchor = probe;
                _stableSince = now;
            }
            HoldProgress = Mathf.Clamp01((now - _stableSince) / HoldSeconds);
            if (HoldProgress < 1f) return;

            if (Step == 0)
            {
                // The user showed their LEFT hand: if MediaPipe called it Right, labels are swapped.
                _working.swapHandedness = !obs.LabelLeft;
                Advance();
                return;
            }

            int k = Step - 1;
            _image[k] = probe;
            _hands[k].CopyFrom(obs);
            Advance();
        }

        private void Advance()
        {
            Step++;
            _stableSince = -1f;
            HoldProgress = 0f;
            if (Step <= 3)
            {
                Message = $"Step {Step + 1}/4: touch {TargetDescription(_working.mount, Step - 1)} with your index fingertip and hold still.";
                return;
            }

            var targets = WebcamCalibration.Targets(_working.mount);
            if (_working.Solve(_image, targets, _hands))
            {
                _provider.SaveCalibration(_working);
                Message = $"Calibrated ({_working.mount}, swap L/R = {_working.swapHandedness}, focal {_working.focal:0.00}, hand scale {_working.handScale:0.00}).";
            }
            else
            {
                Message = "Calibration failed: the three points were in a line. Try again with points further apart.";
            }
            Step = 4;
        }

        /// <summary>The only visible hand, or null if none or two.</summary>
        private WebcamHandObservation Single()
        {
            WebcamHandObservation found = null;
            foreach (var o in _provider.Latest)
            {
                if (!o.Present) continue;
                if (found != null) return null;
                found = o;
            }
            return found;
        }
    }
}
