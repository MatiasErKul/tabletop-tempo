using System;
using TabletopTempo.Core.Input;
using TabletopTempo.Core.Rhythm;
using UnityEngine;

namespace TabletopTempo.Core.Recognizers
{
    [Serializable]
    public struct ClapTuning
    {
        [Tooltip("Palm-centre distance (m) when the palms physically touch (joints sit inside the hand, so not 0).")]
        public float touchDistance;
        [Tooltip("Palm-centre distance (m) at which a clap is detected; the touch moment is extrapolated from here.")]
        public float triggerDistance;
        [Tooltip("Hands must separate beyond this (m) before the next clap can register.")]
        public float rearmDistance;
        [Tooltip("Minimum closing speed (m/s) shortly before contact. Rejects slowly resting the hands together.")]
        public float minClosingSpeed;
        [Tooltip("If tracking drops while hands close fast within this distance (m), predict the contact.")]
        public float occlusionDistance;
        [Tooltip("Longest gap (s) between losing tracking and the predicted contact that is still trusted.")]
        public float occlusionMaxPredict;
        [Tooltip("Minimum time between two claps (s).")]
        public float cooldown;
        [Tooltip("Reject if palms face the same way (dot of palm normals above this). 1 = never reject.")]
        public float maxPalmNormalDot;

        public static ClapTuning Default => new ClapTuning
        {
            touchDistance = 0.03f,
            triggerDistance = 0.06f,
            rearmDistance = 0.14f,
            minClosingSpeed = 0.35f,
            occlusionDistance = 0.16f,
            occlusionMaxPredict = 0.15f,
            cooldown = 0.18f,
            maxPalmNormalDot = 0.6f,
        };
    }

    /// <summary>
    /// Detects claps from a stream of hand frames (table space, DSP-clock times).
    ///
    /// Hand tracking often drops exactly when the palms meet (self-occlusion), so the event time is
    /// the PREDICTED touch moment: the interpolated crossing of <see cref="ClapTuning.triggerDistance"/>
    /// plus the time to close the remaining gap to <see cref="ClapTuning.touchDistance"/> at the current
    /// speed, or — if tracking is lost while the hands close fast — extrapolated from the last approach. Each clap needs the hands to separate again (re-arm) before the next one.
    /// Plain C#, no Unity objects: driven by tests and by the runtime the same way.
    /// </summary>
    public sealed class ClapRecognizer
    {
        public ClapTuning Tuning;

        private bool _armed = true;
        private bool _havePrev;
        private double _prevTime;
        private float _prevDist;
        private float _closingSpeed;      // smoothed, m/s, positive = approaching
        private double _lastEvent = double.NegativeInfinity;
        private bool _pendingOcclusion;
        private double _occlusionLostAt;
        private float _occlusionDist, _occlusionSpeed;

        public ClapRecognizer(ClapTuning tuning) => Tuning = tuning;
        public ClapRecognizer() : this(ClapTuning.Default) { }

        public event Action<MoveEvent> Detected;

        public void Reset()
        {
            _armed = true;
            _havePrev = false;
            _pendingOcclusion = false;
            _closingSpeed = 0f;
            _lastEvent = double.NegativeInfinity;
        }

        /// <summary>Feed one processed frame (after HandShape.Compute).</summary>
        public void Process(HandFrame f)
        {
            bool both = f.Left.Tracked && f.Right.Tracked;
            if (!both)
            {
                OnLost(f.Time);
                return;
            }

            float d = Vector3.Distance(f.Left.PalmCenter, f.Right.PalmCenter);

            if (_pendingOcclusion)
            {
                // Tracking came back. If the hands are now together or separating from contact,
                // the predicted clap happened while we were blind.
                ResolveOcclusion(f.Time, d);
            }

            if (_havePrev)
            {
                double dt = f.Time - _prevTime;
                if (dt > 1e-4 && dt < 0.25)
                {
                    float v = (_prevDist - d) / (float)dt;
                    _closingSpeed = Mathf.Lerp(_closingSpeed, v, 0.6f);

                    if (_armed && _prevDist > Tuning.triggerDistance && d <= Tuning.triggerDistance && _closingSpeed >= Tuning.minClosingSpeed)
                    {
                        // Interpolate the trigger crossing, then extrapolate to the physical touch.
                        double frac = (_prevDist - Tuning.triggerDistance) / Math.Max(1e-5, _prevDist - d);
                        double tCross = _prevTime + frac * dt;
                        double tContact = tCross + (Tuning.triggerDistance - Tuning.touchDistance) / Math.Max(0.05, _closingSpeed);
                        float conf = PalmsOpposed(f) ? 1f : 0.7f;
                        if (PalmsAllowed(f)) Emit(tContact, conf * Mathf.Min(f.Left.Confidence, f.Right.Confidence));
                    }
                }
                else
                {
                    _closingSpeed = 0f;
                }
            }

            if (!_armed && d > Tuning.rearmDistance) _armed = true;

            _havePrev = true;
            _prevTime = f.Time;
            _prevDist = d;
        }

        private void OnLost(double now)
        {
            if (_havePrev && _armed && !_pendingOcclusion &&
                _prevDist <= Tuning.occlusionDistance && _prevDist > Tuning.touchDistance &&
                _closingSpeed >= Tuning.minClosingSpeed)
            {
                _pendingOcclusion = true;
                _occlusionLostAt = _prevTime;
                _occlusionDist = _prevDist;
                _occlusionSpeed = _closingSpeed;
            }

            if (_pendingOcclusion)
            {
                double predicted = PredictedContact();
                // Still blind well past the predicted contact: accept it (classic clap occlusion).
                if (now - predicted > 0.05)
                {
                    if (predicted - _occlusionLostAt <= Tuning.occlusionMaxPredict) Emit(predicted, 0.6f);
                    _pendingOcclusion = false;
                }
            }
            _havePrev = false;
        }

        private void ResolveOcclusion(double now, float d)
        {
            double predicted = PredictedContact();
            _pendingOcclusion = false;
            if (predicted - _occlusionLostAt > Tuning.occlusionMaxPredict) return;
            // Back after the predicted touch (hands may already be apart again), or back already
            // inside the trigger distance: the clap happened while tracking was blind.
            if (now >= predicted - 0.03 || d <= Tuning.triggerDistance) Emit(predicted, 0.7f);
        }

        private double PredictedContact() =>
            _occlusionLostAt + (_occlusionDist - Tuning.touchDistance) / Math.Max(0.05, _occlusionSpeed);

        private bool PalmsAllowed(HandFrame f) =>
            Vector3.Dot(f.Left.PalmNormal, f.Right.PalmNormal) <= Tuning.maxPalmNormalDot;

        private static bool PalmsOpposed(HandFrame f) =>
            Vector3.Dot(f.Left.PalmNormal, f.Right.PalmNormal) < -0.3f;

        private void Emit(double time, float confidence)
        {
            if (time - _lastEvent < Tuning.cooldown) return;
            _lastEvent = time;
            _armed = false;
            Detected?.Invoke(new MoveEvent(MoveId.Clap, time, Mathf.Clamp01(confidence)));
        }
    }
}
