using TabletopTempo.Boot;
using TMPro;
using UnityEngine;

namespace TabletopTempo.Diagnostics
{
    /// <summary>M1 smoke-test card: shows the resolved capabilities and live frame rate.</summary>
    public sealed class CapabilityReadout : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        private float _accum;
        private int _frames;
        private float _fps;

        public void Bind(TMP_Text target) => text = target;

        private void Update()
        {
            _accum += Time.unscaledDeltaTime;
            _frames++;
            if (_accum < 0.5f) return;
            _fps = _frames / _accum;
            _accum = 0f;
            _frames = 0;
            Refresh();
        }

        private void Refresh()
        {
            if (text == null) return;
            var c = CapabilityService.Current;
            text.SetText(
                "<b>Tabletop Tempo</b>  <size=70%>M1 skeleton</size>\n" +
                $"<size=60%>{c.Source} · tier {c.Tier} · {_fps:0} fps\n" +
                $"hands {Yn(c.HandTracking)}  passthrough {Yn(c.Passthrough)} ({(c.ColorPassthrough ? "colour" : "greyscale")})\n" +
                $"scene {Yn(c.SceneModel)}  depth {Yn(c.DepthApiAvailable)}  camera {Yn(c.PassthroughCameraApiAvailable)}</size>");
        }

        // ASCII only: the default TMP font atlas has no check-mark glyph.
        private static string Yn(bool b) => b ? "yes" : "no";
    }
}
