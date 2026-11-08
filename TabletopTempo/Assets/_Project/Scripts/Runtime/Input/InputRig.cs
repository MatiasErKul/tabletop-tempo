using TabletopTempo.Core.Input;
using TabletopTempo.Settings;
using UnityEngine;

namespace TabletopTempo.Input
{
    /// <summary>
    /// Picks the hand provider at startup (section 4):
    ///   device → Meta hands · editor + webcam mode → webcam (M3) · editor → XR Simulator hands (Meta path)
    ///   replay asset set → replay. Manual override via TabletopTempoSettings.inputMode.
    /// Also keeps the table frame in sync with the TableAnchor until calibration takes over.
    /// </summary>
    [RequireComponent(typeof(HandInputService))]
    public sealed class InputRig : MonoBehaviour
    {
        [SerializeField] private Transform tableAnchor;
        [Tooltip("Optional: play this .ttrec recording instead of live hands (editor/tests).")]
        [SerializeField] private TextAsset replay;

        private HandInputService _service;
        private Vector3 _lastAnchorPos;
        private float _lastAnchorYaw = float.NaN;

        public void Bind(Transform anchor) => tableAnchor = anchor;

        private void Start()
        {
            _service = GetComponent<HandInputService>();
            SyncTable(force: true);
            _service.SetProvider(Choose(TabletopTempoSettings.Load()));
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Diagnostics.DevKeys.ToggleWebcamPressed()) ToggleWebcam();
#endif
        }

        private void LateUpdate() => SyncTable(force: false);

        /// <summary>F8: switch between webcam hands and Meta / XR Simulator hands at runtime.</summary>
        public void ToggleWebcam()
        {
            bool toWebcam = _service.ProviderName != "webcam-mediapipe";
            if (_service.Provider is Behaviour old) old.enabled = false;
            IHandFrameProvider next = toWebcam ? HandProviderRegistry.Create(HandProviderRegistry.Webcam, gameObject) : null;
            if (toWebcam && next == null)
                Debug.LogWarning("[TT] F8: webcam provider not available (run tools/webcam/fetch_mediapipe.py).");
            if (next == null)
            {
                next = HandProviderRegistry.Create(HandProviderRegistry.Meta, gameObject);
                if (next is Behaviour b) b.enabled = true;
            }
            _service.SetProvider(next);
            Diagnostics.DevOverlay.Post("input: " + _service.ProviderName);
        }

        private IHandFrameProvider Choose(TabletopTempoSettings s)
        {
            InputMode mode = s.inputMode;
            if (replay != null) mode = InputMode.Replay;
#if UNITY_EDITOR || UNITY_STANDALONE
            if (mode == InputMode.Auto && s.webcamMode) mode = InputMode.Webcam;
#endif
            switch (mode)
            {
                case InputMode.Replay when replay != null:
                    return new ReplayHandProvider(ReplaySession.FromText(replay.text), loop: true);
                case InputMode.Webcam:
                    var webcam = HandProviderRegistry.Create(HandProviderRegistry.Webcam, gameObject);
                    if (webcam != null) return webcam;
                    Debug.LogWarning("[TT] Webcam mode requested but no webcam provider is available; using Meta/XR Simulator hands.");
                    goto default;
                default:
                    var meta = HandProviderRegistry.Create(HandProviderRegistry.Meta, gameObject);
                    if (meta == null) Debug.LogWarning("[TT] No hand provider available.");
                    return meta;
            }
        }

        private void SyncTable(bool force)
        {
            if (tableAnchor == null || _service == null) return;
            Vector3 p = tableAnchor.position;
            float yaw = tableAnchor.eulerAngles.y;
            if (!force && p == _lastAnchorPos && Mathf.Approximately(yaw, _lastAnchorYaw)) return;
            _lastAnchorPos = p;
            _lastAnchorYaw = yaw;
            _service.SetTable(new TableSpace(p, yaw));
        }
    }
}
