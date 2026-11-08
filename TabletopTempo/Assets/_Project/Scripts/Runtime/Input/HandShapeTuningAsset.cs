using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Input
{
    [CreateAssetMenu(menuName = "Tabletop Tempo/Hand Shape Tuning", fileName = "HandShapeTuning")]
    public sealed class HandShapeTuningAsset : ScriptableObject
    {
        public HandShapeTuning tuning = HandShapeTuning.Default;

        private void Reset() => tuning = HandShapeTuning.Default;
    }
}
