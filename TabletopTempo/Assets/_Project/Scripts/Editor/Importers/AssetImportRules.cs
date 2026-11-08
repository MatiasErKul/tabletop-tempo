using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Importers
{
    /// <summary>
    /// Consistent import settings for generated assets (re-applied on reimport):
    ///  * music: Vorbis, compressed in memory (small, PlayScheduled-accurate)
    ///  * SFX: mono ADPCM, decompressed on load (instant playback)
    ///  * pictograms: mipmapped, clamped, alpha = transparency, max 256 (crisp at 10 cm in VR)
    /// </summary>
    public sealed class AssetImportRules : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/_Project/Audio/Music/";
        public const string SfxFolder = "Assets/_Project/Audio/Sfx/";
        public const string PictogramFolder = "Assets/_Project/Art/Pictograms/";

        private void OnPreprocessAudio()
        {
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;
            if (assetPath.StartsWith(MusicFolder))
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                imp.loadInBackground = false;
            }
            else if (assetPath.StartsWith(SfxFolder))
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                imp.forceToMono = true;
                imp.loadInBackground = false;
            }
            else return;
            imp.defaultSampleSettings = s;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PictogramFolder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 2;
            t.maxTextureSize = 256;
            t.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>Re-imports generated assets so these rules apply to files created before them.</summary>
        public static void ReimportGenerated()
        {
            foreach (var folder in new[] { MusicFolder, SfxFolder, PictogramFolder })
            {
                string f = folder.TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(f)) continue;
                foreach (var guid in AssetDatabase.FindAssets("", new[] { f }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(p)) continue;
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
