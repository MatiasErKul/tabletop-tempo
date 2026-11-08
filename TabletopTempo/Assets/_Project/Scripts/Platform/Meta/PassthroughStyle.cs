using TabletopTempo.Settings;
using UnityEngine;

namespace TabletopTempo.Platform.Meta
{
    /// <summary>
    /// Editor/simulator helper: render passthrough in greyscale (like Quest 2) so UI contrast can be
    /// checked without a headset. On device this does nothing; real Quest 2 passthrough is already
    /// greyscale and Quest 3/3S stays in colour.
    /// </summary>
    [RequireComponent(typeof(OVRPassthroughLayer))]
    public sealed class PassthroughStyle : MonoBehaviour
    {
        private OVRPassthroughLayer _layer;
        private bool _applied;

        private void Awake() => _layer = GetComponent<OVRPassthroughLayer>();

        private void Start() => Apply(TabletopTempoSettings.Load().simulateGreyscalePassthrough);

        public void Apply(bool greyscale)
        {
            if (_layer == null) return;
#if UNITY_EDITOR
            if (greyscale)
            {
                _layer.SetBrightnessContrastSaturation(brightness: 0f, contrast: 0f, saturation: -1f);
                _applied = true;
            }
            else if (_applied)
            {
                _layer.SetBrightnessContrastSaturation(0f, 0f, 0f);
                _applied = false;
            }
#endif
        }
    }
}
