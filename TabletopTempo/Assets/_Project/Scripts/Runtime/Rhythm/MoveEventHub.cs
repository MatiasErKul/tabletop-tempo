using System;
using TabletopTempo.Core.Recognizers;
using TabletopTempo.Core.Rhythm;
using TabletopTempo.Diagnostics;
using TabletopTempo.Input;
using UnityEngine;

namespace TabletopTempo.Rhythm
{
    /// <summary>
    /// Where recognised moves are published (recognizers, debug keys, autoplay) and consumed
    /// (stage runner, judge). Times are on the input (DSP) clock.
    /// </summary>
    public static class MoveEventHub
    {
        public static event Action<MoveEvent> Moved;

        public static void Publish(MoveEvent e)
        {
            Moved?.Invoke(e);
            DevOverlay.Post(e.ToString());
        }

        /// <summary>Tests / domain-reload-free play mode.</summary>
        public static void ResetForTests() => Moved = null;
    }

    /// <summary>Feeds every processed hand frame into the gesture recognizers and publishes their moves.</summary>
    [DefaultExecutionOrder(-400)]
    public sealed class RecognizerHost : MonoBehaviour
    {
        private readonly ClapRecognizer _clap = new ClapRecognizer();
        private HandInputService _input;

        private void OnEnable()
        {
            _clap.Detected += MoveEventHub.Publish;
            TryHook();
        }

        private void OnDisable()
        {
            _clap.Detected -= MoveEventHub.Publish;
            if (_input != null) _input.FrameReady -= OnFrame;
            _input = null;
        }

        private void Update()
        {
            if (_input == null) TryHook();
        }

        private void TryHook()
        {
            if (_input != null || HandInputService.Instance == null) return;
            _input = HandInputService.Instance;
            _input.FrameReady += OnFrame;
            _input.ProviderChanged += _ => _clap.Reset();
        }

        private void OnFrame(Core.Input.HandFrame f) => _clap.Process(f);
    }

    /// <summary>Editor / desktop: keyboard stand-ins for moves (Space = clap) so stages can be tried without hands.</summary>
    public sealed class DevMoveKeys : MonoBehaviour
    {
        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) MoveEventHub.Publish(new MoveEvent(MoveId.Clap, AudioSettings.dspTime, 1f));
#endif
        }
    }
}
