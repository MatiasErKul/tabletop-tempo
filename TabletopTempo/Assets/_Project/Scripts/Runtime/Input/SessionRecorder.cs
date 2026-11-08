using System;
using System.IO;
using TabletopTempo.Core.Input;
using TabletopTempo.Diagnostics;
using UnityEngine;

namespace TabletopTempo.Input
{
    /// <summary>
    /// Records the processed hand stream (table space) to a .ttrec file for replay tests.
    /// Toggle with F10 or Tools/Tabletop Tempo/Record Session (play mode). Files go to
    /// &lt;repo&gt;/Recordings/ (gitignored); copy good ones into Assets/_Project/Tests/Replays/.
    /// Editor and desktop builds only.
    /// </summary>
    public sealed class SessionRecorder : MonoBehaviour
    {
        private ReplaySession _session;
        private HandInputService _service;

        public bool IsRecording => _session != null;
        public static SessionRecorder Instance { get; private set; }

        private void Awake() => Instance = this;
        private void OnDestroy()
        {
            if (IsRecording) Stop();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (DevKeys.ToggleRecordingPressed()) Toggle();
        }

        public void Toggle()
        {
            if (IsRecording) Stop();
            else Begin();
        }

        public void Begin(string note = "")
        {
            _service = HandInputService.Instance;
            if (_service == null)
            {
                Debug.LogWarning("[TT] Recorder: no HandInputService");
                return;
            }
            _session = new ReplaySession { Source = _service.ProviderName, Note = note };
            _service.FrameReady += OnFrame;
            DevOverlay.Post("REC started");
            Debug.Log("[TT] Recording hand session…");
        }

        public string Stop()
        {
            if (_session == null) return null;
            _service.FrameReady -= OnFrame;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Recordings"));
            if (!Application.isEditor) dir = Path.Combine(Application.persistentDataPath, "Recordings");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(_session.Source)}.ttrec");
            File.WriteAllText(path, _session.ToText());
            Debug.Log($"[TT] Recording saved: {path} ({_session.Frames.Count} frames, {_session.Duration:0.0}s)");
            DevOverlay.Post($"REC saved {_session.Frames.Count} frames");
            _session = null;
            return path;
        }

        private void OnFrame(HandFrame f) => _session?.Add(f);

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(':', '_');
        }
    }
}
