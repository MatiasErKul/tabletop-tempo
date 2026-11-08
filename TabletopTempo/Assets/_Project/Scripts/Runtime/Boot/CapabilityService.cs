using TabletopTempo.Core.Platform;
using TabletopTempo.Settings;
using UnityEngine;

namespace TabletopTempo.Boot
{
    /// <summary>
    /// Resolves device capabilities once at boot and exposes them to the game.
    /// Platform assemblies register an <see cref="ICapabilityProbe"/>; if none is registered or it
    /// throws, the Quest 2 baseline is used.
    /// </summary>
    public static class CapabilityService
    {
        private static bool s_initialized;
        private static DeviceCapabilities s_current = DeviceCapabilities.Quest2Baseline("not-initialized");

        public static DeviceCapabilities Current => s_current;
        public static bool IsInitialized => s_initialized;

        public static DeviceCapabilities Initialize(TabletopTempoSettings settings)
        {
            var probe = CapabilityProbeRegistry.Current;
            var flags = settings != null ? settings.optionalFeatures : OptionalFeatureFlags.AllOff;
            var quality = settings != null ? settings.qualityOverride : QualityOverride.Auto;

#if UNITY_EDITOR
            // In the editor/XR Simulator the runtime answers describe the PC, not a headset.
            // Default to the Quest 2 profile so we design against the weakest target.
            if (settings != null && settings.simulateQuest2Capabilities)
            {
                probe = new FixedCapabilityProbe("editor-simulated-quest2", new CapabilityProbeResult
                {
                    HandTracking = true, Passthrough = true, ColorPassthrough = false,
                    SceneModel = true, DepthApi = false, PassthroughCameraApi = false,
                });
            }
#endif

            var raw = CapabilityResolver.SafeProbe(probe, msg => Debug.LogWarning("[TT] " + msg));
            s_current = CapabilityResolver.Resolve(raw, flags, quality, probe != null ? probe.Name : "none");
            s_initialized = true;
            Debug.Log($"[TT] Capabilities probe: {raw}");
            Debug.Log($"[TT] Capabilities resolved: {s_current}");
            return s_current;
        }

        /// <summary>Test hook.</summary>
        public static void SetForTests(DeviceCapabilities caps)
        {
            s_current = caps;
            s_initialized = true;
        }
    }
}
