using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>
    /// XR configuration: OpenXR loader for Android + Standalone (XR Simulator), Meta XR feature set,
    /// and the Meta project config (devices, hands-only, passthrough, scene). Idempotent.
    /// Run AFTER Meta's own Project Setup fixes so our explicit choices win.
    /// </summary>
    public static class MetaProjectSetup
    {
        private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string MetaXRFeatureId = "com.meta.openxr.feature.metaxr";
        private const string MetaQuestFeatureId = "com.unity.openxr.feature.metaquest";

        public static void Apply()
        {
            AssignLoader(BuildTargetGroup.Android);
            AssignLoader(BuildTargetGroup.Standalone);
            EnableFeature(BuildTargetGroup.Android, MetaXRFeatureId);
            EnableFeature(BuildTargetGroup.Standalone, MetaXRFeatureId);
            EnableFeature(BuildTargetGroup.Android, MetaQuestFeatureId);
            ConfigureOpenXRSettings(BuildTargetGroup.Android);
            ConfigureProjectConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[TT] Meta XR / OpenXR configured.");
        }

        private static void AssignLoader(BuildTargetGroup group)
        {
            var perTarget = GetOrCreatePerBuildTarget();
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var manager = general.AssignedSettings;
            bool ok = XRPackageMetadataStore.AssignLoader(manager, OpenXRLoader, group);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            Debug.Log($"[TT] OpenXR loader on {group}: {(ok ? "assigned" : "already assigned / failed")}");
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTarget()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget != null) return perTarget;

            AssetFolders.Ensure("Assets/XR");
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            return perTarget;
        }

        private static void EnableFeature(BuildTargetGroup group, string featureId)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
            if (feature == null)
            {
                Debug.LogWarning($"[TT] OpenXR feature '{featureId}' not found for {group}");
                return;
            }
            if (!feature.enabled)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
        }

        private static void ConfigureOpenXRSettings(BuildTargetGroup group)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) { Debug.LogWarning($"[TT] No OpenXR settings for {group}"); return; }
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced; // multiview on Quest
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureProjectConfig()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>
            {
                OVRProjectConfig.DeviceType.Quest2,
                OVRProjectConfig.DeviceType.Quest3,
                OVRProjectConfig.DeviceType.Quest3S,
            };

            // Hands-first: the whole game is hand poses. "HandsOnly" marks hand tracking as required
            // in the manifest, so the app launches without controllers.
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.HandsOnly;
            // Fast Motion Mode: better tracking for claps/pounds. Watch its cost on Quest 2 (PERFORMANCE.md).
            config.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;

            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            // Space Setup TABLE is an optional shortcut; touch calibration is the main path.
            config.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
            config.anchorSupport = OVRProjectConfig.AnchorSupport.Enabled;

            // No camera permission in the default build (privacy, section 9).
            config.isPassthroughCameraAccessEnabled = false;
            config.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.None;
            config.faceTrackingSupport = OVRProjectConfig.FeatureSupport.None;
            config.eyeTrackingSupport = OVRProjectConfig.FeatureSupport.None;
            config.requiresSystemKeyboard = false;
            config.focusAware = true;

            OVRProjectConfig.CommitProjectConfig(config);
        }
    }
}
