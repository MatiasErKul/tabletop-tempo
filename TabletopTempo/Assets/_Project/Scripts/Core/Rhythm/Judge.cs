using System;
using System.Collections.Generic;

namespace TabletopTempo.Core.Rhythm
{
    public enum Grade
    {
        Miss = 0,
        /// <summary>Wrong move at the right time: partial credit, so beginners are encouraged.</summary>
        Close = 1,
        Good = 2,
        Great = 3,
        Perfect = 4,
    }

    [Serializable]
    public struct JudgeWindows
    {
        public float perfectMs;
        public float greatMs;
        public float goodMs;

        /// <summary>Generous on purpose: hand tracking adds latency and jitter.</summary>
        public static JudgeWindows Standard => new JudgeWindows { perfectMs = 60, greatMs = 110, goodMs = 170 };

        /// <summary>Accessibility "Relaxed" judge.</summary>
        public static JudgeWindows Relaxed => new JudgeWindows { perfectMs = 90, greatMs = 160, goodMs = 250 };

        public Grade GradeFor(double absOffsetMs) =>
            absOffsetMs <= perfectMs ? Grade.Perfect :
            absOffsetMs <= greatMs ? Grade.Great :
            absOffsetMs <= goodMs ? Grade.Good : Grade.Miss;
    }

    public readonly struct Judgement
    {
        public readonly int NoteIndex;
        public readonly Grade Grade;
        /// <summary>Input time − note time, ms (negative = early). NaN for misses.</summary>
        public readonly double OffsetMs;
        public readonly MoveId Expected;
        public readonly MoveId Got;

        public Judgement(int noteIndex, Grade grade, double offsetMs, MoveId expected, MoveId got)
        {
            NoteIndex = noteIndex;
            Grade = grade;
            OffsetMs = offsetMs;
            Expected = expected;
            Got = got;
        }

        public override string ToString() => $"#{NoteIndex} {Grade} {(double.IsNaN(OffsetMs) ? "" : OffsetMs.ToString("+0;-0") + "ms")}";
    }

    /// <summary>
    /// Matches move events to chart notes. Times are song seconds. Rules:
    ///  * an event hits the earliest unjudged note of the SAME move within the Good window;
    ///  * otherwise, if a note of another move is within the Good window, that note is marked
    ///    "Close" (wrong move, right time) unless a correct hit arrives before its window ends;
    ///  * a note whose window passes unmatched is a Miss (or Close if a wrong move was seen);
    ///  * one event judges at most one note.
    /// Input latency (per input mode, from latency calibration) is subtracted from event times.
    /// </summary>
    public sealed class Judge
    {
        private readonly IReadOnlyList<ChartNote> _notes;
        private readonly bool[] _done;
        private readonly bool[] _closeCandidate;
        private readonly MoveId[] _closeMove;
        private int _firstOpen;

        public JudgeWindows Windows;
        public double LatencySeconds;

        public event Action<Judgement> Judged;

        public int JudgedCount { get; private set; }

        public Judge(IReadOnlyList<ChartNote> notes, JudgeWindows windows, double latencySeconds = 0)
        {
            _notes = notes;
            _done = new bool[notes.Count];
            _closeCandidate = new bool[notes.Count];
            _closeMove = new MoveId[notes.Count];
            Windows = windows;
            LatencySeconds = latencySeconds;
        }

        private double GoodSeconds => Windows.goodMs / 1000.0;

        /// <summary>Feeds one recognised move (time already converted to song seconds).</summary>
        public void OnMove(MoveId move, double songTime)
        {
            double t = songTime - LatencySeconds;
            int best = -1;
            double bestAbs = double.MaxValue;
            int wrong = -1;
            for (int i = _firstOpen; i < _notes.Count; i++)
            {
                if (_done[i]) continue;
                double off = t - _notes[i].Time;
                if (off < -GoodSeconds) break; // notes are sorted: the rest are later
                if (off > GoodSeconds) continue;
                if (_notes[i].Move == move)
                {
                    double a = Math.Abs(off);
                    if (a < bestAbs) { bestAbs = a; best = i; }
                }
                else if (wrong < 0) wrong = i;
            }

            if (best >= 0)
            {
                double offMs = (t - _notes[best].Time) * 1000.0;
                Complete(best, Windows.GradeFor(Math.Abs(offMs)), offMs, move);
            }
            else if (wrong >= 0 && !_closeCandidate[wrong])
            {
                _closeCandidate[wrong] = true;
                _closeMove[wrong] = move;
            }
        }

        /// <summary>Call every frame with the current song time: closes notes whose window has passed.</summary>
        public void Update(double songTime)
        {
            double t = songTime - LatencySeconds;
            for (int i = _firstOpen; i < _notes.Count; i++)
            {
                if (_done[i]) continue;
                if (t - _notes[i].Time <= GoodSeconds) break;
                if (_closeCandidate[i]) Complete(i, Grade.Close, double.NaN, _closeMove[i]);
                else Complete(i, Grade.Miss, double.NaN, MoveId.None);
            }
        }

        public bool IsDone(int noteIndex) => _done[noteIndex];

        private void Complete(int i, Grade g, double offsetMs, MoveId got)
        {
            _done[i] = true;
            JudgedCount++;
            while (_firstOpen < _notes.Count && _done[_firstOpen]) _firstOpen++;
            Judged?.Invoke(new Judgement(i, g, offsetMs, _notes[i].Move, got));
        }
    }

    /// <summary>Combo, accuracy and stars. A miss never ends the run.</summary>
    public sealed class Score
    {
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Notes { get; private set; }
        public int Points { get; private set; }
        public readonly int[] Counts = new int[5];

        public void Add(Grade g)
        {
            Notes++;
            Counts[(int)g]++;
            Points += PointsFor(g);
            if (g >= Grade.Good)
            {
                Combo++;
                if (Combo > BestCombo) BestCombo = Combo;
            }
            else Combo = 0;
        }

        public static int PointsFor(Grade g)
        {
            switch (g)
            {
                case Grade.Perfect: return 100;
                case Grade.Great: return 80;
                case Grade.Good: return 50;
                case Grade.Close: return 25;
                default: return 0;
            }
        }

        /// <summary>0..1</summary>
        public float Accuracy => Notes == 0 ? 0f : Points / (100f * Notes);

        /// <summary>0..3 stars.</summary>
        public int Stars => Accuracy >= 0.85f ? 3 : Accuracy >= 0.65f ? 2 : Accuracy >= 0.40f ? 1 : 0;
    }
}
