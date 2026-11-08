using System;

namespace TabletopTempo.Core.Platform
{
    /// <summary>Rendering/effects budget. Picked from capabilities, never from a device name.</summary>
    public enum QualityTier
    {
        /// <summary>Quest 2 budget (72 fps target). Also the fallback whenever anything is unknown.</summary>
        Low = 0,
        /// <summary>Quest 3 / 3S class hardware: a little more polish.</summary>
        High = 1,
    }

    public enum QualityOverride
    {
        Auto = 0,
        ForceLow = 1,
        ForceHigh = 2,
    }

    /// <summary>
    /// Optional Quest-3-class extras. All OFF by default and only switched on once someone has
    /// verified them on real hardware (see docs/UNTESTED_ON_QUEST3.md).
    /// </summary>
    [Serializable]
    public struct OptionalFeatureFlags
    {
        /// <summary>Headset-camera cup finder (Passthrough Camera API + detector). Stretch goal.</summary>
        public bool CameraCupFinder;

        /// <summary>Depth API / MRUK environment raycast.</summary>
        public bool DepthRaycast;

        public static OptionalFeatureFlags AllOff => default;
    }

    /// <summary>
    /// Raw result of runtime feature probes. <c>null</c> means "the probe failed or could not tell",
    /// which the resolver always turns into the Quest 2 answer.
    /// </summary>
    public struct CapabilityProbeResult
    {
        public bool? HandTracking;
        public bool? Passthrough;
        public bool? ColorPassthrough;
        public bool? SceneModel;
        public bool? DepthApi;
        public bool? PassthroughCameraApi;

        public static CapabilityProbeResult Unknown => default;

        public override string ToString() =>
            $"hands={Fmt(HandTracking)} passthrough={Fmt(Passthrough)} color={Fmt(ColorPassthrough)} " +
            $"scene={Fmt(SceneModel)} depth={Fmt(DepthApi)} camera={Fmt(PassthroughCameraApi)}";

        private static string Fmt(bool? b) => b.HasValue ? (b.Value ? "yes" : "no") : "?";
    }

    /// <summary>What the game is allowed to use on this device, after fallbacks and feature flags.</summary>
    public readonly struct DeviceCapabilities
    {
        /// <summary>Hand tracking is available (the core game needs it).</summary>
        public readonly bool HandTracking;

        /// <summary>Passthrough can be shown.</summary>
        public readonly bool Passthrough;

        /// <summary>Passthrough is in colour. False on Quest 2 (greyscale), so UI must never rely on colour alone.</summary>
        public readonly bool ColorPassthrough;

        /// <summary>The Scene model (Space Setup / MRUK) may be queried for a TABLE. Absence is fine: touch calibration is the main path.</summary>
        public readonly bool SceneModel;

        /// <summary>Hardware supports the Depth API (informational).</summary>
        public readonly bool DepthApiAvailable;

        /// <summary>Hardware supports the Passthrough Camera API (informational).</summary>
        public readonly bool PassthroughCameraApiAvailable;

        /// <summary>Depth raycast may be used: available AND feature flag on.</summary>
        public readonly bool UseDepthRaycast;

        /// <summary>Camera cup finder may be used: available AND feature flag on.</summary>
        public readonly bool UseCameraCupFinder;

        public readonly QualityTier Tier;

        /// <summary>Which probe produced this (for the debug overlay and logs).</summary>
        public readonly string Source;

        public DeviceCapabilities(
            bool handTracking, bool passthrough, bool colorPassthrough, bool sceneModel,
            bool depthApiAvailable, bool passthroughCameraApiAvailable,
            bool useDepthRaycast, bool useCameraCupFinder, QualityTier tier, string source)
        {
            HandTracking = handTracking;
            Passthrough = passthrough;
            ColorPassthrough = colorPassthrough;
            SceneModel = sceneModel;
            DepthApiAvailable = depthApiAvailable;
            PassthroughCameraApiAvailable = passthroughCameraApiAvailable;
            UseDepthRaycast = useDepthRaycast;
            UseCameraCupFinder = useCameraCupFinder;
            Tier = tier;
            Source = source ?? "unknown";
        }

        /// <summary>The Quest 2 path: hands + greyscale passthrough + optional Space Setup table, nothing else.</summary>
        public static DeviceCapabilities Quest2Baseline(string source) =>
            CapabilityResolver.Resolve(CapabilityProbeResult.Unknown, OptionalFeatureFlags.AllOff, QualityOverride.Auto, source);

        public override string ToString() =>
            $"[{Source}] tier={Tier} hands={HandTracking} passthrough={Passthrough} color={ColorPassthrough} " +
            $"scene={SceneModel} depthHw={DepthApiAvailable} cameraHw={PassthroughCameraApiAvailable} " +
            $"useDepth={UseDepthRaycast} useCameraCup={UseCameraCupFinder}";
    }
}
