using System;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>
    /// Imports TextMesh Pro essential resources (TMP Settings, default SDF font, shaders) if missing.
    /// AssetDatabase.ImportPackage completes on a later editor tick, so this is asynchronous: it calls
    /// <c>onDone</c> when the import finished, failed, or timed out.
    /// </summary>
    public static class TmpResources
    {
        public const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const double TimeoutSeconds = 180;

        public static bool Present => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsPath) != null;

        // TMP's sample emoji sprites point to "review their licensing terms" without stating a license.
        // We don't use emoji, so they are removed (project rule: no assets with unclear licenses).
        private static readonly string[] EmojiAssets =
        {
            "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset",
            "Assets/TextMesh Pro/Sprites/EmojiOne.png",
            "Assets/TextMesh Pro/Sprites/EmojiOne.json",
            "Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt",
        };

        public static void RemoveEmojiSamples()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultSpriteAsset");
                if (p != null && p.objectReferenceValue != null)
                {
                    p.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
            foreach (var path in EmojiAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null || System.IO.File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        public static void EnsureEssentials(Action onDone)
        {
            if (Present)
            {
                RemoveEmojiSamples();
                onDone();
                return;
            }

            bool finished = false;
            double start = EditorApplication.timeSinceStartup;

            void Done(string why)
            {
                if (finished) return;
                finished = true;
                AssetDatabase.importPackageCompleted -= OnCompleted;
                AssetDatabase.importPackageFailed -= OnFailed;
                AssetDatabase.importPackageCancelled -= OnCancelled;
                EditorApplication.update -= Poll;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (Present) RemoveEmojiSamples();
                if (Present) Debug.Log($"[TT] TMP Essential Resources imported ({why}).");
                else Debug.LogError($"[TT-ERROR] TMP Essential Resources missing after import ({why}).");
                onDone();
            }

            void OnCompleted(string name) => Done("completed: " + name);
            void OnFailed(string name, string error) => Done($"failed: {name}: {error}");
            void OnCancelled(string name) => Done("cancelled: " + name);
            void Poll()
            {
                if (Present) Done("found");
                else if (EditorApplication.timeSinceStartup - start > TimeoutSeconds) Done("timeout");
            }

            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
            EditorApplication.update += Poll;

            Debug.Log("[TT] Importing TMP Essential Resources");
            TMPro.TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
        }
    }
}
