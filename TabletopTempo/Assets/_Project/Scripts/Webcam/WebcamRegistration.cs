using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Webcam
{
    internal static class WebcamRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            HandProviderRegistry.Register(HandProviderRegistry.Webcam, host =>
            {
                var p = host.GetComponent<WebcamMediaPipeProvider>();
                if (p == null) p = host.AddComponent<WebcamMediaPipeProvider>();
                p.enabled = true;
                return p;
            });
        }
    }
}
