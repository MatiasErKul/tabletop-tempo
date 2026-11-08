using TabletopTempo.Core.Rhythm;
using TMPro;
using UnityEngine;

namespace TabletopTempo.Rhythm
{
    /// <summary>
    /// Text and feedback around the highway: coaching line (always text, no audio needed),
    /// Perfect/Great/Good/Close/Miss labels at the hit line, combo, results with stars, and the
    /// feedback sounds. All children of the highway so they move with it.
    /// </summary>
    public sealed class StageHud : MonoBehaviour
    {
        [Header("Set by SceneBuilder")]
        public NoteHighway highway;
        public TMP_Text coach;
        public TMP_Text combo;
        public TMP_Text results;
        public Renderer[] stars = new Renderer[3];
        public TMP_Text[] gradeLabels = new TMP_Text[6];
        public AudioSource sfx;

        [Header("Sounds")]
        public AudioClip perfect, great, good, close, miss, countHi, countLo, confirm, stageClear;

        private readonly float[] _labelAge = new float[6];
        private int _nextLabel;
        private MaterialPropertyBlock _starBlock;
        private static readonly int LaneColorId = Shader.PropertyToID("_LaneColor");

        private void Awake()
        {
            _starBlock = new MaterialPropertyBlock();
            for (int i = 0; i < gradeLabels.Length; i++)
            {
                _labelAge[i] = 99f;
                if (gradeLabels[i] != null) gradeLabels[i].gameObject.SetActive(false);
            }
            ShowResults(null);
        }

        public void SetCoach(string text)
        {
            if (coach != null) coach.text = text ?? "";
        }

        public void SetCombo(int value)
        {
            if (combo != null) combo.text = value >= 4 ? $"{value} combo" : "";
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip, volume);
        }

        /// <summary>Plays a sound exactly on a future DSP time (count-in clicks).</summary>
        public void PlayAt(AudioClip clip, double dspTime)
        {
            if (clip == null || sfx == null) return;
            var go = new GameObject("ScheduledSfx");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 0f;
            src.PlayScheduled(dspTime);
            Destroy(go, (float)(dspTime - AudioSettings.dspTime) + clip.length + 0.2f);
        }

        public void ShowJudgement(Judgement j, Lane lane)
        {
            switch (j.Grade)
            {
                case Grade.Perfect: Play(perfect); break;
                case Grade.Great: Play(great); break;
                case Grade.Good: Play(good); break;
                case Grade.Close: Play(close, 0.8f); break;
                default: Play(miss, 0.5f); break;
            }

            var label = gradeLabels[_nextLabel];
            _labelAge[_nextLabel] = 0f;
            _nextLabel = (_nextLabel + 1) % gradeLabels.Length;
            if (label == null || highway == null) return;
            label.text = j.Grade switch
            {
                Grade.Perfect => "PERFECT",
                Grade.Great => "GREAT",
                Grade.Good => "GOOD",
                Grade.Close => "CLOSE!",
                _ => "MISS",
            };
            label.color = j.Grade >= Grade.Good ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            label.transform.localPosition = highway.LanePosition(lane, 0.07f) + new Vector3(0, 0, -0.01f);
            label.gameObject.SetActive(true);
        }

        public void ShowResults(Score score)
        {
            bool on = score != null;
            if (results != null)
            {
                results.gameObject.SetActive(on);
                if (on)
                    results.text = $"<b>Stage clear!</b>\n<size=70%>Accuracy {score.Accuracy * 100f:0}%  ·  best combo {score.BestCombo}\n" +
                                   $"Perfect {score.Counts[(int)Grade.Perfect]}  Great {score.Counts[(int)Grade.Great]}  Good {score.Counts[(int)Grade.Good]}  " +
                                   $"Close {score.Counts[(int)Grade.Close]}  Miss {score.Counts[(int)Grade.Miss]}</size>";
            }
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                stars[i].gameObject.SetActive(on);
                if (!on) continue;
                bool lit = i < score.Stars;
                _starBlock.SetColor(LaneColorId, lit ? new Color(1f, 0.82f, 0.25f) : new Color(0.25f, 0.25f, 0.25f));
                stars[i].SetPropertyBlock(_starBlock);
            }
        }

        private void Update()
        {
            for (int i = 0; i < gradeLabels.Length; i++)
            {
                var l = gradeLabels[i];
                if (l == null || !l.gameObject.activeSelf) continue;
                _labelAge[i] += Time.deltaTime;
                float k = _labelAge[i] / 0.6f;
                if (k >= 1f)
                {
                    l.gameObject.SetActive(false);
                    continue;
                }
                l.transform.localPosition += new Vector3(0f, Time.deltaTime * 0.06f, 0f);
                var c = l.color;
                c.a = 1f - k * k;
                l.color = c;
            }
        }
    }
}
