using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Platform.Meta
{
    /// <summary>Registers Meta-backed implementations with the game's registries before any scene loads.</summary>
    internal static class MetaRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            HandProviderRegistry.Register(HandProviderRegistry.Meta, host =>
            {
                var p = host.GetComponent<MetaHandProvider>();
                if (p == null) p = host.AddComponent<MetaHandProvider>();
                return p;
            });
        }
    }
}
