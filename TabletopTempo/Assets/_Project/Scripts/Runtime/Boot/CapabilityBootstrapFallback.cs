using TabletopTempo.Settings;
using UnityEngine;

namespace TabletopTempo.Boot
{
    /// <summary>
    /// When Game.unity is played directly (editor), Boot never ran. This initialises capabilities
    /// so the scene still works. No-op when Boot already did it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class CapabilityBootstrapFallback : MonoBehaviour
    {
        private void Awake()
        {
            if (CapabilityService.IsInitialized) return;
            var caps = CapabilityService.Initialize(TabletopTempoSettings.Load());
            QualityTierApplier.Apply(caps.Tier);
        }
    }
}
