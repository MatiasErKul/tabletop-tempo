using System;
using System.Collections.Generic;

namespace TabletopTempo.Core.Rhythm
{
    /// <summary>Piecewise-constant tempo (same model as tools/music/ttmusic.py).</summary>
    public sealed class TempoMap
    {
        private readonly double[] _beats;
        private readonly double[] _bpm;
        private readonly double[] _startSeconds;

        public TempoMap(IList<(double beat, double bpm)> points)
        {
            if (points == null || points.Count == 0) throw new ArgumentException("TempoMap needs at least one point");
            var sorted = new List<(double beat, double bpm)>(points);
            sorted.Sort((a, b) => a.beat.CompareTo(b.beat));
            _beats = new double[sorted.Count];
            _bpm = new double[sorted.Count];
            _startSeconds = new double[sorted.Count];
            double t = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].bpm <= 0) throw new ArgumentException("bpm must be > 0");
                if (i > 0) t += (sorted[i].beat - sorted[i - 1].beat) * 60.0 / sorted[i - 1].bpm;
                _beats[i] = sorted[i].beat;
                _bpm[i] = sorted[i].bpm;
                _startSeconds[i] = t;
            }
        }

        public static TempoMap Constant(double bpm) => new TempoMap(new[] { (0.0, bpm) });

        public double BeatToSeconds(double beat)
        {
            int i = SegmentForBeat(beat);
            return _startSeconds[i] + (beat - _beats[i]) * 60.0 / _bpm[i];
        }

        public double SecondsToBeat(double seconds)
        {
            int i = 0;
            for (int k = 0; k < _startSeconds.Length; k++)
                if (seconds >= _startSeconds[k]) i = k;
            return _beats[i] + (seconds - _startSeconds[i]) * _bpm[i] / 60.0;
        }

        public double BpmAtBeat(double beat) => _bpm[SegmentForBeat(beat)];

        /// <summary>Seconds per beat at <paramref name="beat"/>.</summary>
        public double BeatLength(double beat) => 60.0 / BpmAtBeat(beat);

        private int SegmentForBeat(double beat)
        {
            int i = 0;
            for (int k = 0; k < _beats.Length; k++)
                if (beat >= _beats[k]) i = k;
            return i;
        }
    }
}
