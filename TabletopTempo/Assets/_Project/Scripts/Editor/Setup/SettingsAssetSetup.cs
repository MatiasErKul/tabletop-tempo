using System.IO;
using TabletopTempo.Settings;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>Creates the TabletopTempoSettings asset in a Resources folder if missing.</summary>
    public static class SettingsAssetSetup
    {
        public const string Folder = "Assets/_Project/Settings/Resources";
        public const string AssetPath = Folder + "/" + TabletopTempoSettings.ResourceName + ".asset";

        public static TabletopTempoSettings Apply()
        {
            AssetFolders.Ensure(Folder);
            var settings = AssetDatabase.LoadAssetAtPath<TabletopTempoSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TabletopTempoSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                Debug.Log("[TT] Created " + AssetPath);
            }

            // Shipped default: every Quest-3-only extra stays off until verified (UNTESTED_ON_QUEST3.md).
            settings.optionalFeatures = Core.Platform.OptionalFeatureFlags.AllOff;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
