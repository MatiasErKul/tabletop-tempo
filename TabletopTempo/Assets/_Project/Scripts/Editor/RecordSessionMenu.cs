using TabletopTempo.Input;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools
{
    /// <summary>Tools/Tabletop Tempo/Record Session: start/stop recording the live hand stream (play mode).</summary>
    public static class RecordSessionMenu
    {
        private const string MenuPath = "Tools/Tabletop Tempo/Record Session (F10)";

        [MenuItem(MenuPath, priority = 300)]
        private static void Toggle()
        {
            var rec = SessionRecorder.Instance;
            if (rec == null)
            {
                Debug.LogWarning("[TT] Enter play mode in Game/Boot first: no SessionRecorder in the scene.");
                return;
            }
            rec.Toggle();
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, SessionRecorder.Instance != null && SessionRecorder.Instance.IsRecording);
            return Application.isPlaying;
        }
    }
}
