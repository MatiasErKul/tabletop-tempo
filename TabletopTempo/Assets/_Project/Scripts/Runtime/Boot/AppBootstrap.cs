using TabletopTempo.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabletopTempo.Boot
{
    /// <summary>
    /// Lives alone in the Boot scene. Resolves capabilities, applies the quality tier, then loads the
    /// Game scene. Keep this tiny: it is on the cold-start path.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private bool loadGameScene = true;

        private void Awake()
        {
            var settings = TabletopTempoSettings.Load();
            var caps = CapabilityService.Initialize(settings);
            QualityTierApplier.Apply(caps.Tier);

            if (loadGameScene && !string.IsNullOrEmpty(settings.gameSceneName))
            {
                SceneManager.LoadSceneAsync(settings.gameSceneName, LoadSceneMode.Single);
            }
        }
    }
}
