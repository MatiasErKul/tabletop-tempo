namespace TabletopTempo.Core.Platform
{
    /// <summary>
    /// A platform-specific runtime feature check (Meta device, XR Simulator, editor, test).
    /// Implementations must check features, never device names.
    /// </summary>
    public interface ICapabilityProbe
    {
        string Name { get; }
        CapabilityProbeResult Probe();
    }

    /// <summary>
    /// Platform assemblies register their probe here (e.g. from RuntimeInitializeOnLoadMethod),
    /// so the game assemblies never reference a platform SDK directly. Highest priority wins.
    /// </summary>
    public static class CapabilityProbeRegistry
    {
        private static ICapabilityProbe s_probe;
        private static int s_priority = int.MinValue;

        public static ICapabilityProbe Current => s_probe;

        public static void Register(ICapabilityProbe probe, int priority)
        {
            if (probe == null || priority < s_priority) return;
            s_probe = probe;
            s_priority = priority;
        }

        /// <summary>For tests and domain-reload-free play mode.</summary>
        public static void Reset()
        {
            s_probe = null;
            s_priority = int.MinValue;
        }
    }

    /// <summary>Fixed answers, for tests and for the editor without an XR runtime.</summary>
    public sealed class FixedCapabilityProbe : ICapabilityProbe
    {
        private readonly CapabilityProbeResult _result;

        public FixedCapabilityProbe(string name, CapabilityProbeResult result)
        {
            Name = name;
            _result = result;
        }

        public string Name { get; }
        public CapabilityProbeResult Probe() => _result;
    }
}
