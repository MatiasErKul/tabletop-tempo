using System;
using System.Collections.Generic;
using TabletopTempo.Core.Rhythm;
using UnityEngine;

namespace TabletopTempo.Rhythm
{
    /// <summary>
    /// The falling move cards. A world-space panel behind the far table edge (child of the
    /// TableAnchor), tilted toward the player. Three lanes: Left · Both/Cup · Right, each with its own
    /// colour AND frame shape (circle / rounded square / diamond) so colour is never the only cue.
    /// Cards spawn <see cref="lookaheadBeats"/> ahead at the top, slide to the hit line, grow a little,
    /// then dissolve as they pass it (fast with a flash on a hit, slower on a miss).
    /// Local space: x = lanes, y = up the panel (hit line at y = 0), cards face −Z (the player).
    /// </summary>
    public sealed class NoteHighway : MonoBehaviour
    {
        [Serializable]
        public struct MoveIcon
        {
            public MoveId move;
            public Texture2D icon;
        }

        [Header("Layout (metres, local)")]
        public float laneSpacing = 0.15f;
        public float travel = 0.36f;          // hit line → spawn point
        public float cardSize = 0.10f;
        public float lookaheadBeats = 2f;

        [Header("Assets (set by SceneBuilder)")]
        public Material[] laneMaterials = new Material[3];   // Left, Center, Right (Card shader)
        public Material lineMaterial;
        public Material hitLineMaterial;
        public MoveIcon[] icons = Array.Empty<MoveIcon>();

        private const int PoolSize = 16;
        private readonly List<Card> _pool = new List<Card>();
        private readonly Dictionary<int, Card> _active = new Dictionary<int, Card>();
        private readonly Dictionary<MoveId, Texture2D> _iconMap = new Dictionary<MoveId, Texture2D>();
        private Mesh _quad;
        private Chart _chart;
        private Conductor _conductor;
        private int _nextNote;
        private Transform _cardRoot;
        private Renderer _hitLine;
        private MaterialPropertyBlock _hitLineBlock;
        private float _hitLinePulse;

        private static readonly int IconTexId = Shader.PropertyToID("_IconTex");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class Card
        {
            public Transform T;
            public MeshRenderer R;
            public MaterialPropertyBlock Block;
            public ChartNote Note;
            public bool Judged;
            public bool Hit;
            public float JudgedAt;
        }

        public static Color LaneColor(Lane lane) => lane switch
        {
            // Okabe–Ito colour-blind-safe palette.
            Lane.Left => new Color(0.00f, 0.45f, 0.70f),   // blue
            Lane.Right => new Color(0.00f, 0.62f, 0.45f),  // bluish green
            _ => new Color(0.90f, 0.62f, 0.00f),           // orange
        };

        public Vector3 LanePosition(Lane lane, float y) => new Vector3(((int)lane - 1) * laneSpacing, y, 0f);

        private void Awake()
        {
            _quad = BuildQuad();
            foreach (var i in icons)
                if (i.icon != null) _iconMap[i.move] = i.icon;
            _cardRoot = new GameObject("Cards").transform;
            _cardRoot.SetParent(transform, false);
            BuildGuides();
            for (int i = 0; i < PoolSize; i++) _pool.Add(NewCard(i));
        }

        public void Begin(Chart chart, Conductor conductor)
        {
            Clear();
            _chart = chart;
            _conductor = conductor;
            _nextNote = 0;
        }

        public void Clear()
        {
            foreach (var c in _active.Values) Release(c);
            _active.Clear();
            _chart = null;
        }

        /// <summary>Visual feedback for a judgement (called by the stage runner).</summary>
        public void OnJudged(Judgement j)
        {
            if (!_active.TryGetValue(j.NoteIndex, out var c)) return;
            c.Judged = true;
            c.Hit = j.Grade >= Grade.Good;
            c.JudgedAt = Time.time;
            if (c.Hit) _hitLinePulse = 1f;
        }

