using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>Android / Quest player settings. Idempotent.</summary>
    public static class PlayerSettingsSetup
    {
        public const string CompanyName = "MatiasErKul";
        public const string ProductName = "Tabletop Tempo";
        public const string AndroidPackage = "com.matiaserkul.tabletoptempo";

        public static void Apply()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, AndroidPackage);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidPackage);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";

            // Scripting / CPU
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);

            // Horizon OS requires Android 12L (API 32) as the minimum.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;

            // Graphics: Vulkan only, linear colour, landscape.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetMobileMTRendering(android, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // No splash delay beyond what the plan requires (fast cold start).
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // Input handling "Both": Input System for our editor/simulator hotkeys (F8/F9), legacy
            // still available for SDK code paths. Takes effect on the next editor start.
            var playerSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerSettingsAsset != null && playerSettingsAsset.Length > 0)
            {
                var so = new SerializedObject(playerSettingsAsset[0]);
                SerializedSetter.Set(so, "activeInputHandler", 2);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Audio: "Best latency" DSP buffer (256) for tight rhythm timing.
            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (audioManager != null && audioManager.Length > 0)
            {
                var am = new SerializedObject(audioManager[0]);
                SerializedSetter.Set(am, "m_DSPBufferSize", 256);
                am.ApplyModifiedPropertiesWithoutUndo();
            }

            // Desktop (XR Simulator / webcam dev mode)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;

            Debug.Log("[TT] Player settings applied (IL2CPP, ARM64, API 32+, Vulkan, Linear, ASTC).");
        }
    }
}
