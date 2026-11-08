namespace TabletopTempo.Core.Input
{
    /// <summary>
    /// Fixed-size ring buffer of recent hand frames (no allocation after construction).
    /// Recognizers read motion (velocity, approach speed) from here.
    /// </summary>
    public sealed class HandFrameHistory
    {
        private readonly HandFrame[] _frames;
        private int _head = -1;
        private int _count;

        public HandFrameHistory(int capacity = 90)
        {
            _frames = new HandFrame[capacity];
            for (int i = 0; i < capacity; i++) _frames[i] = new HandFrame();
        }

        public int Count => _count;
        public int Capacity => _frames.Length;

        /// <summary>Most recent frame, or null if empty.</summary>
        public HandFrame Latest => _count == 0 ? null : _frames[_head];

        /// <summary>Frame <paramref name="age"/> steps back (0 = latest), or null.</summary>
        public HandFrame Get(int age)
        {
            if (age < 0 || age >= _count) return null;
            int i = (_head - age) % _frames.Length;
            if (i < 0) i += _frames.Length;
            return _frames[i];
        }

        /// <summary>Copies <paramref name="frame"/> in and computes palm velocities against the previous frame.</summary>
        public void Push(HandFrame frame)
        {
            HandFrame prev = Latest;
            _head = (_head + 1) % _frames.Length;
            if (_count < _frames.Length) _count++;
            var slot = _frames[_head];
            slot.CopyFrom(frame);

            if (prev != null)
            {
                double dt = slot.Time - prev.Time;
                ComputeVelocity(slot.Left, prev.Left, dt);
                ComputeVelocity(slot.Right, prev.Right, dt);
            }
        }

        /// <summary>Most recent frame at or before <paramref name="time"/>, or null.</summary>
        public HandFrame AtOrBefore(double time)
        {
            for (int age = 0; age < _count; age++)
            {
                var f = Get(age);
                if (f.Time <= time) return f;
            }
            return null;
        }

        public void Clear()
        {
            _head = -1;
            _count = 0;
        }

        private static void ComputeVelocity(HandData now, HandData before, double dt)
        {
            if (!now.Tracked || !before.Tracked || dt <= 1e-5)
            {
                now.PalmVelocity = UnityEngine.Vector3.zero;
                return;
            }
            now.PalmVelocity = (now.PalmCenter - before.PalmCenter) / (float)dt;
        }
    }
}
