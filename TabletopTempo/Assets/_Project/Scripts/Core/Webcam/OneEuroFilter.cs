using UnityEngine;

namespace TabletopTempo.Core.Webcam
{
    /// <summary>
    /// One-Euro filter (Casiez et al. 2012): smooths jitter when still, follows quickly when moving.
    /// Low latency matters here (rhythm judging), so defaults lean responsive.
    /// </summary>
    public struct OneEuroFilter
    {
        public float MinCutoff;   // Hz: lower = smoother when still
        public float Beta;        // speed coefficient: higher = less lag when fast
        public float DerivativeCutoff;

        private bool _init;
        private float _x, _dx;
        private double _t;

        // beta is in 1/m for positions in metres: hand speeds of 1–3 m/s raise the cutoff by 4–12 Hz.
        public OneEuroFilter(float minCutoff = 1.5f, float beta = 4.0f, float derivativeCutoff = 1.0f)
        {
            MinCutoff = minCutoff;
            Beta = beta;
            DerivativeCutoff = derivativeCutoff;
            _init = false;
            _x = _dx = 0f;
            _t = 0;
        }

        public void Reset() => _init = false;

        public float Filter(float value, double time)
        {
            if (!_init)
            {
                _init = true;
                _x = value;
                _dx = 0f;
                _t = time;
                return value;
            }
            float dt = (float)(time - _t);
            if (dt <= 1e-6f) return _x;
            _t = time;

            float dx = (value - _x) / dt;
            _dx = Mathf.Lerp(_dx, dx, Alpha(DerivativeCutoff, dt));
            float cutoff = MinCutoff + Beta * Mathf.Abs(_dx);
            _x = Mathf.Lerp(_x, value, Alpha(cutoff, dt));
            return _x;
        }

        private static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }

    /// <summary>Three One-Euro filters for a Vector3.</summary>
    public struct OneEuroFilter3
    {
        private OneEuroFilter _x, _y, _z;

        public OneEuroFilter3(float minCutoff, float beta)
        {
            _x = new OneEuroFilter(minCutoff, beta);
            _y = new OneEuroFilter(minCutoff, beta);
            _z = new OneEuroFilter(minCutoff, beta);
        }

        public Vector3 Filter(Vector3 v, double t) => new Vector3(_x.Filter(v.x, t), _y.Filter(v.y, t), _z.Filter(v.z, t));

        public void Reset()
        {
            _x.Reset();
            _y.Reset();
            _z.Reset();
        }
    }
}
