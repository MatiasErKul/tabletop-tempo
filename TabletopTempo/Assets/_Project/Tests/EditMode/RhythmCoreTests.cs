using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TabletopTempo.Core.Rhythm;

namespace TabletopTempo.Tests
{
    public class TempoMapTests
    {
        [Test]
        public void Constant_Tempo()
        {
            var t = TempoMap.Constant(120);
            Assert.AreEqual(0.5, t.BeatToSeconds(1), 1e-9);
            Assert.AreEqual(8.0, t.SecondsToBeat(4), 1e-9);
        }

        [Test]
        public void TempoChange_MatchesGenerator()
        {
            // stage1: 72 BPM for beats 0..20, then 80 BPM (same model as ttmusic.py)
            var t = new TempoMap(new[] { (0.0, 72.0), (20.0, 80.0) });
            Assert.AreEqual(20 * 60.0 / 72, t.BeatToSeconds(20), 1e-9);
            Assert.AreEqual(20 * 60.0 / 72 + 4 * 0.75, t.BeatToSeconds(24), 1e-9);
            Assert.AreEqual(24, t.SecondsToBeat(t.BeatToSeconds(24)), 1e-9);
            Assert.AreEqual(80, t.BpmAtBeat(30));
            Assert.AreEqual(60.0 / 72, t.BeatLength(3), 1e-9);
        }
    }

    public class ChartTests
    {
        private const string Json = @"{
          ""id"": ""t"", ""title"": ""Test"", ""audio"": ""Music/t"", ""offsetSeconds"": 0.1, ""beatsPerBar"": 4,
          ""tempo"": [{""beat"": 0, ""bpm"": 60}, {""beat"": 8, ""bpm"": 120}],
          ""lengthBeats"": 16,
          ""sections"": [{""name"": ""a"", ""startBeat"": 0, ""bars"": 2, ""bpm"": 60, ""phase"": ""play"", ""coach"": ""Clap!""},
                         {""name"": ""b"", ""startBeat"": 8, ""bars"": 2, ""bpm"": 120, ""phase"": ""play"", ""coach"": ""Faster""}],
          ""notes"": [{""beat"": 9, ""move"": ""TAP_L"", ""hand"": ""left""},
                      {""beat"": 4, ""move"": ""CLAP"", ""hand"": ""both""},
                      {""beat"": 10, ""move"": ""RAISE"", ""hand"": ""both"", ""duration"": 2}]
        }";

        [Test]
        public void Parse_SortsNotes_ComputesTimes_Lanes()
        {
            var c = Chart.FromJson(Json);
            Assert.AreEqual("t", c.Id);
            Assert.AreEqual(0.1f, c.OffsetSeconds, 1e-6);
            Assert.AreEqual(3, c.Notes.Count);
            Assert.AreEqual(MoveId.Clap, c.Notes[0].Move);
            Assert.AreEqual(4.0, c.Notes[0].Time, 1e-9, "beat 4 at 60 BPM");
            Assert.AreEqual(8.5, c.Notes[1].Time, 1e-9, "beat 9: 8 s + one 120-BPM beat");
            Assert.AreEqual(Lane.Left, c.Notes[1].Lane);
            Assert.AreEqual(Lane.Center, c.Notes[0].Lane);
            Assert.AreEqual(2.0, c.Notes[2].DurationBeats, 1e-9);
            Assert.AreEqual("Faster", c.SectionAt(9).coach);
        }

        [Test]
        public void UnknownMove_Throws()
        {
            Assert.Throws<System.FormatException>(() => Chart.FromJson(Json.Replace("TAP_L", "MOONWALK")));
        }

        [Test]
        public void Validate_FlagsTooFastRepeats()
        {
            var c = Chart.FromJson(Json.Replace(@"""beat"": 9, ""move"": ""TAP_L""", @"""beat"": 4.2, ""move"": ""CLAP"""));
            var w = c.Validate();
            Assert.IsTrue(w.Any(s => s.Contains("CLAP")), string.Join("\n", w));
        }

