using TabletopTempo.Core.Rhythm;
using TabletopTempo.Rhythm;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>Creates/updates the StageDefinition assets from generated charts + music.</summary>
    public static class StageSetup
    {
        public const string Folder = "Assets/_Project/Settings/Stages";

        public static StageDefinition Stage1() => Ensure(1, "stage1_first_claps", "First Claps",
            "Clap along with the falling cards. Clap when a card reaches the glowing line.", MoveId.Clap);

        private static StageDefinition Ensure(int number, string id, string title, string intro, MoveId tryMove)
        {
            AssetFolders.Ensure(Folder);
            string path = $"{Folder}/Stage{number}.asset";
            var s = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<StageDefinition>();
                AssetDatabase.CreateAsset(s, path);
            }
            s.number = number;
            s.title = title;
            s.intro = intro;
            s.tryMove = tryMove;
            s.chart = AssetDatabase.LoadAssetAtPath<ChartAsset>($"Assets/_Project/Charts/{id}.ttchart");
            s.music = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Importers.AssetImportRules.MusicFolder}{id}.wav");
            if (s.chart == null) Debug.LogError($"[TT-ERROR] Stage {number}: chart {id}.ttchart missing (run tools/music/ttmusic.py)");
            if (s.music == null) Debug.LogError($"[TT-ERROR] Stage {number}: music {id}.wav missing (run tools/music/ttmusic.py)");
            EditorUtility.SetDirty(s);
            return s;
        }
    }
}
