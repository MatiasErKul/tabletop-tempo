using TabletopTempo.Core.Rhythm;
using TabletopTempo.Rhythm;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>Stage part of the generated Game scene: highway, HUD, conductor, stage runner.</summary>
    public static class SceneBuilderStage
    {
        private const string PictoFolder = "Assets/_Project/Art/Pictograms/";
        private const string MatFolder = "Assets/_Project/Art/Materials/";

        // Highway pose in table space: behind the far table edge, hit line ~16 cm above the table,
        // top ~50 cm (just below seated eye level), tilted 15° so it faces the player's eyes.
        public static readonly Vector3 HighwayPosition = new Vector3(0f, 0.16f, 0.60f);
        public static readonly Vector3 HighwayEuler = new Vector3(15f, 0f, 0f);

        public static void Build(Transform gameRoot, Transform tableAnchor)
        {
            // ---- highway
            var hwGo = new GameObject("Highway");
            hwGo.transform.SetParent(tableAnchor, false);
            hwGo.transform.localPosition = HighwayPosition;
            hwGo.transform.localEulerAngles = HighwayEuler;
            var highway = hwGo.AddComponent<NoteHighway>();
            highway.laneMaterials = new[]
            {
                CardMaterial("TT_Card_Left", "frame_left", NoteHighway.LaneColor(Lane.Left)),
                CardMaterial("TT_Card_Center", "frame_center", NoteHighway.LaneColor(Lane.Center)),
                CardMaterial("TT_Card_Right", "frame_right", NoteHighway.LaneColor(Lane.Right)),
            };
            highway.lineMaterial = ColorMaterial("TT_LaneGuide", new Color(1f, 1f, 1f, 0.18f), additive: false);
            highway.hitLineMaterial = ColorMaterial("TT_HitLine", new Color(1f, 0.95f, 0.8f, 0.7f), additive: true);
            highway.icons = new[]
            {
                new NoteHighway.MoveIcon { move = MoveId.Clap, icon = Tex("clap") },
            };

            // ---- HUD (children of the highway, local coordinates)
            var hud = hwGo.AddComponent<StageHud>();
            hud.highway = highway;
            hud.coach = Text(hwGo.transform, "Coach", new Vector3(0f, highway.travel + 0.11f, 0f), new Vector2(0.60f, 0.12f), 0.32f,
                TextAlignmentOptions.Bottom, Color.white);
            hud.combo = Text(hwGo.transform, "Combo", new Vector3(0.30f, 0.02f, 0f), new Vector2(0.16f, 0.05f), 0.22f,
                TextAlignmentOptions.Left, new Color(1f, 0.9f, 0.6f));
            hud.results = Text(hwGo.transform, "Results", new Vector3(0f, highway.travel * 0.45f, -0.02f), new Vector2(0.55f, 0.16f), 0.34f,
                TextAlignmentOptions.Center, Color.white);
            hud.gradeLabels = new TMP_Text[6];
            for (int i = 0; i < 6; i++)
                hud.gradeLabels[i] = Text(hwGo.transform, "Grade" + i, Vector3.zero, new Vector2(0.16f, 0.04f), 0.24f,
                    TextAlignmentOptions.Center, Color.white);
            hud.stars = new Renderer[3];
            var starMat = CardMaterial("TT_Star", "star", new Color(1f, 0.82f, 0.25f));
            for (int i = 0; i < 3; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(star.GetComponent<Collider>());
                star.name = "Star" + i;
                star.transform.SetParent(hwGo.transform, false);
                star.transform.localPosition = new Vector3((i - 1) * 0.085f, highway.travel * 0.80f, -0.02f);
                star.transform.localScale = Vector3.one * 0.075f;
                var r = star.GetComponent<MeshRenderer>();
                r.sharedMaterial = starMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hud.stars[i] = r;
            }

            var sfx = hwGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0.6f;   // a little spatial "pop" from the highway
            sfx.minDistance = 1.5f;
            hud.sfx = sfx;
            hud.perfect = Clip("hit_perfect");
            hud.great = Clip("hit_great");
            hud.good = Clip("hit_good");
            hud.close = Clip("hit_close");
            hud.miss = Clip("miss");
            hud.countHi = Clip("count_hi");
            hud.countLo = Clip("count_lo");
            hud.confirm = Clip("ui_confirm");
            hud.stageClear = Clip("stage_clear");

            // ---- conductor + stage runner
            var stageGo = new GameObject("Stage");
            stageGo.transform.SetParent(gameRoot, false);
            var music = stageGo.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = false;
            music.spatialBlend = 0f;
            music.priority = 0;
            var conductor = stageGo.AddComponent<Conductor>();
            conductor.Bind(music);
            var runner = stageGo.AddComponent<StageRunner>();
            runner.stage = StageSetup.Stage1();
            runner.conductor = conductor;
            runner.highway = highway;
            runner.hud = hud;

            LogTextScale(hud.coach);
        }

        // ------------------------------------------------------------------ helpers

        private static TMP_Text Text(Transform parent, string name, Vector3 localPos, Vector2 size, float fontSize,
            TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = size;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = "";
            return t;
        }

        private static void LogTextScale(TMP_Text t)
        {
            t.text = "Hg";
            t.ForceMeshUpdate();
            Debug.Log($"[TT] Text scale check: fontSize {t.fontSize} → glyph height {t.textBounds.size.y * 100f:0.0} cm");
            t.text = "";
        }

        private static Texture2D Tex(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(PictoFolder + name + ".png");
            if (t == null) Debug.LogError($"[TT-ERROR] Missing pictogram {name}.png (run tools/art/pictograms.py)");
            return t;
        }

        private static AudioClip Clip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Importers.AssetImportRules.SfxFolder + name + ".wav");
            if (c == null) Debug.LogError($"[TT-ERROR] Missing sfx {name}.wav (run tools/music/ttmusic.py sfx)");
            return c;
        }

        private static Material CardMaterial(string name, string frame, Color lane)
        {
            var m = LoadOrCreate(name, "TabletopTempo/Card");
            m.SetTexture("_FrameTex", Tex(frame));
            m.SetTexture("_IconTex", Texture2D.blackTexture);
            m.SetColor("_LaneColor", lane);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material ColorMaterial(string name, Color color, bool additive)
        {
            var m = LoadOrCreate(name, "TabletopTempo/UnlitColor");
            m.SetColor("_Color", color);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = MatFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) Debug.LogError("[TT-ERROR] Shader not found: " + shaderName);
            if (m != null)
            {
                if (shader != null && m.shader != shader) m.shader = shader;
                return m;
            }
            AssetFolders.Ensure(MatFolder.TrimEnd('/'));
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
