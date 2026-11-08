using System;
using System.Threading.Tasks;
using TabletopTempo.EditorTools.Setup;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools
{
    /// <summary>
    /// Batch-mode entry points. Use via tools/unity.ps1, e.g.
    ///   ./tools/unity.ps1 setup
    /// SetupProject is asynchronous (Meta's Project Setup fixer runs on the editor loop), so it is
    /// launched WITHOUT -quit and exits the editor itself.
    /// </summary>
    public static class BatchEntry
    {
        private const double TimeoutSeconds = 600;

        [MenuItem("Tools/Tabletop Tempo/Setup Project (all steps)", priority = 0)]
        public static void SetupProjectMenu() => RunSetup(exitWhenDone: false);

        /// <summary>Batch: full project configuration + scene rebuild.</summary>
        public static void SetupProject() => RunSetup(exitWhenDone: true);

        /// <summary>Batch: only regenerate scenes (fast).</summary>
        public static void RebuildScenes()
        {
            int code = 0;
            try { SceneBuilder.Rebuild(); }
            catch (Exception e) { Debug.LogError("[TT-ERROR] " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static void RunSetup(bool exitWhenDone)
        {
            Debug.Log("[TT] Setup: step 1/2 – Meta Project Setup fixes (Android)");
            Task fix;
            try
            {
                fix = OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TT] Meta Project Setup fixer unavailable, continuing: " + e.Message);
                fix = Task.CompletedTask;
            }

            double start = EditorApplication.timeSinceStartup;
            void Poll()
            {
                bool timedOut = EditorApplication.timeSinceStartup - start > TimeoutSeconds;
                if (!fix.IsCompleted && !timedOut) return;
                EditorApplication.update -= Poll;
                if (timedOut) Debug.LogWarning("[TT] Meta Project Setup fixer timed out; continuing.");
                if (fix.IsFaulted) Debug.LogWarning("[TT] Meta Project Setup fixer faulted: " + fix.Exception?.GetBaseException().Message);
                Finish(exitWhenDone);
            }
            EditorApplication.update += Poll;
        }

        private static void Finish(bool exitWhenDone)
        {
            try
            {
                Debug.Log("[TT] Setup: step 2/3 – our explicit settings");
                PlayerSettingsSetup.Apply();
                RenderingSetup.Apply();
                MetaProjectSetup.Apply();
                SettingsAssetSetup.Apply();
                BuildQuest.ScrubDevAgentSettings();
            }
            catch (Exception e)
            {
                Fail(e, exitWhenDone);
                return;
            }

            // TMP import is asynchronous; scenes need its font + settings.
            TmpResources.EnsureEssentials(() =>
            {
                int code = 0;
                try
                {
                    Debug.Log("[TT] Setup: step 3/3 – generated assets + scenes");
                    Importers.AssetImportRules.ReimportGenerated();
                    StageSetup.Stage1();
                    SceneBuilder.Rebuild();
                    AssetDatabase.SaveAssets();
                    if (!TmpResources.Present) code = 1;
                    Debug.Log(code == 0 ? "[TT] Setup complete." : "[TT-ERROR] Setup finished with errors.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[TT-ERROR] Setup failed: " + e);
                    code = 1;
                }
                if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(code);
            });
        }

        private static void Fail(Exception e, bool exitWhenDone)
        {
            Debug.LogError("[TT-ERROR] Setup failed: " + e);
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