        [Test]
        public void Validate_CleanChart_HasNoWarnings()
        {
            Assert.IsEmpty(Chart.FromJson(Json).Validate());
        }
    }

    public class JudgeTests
    {
        private static List<ChartNote> Notes(params (double t, MoveId m)[] n) =>
            n.Select((x, i) => new ChartNote(i, x.t, x.t, x.m, Moves.LaneOf(x.m), 0)).ToList();

        private static (Judge judge, List<Judgement> got) Make(List<ChartNote> notes, double latency = 0)
        {
            var j = new Judge(notes, JudgeWindows.Standard, latency);
            var got = new List<Judgement>();
            j.Judged += got.Add;
            return (j, got);
        }

        [TestCase(0.000, Grade.Perfect)]
        [TestCase(0.055, Grade.Perfect)]
        [TestCase(-0.100, Grade.Great)]
        [TestCase(0.160, Grade.Good)]
        public void Windows(double offset, Grade expected)
        {
            var (j, got) = Make(Notes((1.0, MoveId.Clap)));
            j.OnMove(MoveId.Clap, 1.0 + offset);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(expected, got[0].Grade);
            Assert.AreEqual(offset * 1000, got[0].OffsetMs, 0.01);
        }

        [Test]
        public void OutsideWindow_IsIgnored_ThenMiss()
        {
            var (j, got) = Make(Notes((1.0, MoveId.Clap)));
            j.OnMove(MoveId.Clap, 1.5);
            Assert.IsEmpty(got, "far too late: not a hit");
            j.Update(1.0 + 0.171);
            Assert.AreEqual(Grade.Miss, got.Single().Grade);
        }

        [Test]
        public void WrongMove_RightTime_IsClose()
        {
            var (j, got) = Make(Notes((1.0, MoveId.PoundBoth)));
            j.OnMove(MoveId.Clap, 1.02);
            Assert.IsEmpty(got, "decided only when the window closes");
            j.Update(1.2);
            Assert.AreEqual(Grade.Close, got.Single().Grade);
            Assert.AreEqual(MoveId.Clap, got[0].Got);
        }

        [Test]
        public void WrongThenRight_CountsAsHit()
        {
            var (j, got) = Make(Notes((1.0, MoveId.PoundBoth)));
            j.OnMove(MoveId.Clap, 0.95);
            j.OnMove(MoveId.PoundBoth, 1.03);
            Assert.AreEqual(Grade.Perfect, got.Single().Grade);
        }

        [Test]
        public void OneEvent_JudgesOneNote_Nearest()
        {
            var (j, got) = Make(Notes((1.0, MoveId.Clap), (1.1, MoveId.Clap)));
            j.OnMove(MoveId.Clap, 1.09);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(1, got[0].NoteIndex, "nearest note wins");
            j.Update(1.5);
            Assert.AreEqual(Grade.Miss, got.Single(g => g.NoteIndex == 0).Grade);
        }

        [Test]
        public void Latency_IsSubtracted()
        {
            var (j, got) = Make(Notes((1.0, MoveId.Clap)), latency: 0.12);
            j.OnMove(MoveId.Clap, 1.12);
            Assert.AreEqual(Grade.Perfect, got.Single().Grade);
            Assert.AreEqual(0, got[0].OffsetMs, 0.01);
        }

        [Test]
        public void EveryNote_IsJudgedExactlyOnce()
        {
            var notes = Notes(Enumerable.Range(0, 20).Select(i => (i * 0.75, MoveId.Clap)).ToArray());
            var (j, got) = Make(notes);
            for (int i = 0; i < 20; i += 2) j.OnMove(MoveId.Clap, i * 0.75 + 0.03);
            j.Update(100);
            Assert.AreEqual(20, got.Count);
            CollectionAssert.AllItemsAreUnique(got.Select(g => g.NoteIndex));
            Assert.AreEqual(10, got.Count(g => g.Grade == Grade.Perfect));
            Assert.AreEqual(10, got.Count(g => g.Grade == Grade.Miss));
        }
    }

    public class ScoreTests
    {
        [Test]
        public void Combo_Accuracy_Stars()
        {
            var s = new Score();
            foreach (var g in new[] { Grade.Perfect, Grade.Perfect, Grade.Great, Grade.Miss, Grade.Good, Grade.Close })
                s.Add(g);
            Assert.AreEqual(3, s.BestCombo);
            Assert.AreEqual(0, s.Combo, "Close breaks the combo");
            Assert.AreEqual((100 + 100 + 80 + 0 + 50 + 25) / 600f, s.Accuracy, 1e-6);
            Assert.AreEqual(1, s.Stars);
        }

        [Test]
        public void AllPerfect_ThreeStars()
        {
            var s = new Score();
            for (int i = 0; i < 10; i++) s.Add(Grade.Perfect);
            Assert.AreEqual(1f, s.Accuracy);
            Assert.AreEqual(3, s.Stars);
        }
    }
}
