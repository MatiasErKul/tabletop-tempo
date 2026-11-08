using TabletopTempo.Boot;
using TabletopTempo.Diagnostics;
using TabletopTempo.Input;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>
    /// Generates Boot.unity and Game.unity from code. Never hand-edit these scenes: change this
    /// builder and run Tools/Tabletop Tempo/Rebuild Scenes (or the batch SetupProject entry).
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootPath = ScenesFolder + "/Boot.unity";
        public const string GamePath = ScenesFolder + "/Game.unity";

        // Seated layout, in metres, floor-level tracking origin. A typical seated eye height is
        // ~1.15–1.25 m and a table top ~0.72–0.76 m. Real values come from table calibration (M5);
        // these are only the defaults before calibration.
        public static readonly Vector3 DefaultTablePoint = new Vector3(0f, 0.74f, 0.40f);

        [MenuItem("Tools/Tabletop Tempo/Rebuild Scenes", priority = 100)]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Rebuild();
            EditorSceneManager.OpenScene(GamePath);
        }

        public static void Rebuild()
        {
            AssetFolders.Ensure(ScenesFolder);
            BuildBoot();
            BuildGame();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[TT] Scenes rebuilt: Boot → Game");
        }

        private static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();
            new GameObject("AppBootstrap").AddComponent<AppBootstrap>();
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        private static void BuildGame()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureLighting();

            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // XR rig, passthrough, hands, MRUK (Meta-specific part lives in MetaSceneSetup).
            MetaSceneSetup.BuildRig(scene);

            // Game root: everything gameplay hangs off a table-space anchor.
            var gameRoot = new GameObject("GameRoot");
            var tableAnchor = new GameObject("TableAnchor");
            tableAnchor.transform.SetParent(gameRoot.transform, false);
            tableAnchor.transform.localPosition = DefaultTablePoint;

            BuildStatusPanel(tableAnchor.transform);
            BuildInput(gameRoot.transform, tableAnchor.transform);
            BuildDebugOverlay(tableAnchor.transform);

            // Allow running Game.unity directly in the editor (without Boot).
            new GameObject("CapabilityBootstrapFallback").AddComponent<CapabilityBootstrapFallback>();

            EditorSceneManager.SaveScene(scene, GamePath);
        }

        private static void ConfigureLighting()
        {
            // Passthrough MR: no skybox, flat ambient, nothing baked.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.fog = false;
            Lightmapping.lightingSettings = GetOrCreateLightingSettings();
        }

        private static LightingSettings GetOrCreateLightingSettings()
        {
            const string path = "Assets/_Project/Settings/Rendering/TT_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls != null) return ls;
            AssetFolders.Ensure("Assets/_Project/Settings/Rendering");
            ls = new LightingSettings { bakedGI = false, realtimeGI = false, autoGenerate = false };
            AssetDatabase.CreateAsset(ls, path);
            return ls;
        }

        /// <summary>Small world-space status card above the far table edge (M1 smoke test, replaced in M4).</summary>
        private static void BuildStatusPanel(Transform tableAnchor)
        {
            var panel = new GameObject("StatusPanel");
            panel.transform.SetParent(tableAnchor, false);
            // Behind the far table edge, slightly below eye level, tilted toward the player.
            panel.transform.localPosition = new Vector3(0f, 0.30f, 0.30f);
            panel.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(panel.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            bg.transform.localScale = new Vector3(0.42f, 0.20f, 1f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = PanelMaterial();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panel.transform, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(0.40f, 0.18f);
            tmp.fontSize = 0.18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "Tabletop Tempo";

            panel.AddComponent<CapabilityReadout>().Bind(tmp);
        }

        /// <summary>Hand input: service (provider → table space → history), provider picker, joint visuals, recorder.</summary>
        private static void BuildInput(Transform gameRoot, Transform tableAnchor)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(gameRoot, false);
            var service = go.AddComponent<HandInputService>();
            var so = new SerializedObject(service);
            so.FindProperty("tuning").objectReferenceValue = GetHandShapeTuningAsset();
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<InputRig>().Bind(tableAnchor);
            go.AddComponent<SessionRecorder>();

            var vis = go.AddComponent<HandJointVisualizer>();
            var vso = new SerializedObject(vis);
            vso.FindProperty("jointMaterial").objectReferenceValue = UnlitMaterial("TT_HandJoint", new Color(0.95f, 0.85f, 0.55f, 1f));
            vso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildDebugOverlay(Transform tableAnchor)
        {
            var go = new GameObject("DevOverlay");
            go.transform.SetParent(tableAnchor, false);
            // To the right of the status card, angled toward the player.
            go.transform.localPosition = new Vector3(0.36f, 0.30f, 0.26f);
            go.transform.localRotation = Quaternion.Euler(15f, 30f, 0f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(0.34f, 0.22f);
            tmp.fontSize = 0.12f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.75f, 1f, 0.75f);
            tmp.text = "DEBUG";
            go.AddComponent<DevOverlay>().Bind(tmp);
        }

        private static HandShapeTuningAsset GetHandShapeTuningAsset()
        {
            const string path = "Assets/_Project/Settings/HandShapeTuning.asset";
            var a = AssetDatabase.LoadAssetAtPath<HandShapeTuningAsset>(path);
            if (a != null) return a;
            AssetFolders.Ensure("Assets/_Project/Settings");
            a = ScriptableObject.CreateInstance<HandShapeTuningAsset>();
            a.tuning = Core.Input.HandShapeTuning.Default;
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static Material UnlitMaterial(string name, Color color)
        {
            string path = $"Assets/_Project/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            AssetFolders.Ensure("Assets/_Project/Art/Materials");
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material PanelMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/TT_PanelBackground.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            AssetFolders.Ensure("Assets/_Project/Art/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader) { name = "TT_PanelBackground" };
            // Dark, slightly warm, opaque: readable over both greyscale and colour passthrough.
            mat.SetColor("_BaseColor", new Color(0.08f, 0.07f, 0.10f, 1f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
