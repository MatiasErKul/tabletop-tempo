using TabletopTempo.Core.Platform;
using UnityEngine;

namespace TabletopTempo.Boot
{
    /// <summary>
    /// Maps a <see cref="QualityTier"/> to a Unity quality level by name. The levels are created by
    /// the project setup script ("Quest Low", "Quest High"). Missing levels are ignored.
    /// </summary>
    public static class QualityTierApplier
    {
        public const string LowLevelName = "Quest Low";
        public const string HighLevelName = "Quest High";

        public static void Apply(QualityTier tier)
        {
            string wanted = tier == QualityTier.High ? HighLevelName : LowLevelName;
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == wanted)
                {
                    if (QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, applyExpensiveChanges: true);
                    Debug.Log($"[TT] Quality level '{wanted}' ({i})");
                    return;
                }
            }
            Debug.LogWarning($"[TT] Quality level '{wanted}' not found; keeping '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'.");
        }
    }
}
