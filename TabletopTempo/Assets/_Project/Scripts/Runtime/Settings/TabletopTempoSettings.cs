using TabletopTempo.Core.Platform;
using UnityEngine;

namespace TabletopTempo.Settings
{
    public enum InputMode
    {
        /// <summary>Device → Meta hands; Editor → webcam if enabled, else XR Simulator hands.</summary>
        Auto = 0,
        MetaHands = 1,
        Webcam = 2,
        XRSimulatorHands = 3,
        Replay = 4,
    }

    public enum CameraMountPreset
    {
        Overhead = 0,
        Front = 1,
    }

    /// <summary>
    /// Project-wide settings. Lives at Assets/_Project/Settings/Resources/TabletopTempoSettings.asset
    /// (created by Tools/Tabletop Tempo/Setup Project). Loaded once at boot.
    /// </summary>
    [CreateAssetMenu(menuName = "Tabletop Tempo/Settings", fileName = ResourceName)]
    public sealed class TabletopTempoSettings : ScriptableObject
    {
        public const string ResourceName = "TabletopTempoSettings";

        [Header("Input")]
        public InputMode inputMode = InputMode.Auto;

        [Tooltip("Editor/standalone only: drive hands from an external webcam via MediaPipe. Toggle in play mode with F8.")]
        public bool webcamMode = false;

        [Tooltip("Exact WebCamTexture device name. Empty = first camera.")]
        public string webcamDeviceName = "";

        public CameraMountPreset webcamMount = CameraMountPreset.Overhead;

        [Header("Optional Quest 3/3S extras (keep OFF until verified on hardware)")]
        public OptionalFeatureFlags optionalFeatures = OptionalFeatureFlags.AllOff;

        [Header("Performance")]
        public QualityOverride qualityOverride = QualityOverride.Auto;

        [Header("Editor / simulator previews")]
        [Tooltip("Editor/simulator only: render passthrough in greyscale, like Quest 2.")]
        public bool simulateGreyscalePassthrough = false;

        [Tooltip("Editor/simulator only: pretend the device is Quest 2 class (Low tier, no colour/depth/camera).")]
        public bool simulateQuest2Capabilities = true;

        [Header("Boot")]
        public string gameSceneName = "Game";

        private static TabletopTempoSettings s_cached;

        /// <summary>Loads the settings asset, or an in-memory default if it is missing.</summary>
        public static TabletopTempoSettings Load()
        {
            if (s_cached != null) return s_cached;
            s_cached = Resources.Load<TabletopTempoSettings>(ResourceName);
            if (s_cached == null)
            {
                Debug.LogWarning("[TT] TabletopTempoSettings asset not found in Resources; using defaults.");
                s_cached = CreateInstance<TabletopTempoSettings>();
            }
            return s_cached;
        }

        /// <summary>Test hook: replace the cached instance.</summary>
        public static void OverrideForTests(TabletopTempoSettings settings) => s_cached = settings;
    }
}
