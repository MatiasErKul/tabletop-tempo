using System.Collections.Generic;
using TabletopTempo.Core.Rhythm;
using TabletopTempo.Diagnostics;
using UnityEngine;

namespace TabletopTempo.Rhythm
{
    /// <summary>
    /// Runs one stage: Intro → Try it (untimed reps of the new move) → count-in → Play it (music +
    /// chart, judged) → Results. A miss never ends the run. On results, two claps play again.
    /// (Ghost-hand "See it" demos, menus, pause and save come in later milestones.)
    /// </summary>
    public sealed class StageRunner : MonoBehaviour
    {
        public enum State { Idle, Intro, Try, CountIn, Play, Results }

        [Header("Set by SceneBuilder")]
        public StageDefinition stage;
        public Conductor conductor;
        public NoteHighway highway;
        public StageHud hud;

        [Header("Options")]
        public bool autoStart = true;
        public bool skipTry;
        [Tooltip("Debug: the game plays itself (perfect hits). Toggle with A in the editor.")]
        public bool autoplay;
        public float introSeconds = 2.5f;
        public JudgeWindows windows = JudgeWindows.Standard;
        [Tooltip("Input latency to subtract, seconds (from latency calibration; per input mode).")]
        public double inputLatency;

        public State Current { get; private set; } = State.Idle;
        public Score Score { get; private set; } = new Score();
        public int TryCount { get; private set; }
        public Chart Chart { get; private set; }

        private Judge _judge;
        private float _stateTime;
        private int _autoNext;
        private string _sectionName;
        private readonly List<double> _replayClaps = new List<double>();

        private void OnEnable() => MoveEventHub.Moved += OnMove;
        private void OnDisable() => MoveEventHub.Moved -= OnMove;

        private void Start()
        {
            if (conductor != null) conductor.Finished += OnSongFinished;
            if (autoStart) StartStage();
        }

        public void StartStage()
        {
            Chart = stage.chart.Chart;
            foreach (var w in Chart.Validate()) Debug.LogWarning($"[TT] Chart {Chart.Id}: {w}");
            hud.ShowResults(null);
            Enter(State.Intro);
            hud.SetCoach($"<b>Stage {stage.number} · {stage.title}</b>\n<size=70%>{stage.intro}</size>");
        }

        private void Enter(State s)
        {
            Current = s;
            _stateTime = 0f;
            switch (s)
            {
                case State.Try:
                    TryCount = 0;
                    hud.SetCoach(TryText());
                    break;
                case State.CountIn:
                    BeginPlay();
                    break;
                case State.Results:
                    highway.Clear();
                    hud.SetCombo(0);
                    hud.ShowResults(Score);
                    hud.SetCoach("<size=70%>Clap twice to play again</size>");
                    hud.Play(hud.stageClear);
                    _replayClaps.Clear();
                    Debug.Log($"[TT] Stage {stage.number} result: {Score.Accuracy * 100f:0}% · {Score.Stars} stars · best combo {Score.BestCombo}");
                    break;
            }
        }

        private string TryText() =>
            $"<b>Try it:</b> {Moves.Coach(stage.tryMove)}\n<size=70%>{TryCount} / {stage.tryReps}</size>";

        private void BeginPlay()
        {
            Score = new Score();
            _judge = new Judge(Chart.Notes, windows, inputLatency);
            _judge.Judged += OnJudged;
            _autoNext = 0;
            _sectionName = null;

            // Lead-in: one beat of silence plus the chart's own count-in bar.
            double beat = Chart.Tempo.BeatLength(0);
            conductor.Play(Chart, stage.music, beat);
            highway.Begin(Chart, conductor);
            hud.SetCoach("<b>Get ready…</b>");
        }

        private void Update()
        {
            _stateTime += Time.deltaTime;
            if (DevKeys.AutoplayPressed())
            {
                autoplay = !autoplay;
                DevOverlay.Post("autoplay " + (autoplay ? "on" : "off"));
            }

            switch (Current)
            {
                case State.Intro:
                    if (_stateTime >= introSeconds) Enter(skipTry ? State.CountIn : State.Try);
                    break;

                case State.Try:
                    if (autoplay && _stateTime > 0.6f * (TryCount + 1)) OnMove(new MoveEvent(stage.tryMove, conductor.Clock.Now));
                    break;

                case State.CountIn:
                case State.Play:
                    TickPlay();
                    break;
            }
        }

        private void TickPlay()
        {
            double song = conductor.SongTime;
            if (Current == State.CountIn && song >= -0.05) Current = State.Play;

            // Coaching line follows the chart's sections.
            var section = Chart.SectionAt(conductor.SongBeat);
            if (section != null && section.name != _sectionName)
            {
                _sectionName = section.name;
                if (!string.IsNullOrEmpty(section.coach)) hud.SetCoach(section.coach);
            }

            if (autoplay)
            {
                var notes = Chart.Notes;
                while (_autoNext < notes.Count && notes[_autoNext].Time <= song)
                {
                    MoveEventHub.Publish(new MoveEvent(notes[_autoNext].Move, conductor.ToClockTime(notes[_autoNext].Time) + inputLatency));
                    _autoNext++;
                }
            }

            _judge.Update(song);
        }

        private void OnMove(MoveEvent e)
        {
            switch (Current)
            {
                case State.Try:
                    if (e.Move != stage.tryMove) return;
                    TryCount++;
                    hud.Play(hud.confirm);
                    hud.SetCoach(TryText());
                    if (TryCount >= stage.tryReps)
                    {
                        hud.SetCoach("<b>Great!</b> Now with music…");
                        Enter(State.CountIn);
                    }
                    break;

                case State.CountIn:
                case State.Play:
                    _judge.OnMove(e.Move, conductor.ToSongTime(e.Time));
                    break;

                case State.Results:
                    if (e.Move != MoveId.Clap || _stateTime < 1.5f) return;
                    _replayClaps.Add(e.Time);
                    _replayClaps.RemoveAll(t => e.Time - t > 1.2);
                    if (_replayClaps.Count >= 2)
                    {
                        hud.ShowResults(null);
                        hud.Play(hud.confirm);
                        Enter(State.CountIn);
                    }
                    break;
            }
        }

        private void OnJudged(Judgement j)
        {
            Score.Add(j.Grade);
            highway.OnJudged(j);
            hud.ShowJudgement(j, Chart.Notes[j.NoteIndex].Lane);
            hud.SetCombo(Score.Combo);
            if (!double.IsNaN(j.OffsetMs)) DevOverlay.Post($"judge {j}");
        }

        private void OnSongFinished()
        {
            if (Current == State.Play || Current == State.CountIn)
            {
                _judge.Update(double.MaxValue);
                Enter(State.Results);
            }
        }
    }
}
