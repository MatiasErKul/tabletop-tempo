using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools
{
    /// <summary>
    /// Points Unity at a custom Android toolchain (JDK / SDK / NDK) instead of the one installed with
    /// the editor. Paths come from &lt;repo&gt;/tools/android-toolchain.local.txt (gitignored, one
    /// <c>key=path</c> per line: jdk, sdk, ndk). See tools/android-toolchain.example.txt.
    /// Stored by Unity per user (EditorPrefs), so it only needs to run once per machine; the build
    /// runs it too, so a fresh machine just needs the file.
    /// </summary>
    public static class AndroidToolchain
    {
        private const string TypeName = "UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions";

        public static string ConfigFile =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "android-toolchain.local.txt"));

        [MenuItem("Tools/Tabletop Tempo/Apply Android Toolchain Paths", priority = 210)]
        public static void ApplyMenu() => Apply();

        /// <summary>Batch: ./tools/unity.ps1 method -Method TabletopTempo.EditorTools.AndroidToolchain.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            bool ok = Apply();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Applies the paths if the config file exists. Returns false on a broken config.</summary>
        public static bool Apply()
        {
            if (!File.Exists(ConfigFile))
            {
                Debug.Log("[TT] No android-toolchain.local.txt: using the Android tools installed with Unity.");
                return true;
            }

            string jdk = null, sdk = null, ndk = null;
            foreach (var raw in File.ReadAllLines(ConfigFile))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                if (key == "jdk") jdk = val;
                else if (key == "sdk") sdk = val;
                else if (key == "ndk") ndk = val;
            }

            bool ok = Check(jdk, Path.Combine(jdk ?? "", "bin", "java.exe"), "jdk")
                      & Check(sdk, Path.Combine(sdk ?? "", "platform-tools"), "sdk")
                      & Check(ndk, Path.Combine(ndk ?? "", "source.properties"), "ndk");
            if (!ok) return false;

            var type = Type.GetType(TypeName);
            if (type == null)
            {
                Debug.LogError("[TT-ERROR] Android Build Support is not installed for this editor.");
                return false;
            }

            Set(type, "jdkRootPath", jdk);
            Set(type, "sdkRootPath", sdk);
            Set(type, "ndkRootPath", ndk);
            Debug.Log($"[TT] Android toolchain: JDK={Get(type, "jdkRootPath")} SDK={Get(type, "sdkRootPath")} NDK={Get(type, "ndkRootPath")}");
            return true;
        }

        private static bool Check(string root, string mustExist, string key)
        {
            if (string.IsNullOrEmpty(root))
            {
                Debug.LogError($"[TT-ERROR] android-toolchain.local.txt: missing '{key}='");
                return false;
            }
            if (!File.Exists(mustExist) && !Directory.Exists(mustExist))
            {
                Debug.LogError($"[TT-ERROR] android-toolchain.local.txt: {key} path looks wrong (no {mustExist})");
                return false;
            }
            return true;
        }

        private static void Set(Type t, string prop, string value)
        {
            var p = t.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
            if (p == null || !p.CanWrite)
            {
                Debug.LogWarning($"[TT] AndroidExternalToolsSettings.{prop} not available in this editor");
                return;
            }
            p.SetValue(null, value);
        }

        private static string Get(Type t, string prop) =>
            t.GetProperty(prop, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string ?? "?";
    }
}
