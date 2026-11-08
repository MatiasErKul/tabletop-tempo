using Meta.XR.EnvironmentDepth;
using TabletopTempo.Core.Platform;
using UnityEngine;

namespace TabletopTempo.Platform.Meta
{
    /// <summary>
    /// Feature checks through the Meta XR Core SDK. Every field is probed independently and any
    /// exception becomes "unknown" (→ Quest 2 path). No device-name checks.
    /// </summary>
    public sealed class MetaCapabilityProbe : ICapabilityProbe
    {
        public string Name => "meta-ovrplugin";

        public CapabilityProbeResult Probe()
        {
            if (!SafeInitialized()) return CapabilityProbeResult.Unknown;

            return new CapabilityProbeResult
            {
                HandTracking = CapabilityResolver.Try(OVRPlugin.GetHandTrackingEnabled),
                Passthrough = CapabilityResolver.Try(OVRManager.IsInsightPassthroughSupported),
                ColorPassthrough = CapabilityResolver.Try(() => OVRManager.GetPassthroughCapabilities().SupportsColorPassthrough),
                // Space Setup / Scene exists on Quest 2 and 3. Whether a TABLE exists is answered later by MRUK.
                SceneModel = null,
                DepthApi = CapabilityResolver.Try(() => EnvironmentDepthManager.IsSupported),
                // Camera cup finder is not compiled into this build (M10 stretch, off by default).
                PassthroughCameraApi = null,
            };
        }

        private static bool SafeInitialized()
        {
            try { return OVRPlugin.initialized; }
            catch { return false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            CapabilityProbeRegistry.Register(new MetaCapabilityProbe(), priority: 100);
        }
    }
}
