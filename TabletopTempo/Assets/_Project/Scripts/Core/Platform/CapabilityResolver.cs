using System;

namespace TabletopTempo.Core.Platform
{
    /// <summary>
    /// Turns raw probe results into the capabilities the game may use.
    /// Rules (docs/UNTESTED_ON_QUEST3.md, prompt section 2b):
    ///  * Unknown core features (hands, passthrough, scene) assume the Quest 2 answer: available.
    ///    The manifest requires hands + passthrough, so "unknown" almost always means "probe failed".
    ///  * Unknown extras (colour, depth, camera) are treated as absent.
    ///  * Quest-3-only extras are used only when available AND their feature flag is on.
    ///  * Quality tier is High only when BOTH colour passthrough and depth hardware are confirmed
    ///    (a capability-based proxy for Quest 3 / 3S class hardware). Anything else is Low.
    /// </summary>
    public static class CapabilityResolver
    {
        public static DeviceCapabilities Resolve(
            in CapabilityProbeResult probe, OptionalFeatureFlags flags, QualityOverride quality, string source)
        {
            bool hands = probe.HandTracking ?? true;
            bool passthrough = probe.Passthrough ?? true;
            bool scene = probe.SceneModel ?? true;
            bool color = probe.ColorPassthrough ?? false;
            bool depthHw = probe.DepthApi ?? false;
            bool cameraHw = probe.PassthroughCameraApi ?? false;

            QualityTier tier;
            switch (quality)
            {
                case QualityOverride.ForceLow: tier = QualityTier.Low; break;
                case QualityOverride.ForceHigh: tier = QualityTier.High; break;
                default: tier = (color && depthHw) ? QualityTier.High : QualityTier.Low; break;
            }

            return new DeviceCapabilities(
                handTracking: hands,
                passthrough: passthrough,
                colorPassthrough: color,
                sceneModel: scene,
                depthApiAvailable: depthHw,
                passthroughCameraApiAvailable: cameraHw,
                useDepthRaycast: depthHw && flags.DepthRaycast,
                useCameraCupFinder: cameraHw && flags.CameraCupFinder,
                tier: tier,
                source: source);
        }

        /// <summary>
        /// Runs a probe and never throws. Any exception yields <see cref="CapabilityProbeResult.Unknown"/>,
        /// i.e. the Quest 2 path.
        /// </summary>
        public static CapabilityProbeResult SafeProbe(ICapabilityProbe probe, Action<string> logWarning = null)
        {
            if (probe == null) return CapabilityProbeResult.Unknown;
            try
            {
                return probe.Probe();
            }
            catch (Exception e)
            {
                logWarning?.Invoke($"Capability probe '{probe.Name}' threw, using Quest 2 path: {e.GetType().Name}: {e.Message}");
                return CapabilityProbeResult.Unknown;
            }
        }

        /// <summary>Evaluates one probe field and turns any exception into "unknown".</summary>
        public static bool? Try(Func<bool> check)
        {
            if (check == null) return null;
            try { return check(); }
            catch { return null; }
        }
    }
}