        private void Update()
        {
            if (_hitLine != null)
            {
                _hitLinePulse = Mathf.MoveTowards(_hitLinePulse, 0f, Time.deltaTime * 4f);
                _hitLineBlock.SetColor(ColorId, new Color(1f, 0.95f, 0.8f, 0.55f + 0.45f * _hitLinePulse));
                _hitLine.SetPropertyBlock(_hitLineBlock);
            }

            if (_chart == null || _conductor == null) return;
            double beat = _conductor.SongBeat;

            // Spawn.
            var notes = _chart.Notes;
            while (_nextNote < notes.Count && notes[_nextNote].Beat - beat <= lookaheadBeats)
            {
                Spawn(notes[_nextNote]);
                _nextNote++;
            }

            // Move / dissolve.
            _toRelease.Clear();
            foreach (var kv in _active)
            {
                var c = kv.Value;
                float beatsAway = (float)(c.Note.Beat - beat);
                float y = beatsAway / lookaheadBeats * travel;
                float approach = Mathf.Clamp01(1f - beatsAway / lookaheadBeats);
                float scale = cardSize * (0.85f + 0.25f * approach);
                float dissolve = 0f, glow = 0f, alpha = Mathf.Clamp01((lookaheadBeats - beatsAway) * 2f);

                if (c.Judged && c.Hit)
                {
                    float k = (Time.time - c.JudgedAt) / 0.22f;
                    y = Mathf.Max(y, 0f);
                    scale *= 1f + 0.35f * k;
                    glow = 1f - k;
                    dissolve = k;
                }
                else if (beatsAway < 0f)
                {
                    // Passed the line: fade and dissolve while sinking a little.
                    float k = Mathf.Clamp01(-beatsAway / 0.6f);
                    dissolve = k;
                    alpha *= 1f - 0.3f * k;
                }
                if (dissolve >= 1f || beatsAway < -1f)
                {
                    _toRelease.Add(kv.Key);
                    continue;
                }

                c.T.localPosition = LanePosition(c.Note.Lane, y) + new Vector3(0f, 0f, -0.002f);
                c.T.localScale = Vector3.one * scale;
                c.Block.SetFloat(DissolveId, dissolve);
                c.Block.SetFloat(GlowId, glow);
                c.Block.SetFloat(AlphaId, alpha);
                c.R.SetPropertyBlock(c.Block);
            }
            foreach (var k in _toRelease)
            {
                Release(_active[k]);
                _active.Remove(k);
            }
        }

        private readonly List<int> _toRelease = new List<int>();

        private void Spawn(ChartNote note)
        {
            Card c = null;
            foreach (var p in _pool)
                if (!p.T.gameObject.activeSelf) { c = p; break; }
            if (c == null) return; // pool exhausted: skip visual (judge still works)

            c.Note = note;
            c.Judged = false;
            c.Hit = false;
            c.R.sharedMaterial = laneMaterials[(int)note.Lane];
            c.Block.Clear();
            if (_iconMap.TryGetValue(note.Move, out var icon)) c.Block.SetTexture(IconTexId, icon);
            else c.Block.SetTexture(IconTexId, Texture2D.blackTexture);
            c.T.gameObject.SetActive(true);
            _active[note.Index] = c;
        }

        private void Release(Card c) => c.T.gameObject.SetActive(false);

        private Card NewCard(int i)
        {
            var go = new GameObject("Card" + i);
            go.transform.SetParent(_cardRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Card { T = go.transform, R = r, Block = new MaterialPropertyBlock() };
        }

        private void BuildGuides()
        {
            // Lane guide lines and the hit line.
            for (int l = 0; l < 3; l++)
            {
                var g = Strip("LaneGuide" + l, lineMaterial);
                g.transform.localPosition = LanePosition((Lane)l, travel * 0.5f) + new Vector3(0, 0, 0.003f);
                g.transform.localScale = new Vector3(0.004f, travel + cardSize, 1f);
            }
            var hit = Strip("HitLine", hitLineMaterial);
            hit.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            hit.transform.localScale = new Vector3(laneSpacing * 3f, 0.008f, 1f);
            _hitLine = hit.GetComponent<Renderer>();
            _hitLineBlock = new MaterialPropertyBlock();
        }

        private GameObject Strip(string name, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>Unit quad in XY facing −Z, UVs 0..1.</summary>
        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "TT_Quad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }
}
