using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TabletopTempo.EditorTools
{
    /// <summary>
    /// Builds the single Quest 2 / 3 / 3S APK to &lt;repo&gt;/Builds/TabletopTempo.apk.
    /// Batch: tools/unity.ps1 build   (or build-dev for a development build)
    /// </summary>
    public static class BuildQuest
    {
        public const string ApkName = "TabletopTempo.apk";

        public static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", ApkName));

        [MenuItem("Tools/Tabletop Tempo/Build Quest APK (release)", priority = 200)]
        public static void BuildReleaseMenu() => Build(development: false);

        [MenuItem("Tools/Tabletop Tempo/Build Quest APK (development)", priority = 201)]
        public static void BuildDevelopmentMenu() => Build(development: true);

        // Batch-mode entry points (exit code reflects success).
        public static void BuildRelease() => ExitWith(Build(development: false));
        public static void BuildDevelopment() => ExitWith(Build(development: true));

        public static bool Build(bool development)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[TT-ERROR] No scenes in Build Settings. Run Tools/Tabletop Tempo/Rebuild Scenes first.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            ScrubDevAgentSettings();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? (BuildOptions.Development | BuildOptions.ConnectWithProfiler) : BuildOptions.None,
            };

            Debug.Log($"[TT] Building {(development ? "development" : "release")} APK → {OutputPath}\n  scenes: {string.Join(", ", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[TT] Build OK: {s.outputPath} ({s.totalSize / (1024f * 1024f):F1} MB) in {s.totalTime.TotalSeconds:F0}s");
                return true;
            }

            Debug.LogError($"[TT-ERROR] Build {s.result}: {s.totalErrors} error(s).");
            foreach (var step in report.steps)
            foreach (var msg in step.messages)
            {
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    Debug.LogError($"[TT-ERROR]   {step.name}: {msg.content}");
            }
            return false;
        }

        /// <summary>
        /// Meta's Dev Agent settings live in Resources (so they ship in the APK) and hold a local access
        /// token plus the dev PC's LAN IP. Blank them and keep the agent disabled before every build.
        /// </summary>
        public static void ScrubDevAgentSettings()
        {
            const string path = "Assets/Resources/DevAgentSettings.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            bool changed = false;
            foreach (var name in new[] { "accessToken", "serverAddress", "witClientAccessToken" })
            {
                var p = so.FindProperty(name);
                if (p != null && p.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = "";
                    changed = true;
                }
            }
            var enabled = so.FindProperty("enabled");
            if (enabled != null && enabled.boolValue) { enabled.boolValue = false; changed = true; }
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[TT] Scrubbed DevAgentSettings (token / LAN IP) before build.");
        }

        private static void ExitWith(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
