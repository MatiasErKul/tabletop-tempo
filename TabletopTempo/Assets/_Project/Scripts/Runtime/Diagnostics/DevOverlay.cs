using System.Text;
using TabletopTempo.Core.Input;
using TabletopTempo.Input;
using TMPro;
using UnityEngine;

namespace TabletopTempo.Diagnostics
{
    /// <summary>
    /// Developer overlay (toggle F9 in the editor / simulator): provider, per-hand tracking and shape
    /// scores, palm height above the table, recent debug events, fps. World-space so it shows up in
    /// simulator recordings. Gameplay systems post events with <see cref="Post"/>.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private bool visibleOnStart = false;

        private static readonly string[] s_events = new string[6];
        private static int s_eventHead;
        private readonly StringBuilder _sb = new StringBuilder(512);
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fps;
        private float _nextRefresh;

        public void Bind(TMP_Text target) => text = target;

        /// <summary>Adds a line to the overlay's event list (e.g. recognizer hits with timestamps).</summary>
        public static void Post(string line)
        {
            s_events[s_eventHead] = line;
            s_eventHead = (s_eventHead + 1) % s_events.Length;
        }

        private void Start()
        {
            if (text != null) text.gameObject.SetActive(visibleOnStart);
        }

        private void Update()
        {
            if (DevKeys.ToggleOverlayPressed() && text != null)
                text.gameObject.SetActive(!text.gameObject.activeSelf);

            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum >= 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }

            if (text == null || !text.gameObject.activeSelf || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            var svc = HandInputService.Instance;
            var f = svc != null ? svc.History.Latest : null;
            _sb.Clear();
            _sb.Append("<b>DEBUG</b>  ").Append(svc != null ? svc.ProviderName : "no input service")
               .Append("  ").Append(_fps.ToString("0")).Append(" fps\n");
            if (f != null)
            {
                Hand(_sb, "L", f.Left);
                Hand(_sb, "R", f.Right);
            }
            for (int i = 0; i < s_events.Length; i++)
            {
                var e = s_events[(s_eventHead - 1 - i + s_events.Length * 2) % s_events.Length];
                if (!string.IsNullOrEmpty(e)) _sb.Append("<size=80%>").Append(e).Append("</size>\n");
            }
            text.SetText(_sb);
        }

        private static void Hand(StringBuilder sb, string label, HandData h)
        {
            sb.Append(label).Append(": ");
            if (!h.Tracked)
            {
                sb.Append("--\n");
                return;
            }
            sb.Append("conf ").Append(h.Confidence.ToString("0.0"))
              .Append(" fist ").Append(h.FistScore.ToString("0.00"))
              .Append(" flat ").Append(h.FlatScore.ToString("0.00"))
              .Append(" pinch ").Append(h.PinchStrength.ToString("0.00"))
              .Append(" y ").Append((h.PalmCenter.y * 100f).ToString("0")).Append("cm")
              .Append(" v ").Append(h.PalmVelocity.y.ToString("0.00")).Append('\n');
        }
    }

    /// <summary>Editor/simulator developer hotkeys (Input System). No-ops on device (no keyboard).</summary>
    public static class DevKeys
    {
        public static bool ToggleOverlayPressed() => Pressed(UnityEngine.InputSystem.Key.F9);
        public static bool ToggleRecordingPressed() => Pressed(UnityEngine.InputSystem.Key.F10);
        public static bool ToggleWebcamPressed() => Pressed(UnityEngine.InputSystem.Key.F8);
        public static bool AutoplayPressed() => Pressed(UnityEngine.InputSystem.Key.A);

        private static bool Pressed(UnityEngine.InputSystem.Key key)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }
    }
}
