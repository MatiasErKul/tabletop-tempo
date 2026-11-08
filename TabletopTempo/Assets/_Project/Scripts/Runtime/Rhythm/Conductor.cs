using System;
using TabletopTempo.Core.Rhythm;
using UnityEngine;

namespace TabletopTempo.Rhythm
{
    /// <summary>Time source. The game uses the audio DSP clock; tests use a manual clock.</summary>
    public interface IClock
    {
        double Now { get; }
    }

    public sealed class DspClock : IClock
    {
        public double Now => AudioSettings.dspTime;
    }

    public sealed class ManualClock : IClock
    {
        public double Now { get; set; }
    }

    /// <summary>
    /// Single source of musical truth. Schedules the track with PlayScheduled on the DSP clock and
    /// converts any DSP time (e.g. a recognised clap) into song time and beats.
    /// Song time 0 = beat 0 = <c>dspStart + chart.OffsetSeconds</c>.
    /// Visual time is smoothed (the DSP clock advances in audio-buffer steps).
    /// </summary>
    public sealed class Conductor : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;

        public IClock Clock { get; set; } = new DspClock();
        public Chart Chart { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>DSP time at which the audio file starts.</summary>
        public double DspStart { get; private set; }

        /// <summary>Smoothed song time for visuals (seconds since beat 0). Negative during lead-in.</summary>
        public double SongTime { get; private set; } = double.NegativeInfinity;
        public double SongBeat => Chart != null ? Chart.Tempo.SecondsToBeat(SongTime) : 0;

        public event Action Finished;

        private double _lastClock;
        private double _visual;
        private double _pausedAt;

        public void Bind(AudioSource source) => musicSource = source;

        /// <summary>Starts the chart <paramref name="leadIn"/> seconds from now (count-in).</summary>
        public void Play(Chart chart, AudioClip clip, double leadIn = 1.0)
        {
            Chart = chart;
            double now = Clock.Now;
            DspStart = now + leadIn;
            if (musicSource != null && clip != null && Clock is DspClock)
            {
                musicSource.Stop();
                musicSource.clip = clip;
                musicSource.PlayScheduled(DspStart);
            }
            _lastClock = now;
            _visual = now;
            IsPlaying = true;
            IsPaused = false;
            UpdateTime();
        }

        public void Stop()
        {
            IsPlaying = false;
            if (musicSource != null) musicSource.Stop();
        }

        /// <summary>Converts a DSP-clock time (input events) to song seconds.</summary>
        public double ToSongTime(double clockTime) => clockTime - DspStart - (Chart?.OffsetSeconds ?? 0);

        /// <summary>Converts song seconds back to the clock (for scheduling sounds on the beat).</summary>
        public double ToClockTime(double songTime) => songTime + DspStart + (Chart?.OffsetSeconds ?? 0);

        public void Pause()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _pausedAt = Clock.Now;
            if (musicSource != null) musicSource.Pause();
        }

        /// <summary>Resumes; the song continues exactly where it paused (DSP start shifted by the pause length).</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            double pausedFor = Clock.Now - _pausedAt;
            DspStart += pausedFor;
            IsPaused = false;
            if (musicSource != null) musicSource.UnPause();
        }

        private void Update()
        {
            if (!IsPlaying || IsPaused) return;
            UpdateTime();
            if (Chart != null && SongTime > Chart.LengthSeconds + 1.0)
            {
                IsPlaying = false;
                Finished?.Invoke();
            }
        }

        private void UpdateTime()
        {
            double clock = Clock.Now;
            if (clock != _lastClock)
            {
                _lastClock = clock;
                // Never run backwards; snap toward the real clock.
                _visual = Math.Max(_visual, clock - 0.03);
                if (Math.Abs(_visual - clock) > 0.05) _visual = clock;
            }
            else
            {
                _visual += Time.unscaledDeltaTime;
                if (_visual > _lastClock + 0.1) _visual = _lastClock + 0.1;
            }
            SongTime = ToSongTime(_visual);
        }

        /// <summary>Tests: recompute immediately after moving a manual clock.</summary>
        public void Tick() => UpdateTime();
    }
}
