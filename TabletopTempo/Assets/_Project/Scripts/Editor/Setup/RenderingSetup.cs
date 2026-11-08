using System.IO;
using TabletopTempo.Boot;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>
    /// Creates two URP assets (Quest Low / Quest High) and two matching quality levels, and makes
    /// "Quest Low" the default everywhere. Quest Low is the Quest 2 budget (72 fps).
    /// </summary>
    public static class RenderingSetup
    {
        public const string Folder = "Assets/_Project/Settings/Rendering";
        public const string RendererPath = Folder + "/TT_URP_Renderer.asset";
        public const string LowPath = Folder + "/TT_URP_QuestLow.asset";
        public const string HighPath = Folder + "/TT_URP_QuestHigh.asset";

        public static void Apply()
        {
            AssetFolders.Ensure(Folder);

            var renderer = LoadOrCreate(RendererPath, () => ScriptableObject.CreateInstance<UniversalRendererData>());
            ConfigureRenderer(renderer);

            var low = LoadOrCreate(LowPath, () => UniversalRenderPipelineAsset.Create(renderer));
            var high = LoadOrCreate(HighPath, () => UniversalRenderPipelineAsset.Create(renderer));
            ConfigurePipeline(low, highTier: false);
            ConfigurePipeline(high, highTier: true);

            GraphicsSettings.defaultRenderPipeline = low;
            ConfigureQualityLevels(low, high);

            AssetDatabase.SaveAssets();
            Debug.Log("[TT] URP assets + quality levels configured (Quest Low default).");
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> create) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureRenderer(UniversalRendererData data)
        {
            var so = new SerializedObject(data);
            // Forward rendering, no depth priming, no intermediate texture where avoidable.
            SerializedSetter.Set(so, "m_RenderingMode", 0);              // Forward
            SerializedSetter.Set(so, "m_DepthPrimingMode", 0);           // Disabled
            SerializedSetter.Set(so, "m_IntermediateTextureMode", 0);    // Auto
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static void ConfigurePipeline(UniversalRenderPipelineAsset asset, bool highTier)
        {
            var so = new SerializedObject(asset);
            SerializedSetter.Set(so, "m_MSAA", 4);                        // 4x MSAA: cheap on tile GPUs, essential for UI edges
            SerializedSetter.Set(so, "m_SupportsHDR", false);
            SerializedSetter.Set(so, "m_RenderScale", 1.0f);
            SerializedSetter.Set(so, "m_RequireDepthTexture", false);
            SerializedSetter.Set(so, "m_RequireOpaqueTexture", false);
            SerializedSetter.Set(so, "m_UseSRPBatcher", true);
            SerializedSetter.Set(so, "m_SupportsDynamicBatching", false);
            SerializedSetter.Set(so, "m_MainLightRenderingMode", 1);      // Per pixel
            SerializedSetter.Set(so, "m_AdditionalLightsRenderingMode", highTier ? 1 : 0); // High: per-pixel, Low: disabled
            SerializedSetter.Set(so, "m_AdditionalLightsPerObjectLimit", highTier ? 2 : 0);
            SerializedSetter.Set(so, "m_MainLightShadowsSupported", highTier);
            SerializedSetter.Set(so, "m_AdditionalLightShadowsSupported", false);
            SerializedSetter.Set(so, "m_ShadowDistance", highTier ? 3f : 0f);
            SerializedSetter.Set(so, "m_ShadowCascadeCount", 1);
            SerializedSetter.Set(so, "m_SoftShadowsSupported", false);
            SerializedSetter.Set(so, "m_MainLightShadowmapResolution", 1024);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ConfigureQualityLevels(RenderPipelineAsset low, RenderPipelineAsset high)
        {
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAsset == null || qualityAsset.Length == 0)
            {
                Debug.LogWarning("[TT] QualitySettings.asset not found");
                return;
            }

            var so = new SerializedObject(qualityAsset[0]);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = 2;
            ConfigureLevel(levels.GetArrayElementAtIndex(0), QualityTierApplier.LowLevelName, low);
            ConfigureLevel(levels.GetArrayElementAtIndex(1), QualityTierApplier.HighLevelName, high);
            SerializedSetter.Set(so, "m_CurrentQuality", 0);

            // Default quality per platform → Quest Low.
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var entry = perPlatform.GetArrayElementAtIndex(i);
                    var second = entry.FindPropertyRelative("second");
                    if (second != null) second.intValue = 0;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLevel(SerializedProperty level, string name, RenderPipelineAsset pipeline)
        {
            level.FindPropertyRelative("name").stringValue = name;
            SetRel(level, "customRenderPipeline", pipeline);
            SetRel(level, "vSyncCount", 0);          // XR runtime paces frames
            SetRel(level, "antiAliasing", 0);        // MSAA comes from the URP asset
            SetRel(level, "pixelLightCount", 1);
            SetRel(level, "anisotropicTextures", 0);
            SetRel(level, "softParticles", false);
            SetRel(level, "realtimeReflectionProbes", false);
            SetRel(level, "billboardsFaceCameraPosition", false);
            SetRel(level, "skinWeights", 2);         // 2 bones
            SetRel(level, "globalTextureMipmapLimit", 0);
            SetRel(level, "lodBias", 1f);
            SetRel(level, "particleRaycastBudget", 16);
        }

        private static void SetRel(SerializedProperty parent, string name, object value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) { Debug.LogWarning($"[TT] Quality level: no field '{name}'"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }
    }
}
