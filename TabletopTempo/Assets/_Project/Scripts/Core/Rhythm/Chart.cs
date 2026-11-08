using System;
using System.Collections.Generic;
using UnityEngine;

namespace TabletopTempo.Core.Rhythm
{
    /// <summary>JSON shape of a chart file (written by tools/music/ttmusic.py). See docs/MUSIC.md.</summary>
    [Serializable]
    public sealed class ChartData
    {
        public string id;
        public string title;
        public string audio;
        public float offsetSeconds;
        public int beatsPerBar = 4;
        public TempoPoint[] tempo;
        public float lengthBeats;
        public SectionData[] sections;
        public NoteData[] notes;

        [Serializable]
        public struct TempoPoint
        {
            public float beat;
            public float bpm;
        }

        [Serializable]
        public sealed class SectionData
        {
            public string name;
            public float startBeat;
            public int bars;
            public float bpm;
            public string phase;
            public string coach;
        }

        [Serializable]
        public sealed class NoteData
        {
            public float beat;
            public string move;
            public string hand;
            public float duration;
        }
    }

    /// <summary>A note ready for gameplay: beat, absolute song time, move, lane.</summary>
    public readonly struct ChartNote
    {
        public readonly int Index;
        public readonly double Beat;
        public readonly double Time;      // song seconds (beat 0 = 0)
        public readonly MoveId Move;
        public readonly Lane Lane;
        public readonly double DurationBeats;

        public ChartNote(int index, double beat, double time, MoveId move, Lane lane, double durationBeats)
        {
            Index = index;
            Beat = beat;
            Time = time;
            Move = move;
            Lane = lane;
            DurationBeats = durationBeats;
        }
    }

    /// <summary>Parsed, validated chart with a tempo map.</summary>
    public sealed class Chart
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public float OffsetSeconds { get; private set; }
        public int BeatsPerBar { get; private set; }
        public TempoMap Tempo { get; private set; }
        public double LengthBeats { get; private set; }
        public IReadOnlyList<ChartNote> Notes => _notes;
        public IReadOnlyList<ChartData.SectionData> Sections => _sections;

        private readonly List<ChartNote> _notes = new List<ChartNote>();
        private readonly List<ChartData.SectionData> _sections = new List<ChartData.SectionData>();

        public double LengthSeconds => Tempo.BeatToSeconds(LengthBeats);

        public static Chart FromJson(string json) => FromData(JsonUtility.FromJson<ChartData>(json));

        public static Chart FromData(ChartData d)
        {
            if (d == null) throw new FormatException("Chart: empty");
            if (d.tempo == null || d.tempo.Length == 0) throw new FormatException($"Chart '{d.id}': no tempo");
            var c = new Chart
            {
                Id = d.id,
                Title = d.title,
                OffsetSeconds = d.offsetSeconds,
                BeatsPerBar = d.beatsPerBar > 0 ? d.beatsPerBar : 4,
                LengthBeats = d.lengthBeats,
            };
            var pts = new List<(double, double)>();
            foreach (var p in d.tempo) pts.Add((p.beat, p.bpm));
            c.Tempo = new TempoMap(pts);
            if (d.sections != null) c._sections.AddRange(d.sections);
            if (d.notes != null)
            {
                var sorted = new List<ChartData.NoteData>(d.notes);
                sorted.Sort((a, b) => a.beat.CompareTo(b.beat));
                for (int i = 0; i < sorted.Count; i++)
                {
                    var n = sorted[i];
                    var move = Moves.Parse(n.move);
                    c._notes.Add(new ChartNote(i, n.beat, c.Tempo.BeatToSeconds(n.beat), move, Moves.LaneOf(move), n.duration));
                }
            }
            return c;
        }

        /// <summary>Section active at <paramref name="beat"/>, or null.</summary>
        public ChartData.SectionData SectionAt(double beat)
        {
            ChartData.SectionData cur = null;
            foreach (var s in _sections)
                if (beat >= s.startBeat) cur = s;
            return cur;
        }

        /// <summary>
        /// Editor validation: impossible or unfair sequences. Returns human-readable warnings.
        /// (Cup-holder rules are added with the cup stages.)
        /// </summary>
        public List<string> Validate(double minGapSeconds = 0.25)
        {
            var warnings = new List<string>();
            for (int i = 1; i < _notes.Count; i++)
            {
                var a = _notes[i - 1];
                var b = _notes[i];
                double gap = b.Time - a.Time;
                if (gap < 1e-6 && a.Lane == b.Lane)
                    warnings.Add($"beat {b.Beat}: two notes at the same time in lane {b.Lane}");
                else if (gap < minGapSeconds && a.Move == b.Move)
                    warnings.Add($"beat {b.Beat}: {Moves.ToId(b.Move)} only {gap * 1000:0} ms after the previous one (tracking can't separate it)");
            }
            if (_notes.Count > 0 && _notes[_notes.Count - 1].Beat > LengthBeats)
                warnings.Add("last note is after the end of the chart");
            return warnings;
        }
    }
}
