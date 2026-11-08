using UnityEditor;

namespace TabletopTempo.EditorTools.Setup
{
    internal static class AssetFolders
    {
        /// <summary>Creates "Assets/a/b/c" through the AssetDatabase (so CreateAsset works immediately).</summary>
        public static void Ensure(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            int slash = assetPath.LastIndexOf('/');
            string parent = assetPath.Substring(0, slash);
            string leaf = assetPath.Substring(slash + 1);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
