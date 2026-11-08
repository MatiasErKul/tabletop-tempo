#if UNITY_EDITOR
using TabletopTempo.Core.Input;
using TabletopTempo.Core.Webcam;
using TabletopTempo.Settings;
using UnityEditor;
using UnityEngine;

namespace TabletopTempo.Webcam
{
    /// <summary>
    /// Tools → Tabletop Tempo → Webcam: pick the external camera, see what MediaPipe tracks, and run the
    /// guided calibration. Works while in Play mode with webcam input active (F8 or settings).
    /// </summary>
    public sealed class WebcamWindow : EditorWindow
    {
        private static readonly int[][] Bones =
        {
            new[] { 0, 1, 2, 3, 4 }, new[] { 0, 5, 6, 7, 8 }, new[] { 9, 10, 11, 12 },
            new[] { 13, 14, 15, 16 }, new[] { 0, 17, 18, 19, 20 }, new[] { 5, 9, 13, 17 },
        };

        private float _cameraDistance = 0.5f;
        private WebcamMount _mount = WebcamMount.Overhead;

        [MenuItem("Tools/Tabletop Tempo/Webcam (MediaPipe)", priority = 310)]
        private static void Open() => GetWindow<WebcamWindow>("TT Webcam");

        private void OnEnable() => EditorApplication.update += RepaintWhilePlaying;
        private void OnDisable() => EditorApplication.update -= RepaintWhilePlaying;

        private void RepaintWhilePlaying()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            var settings = TabletopTempoSettings.Load();
            EditorGUILayout.LabelField("Webcam hand tracking (developer only, never in the APK)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var devices = WebCamTexture.devices;
                var names = new string[devices.Length];
                int current = 0;
                for (int i = 0; i < devices.Length; i++)
                {
                    names[i] = devices[i].name;
                    if (devices[i].name == settings.webcamDeviceName) current = i;
                }
                if (devices.Length == 0) EditorGUILayout.HelpBox("No cameras found.", MessageType.Warning);
                else
                {
                    int pick = EditorGUILayout.Popup("Camera", current, names);
                    if (names[pick] != settings.webcamDeviceName)
                    {
                        settings.webcamDeviceName = names[pick];
                        EditorUtility.SetDirty(settings);
                    }
                }
                bool webcam = EditorGUILayout.Toggle(new GUIContent("Use webcam on Play", "Same as TabletopTempoSettings.webcamMode. F8 toggles during Play."), settings.webcamMode);
                if (webcam != settings.webcamMode)
                {
                    settings.webcamMode = webcam;
                    EditorUtility.SetDirty(settings);
                }
            }

            _mount = (WebcamMount)EditorGUILayout.EnumPopup(new GUIContent("Camera mount", "Overhead (~50 cm above the desk, looking down) works best."), _mount);
            _cameraDistance = EditorGUILayout.Slider(new GUIContent(_mount == WebcamMount.Overhead ? "Camera height (m)" : "Camera distance (m)",
                "Rough distance from the camera to the table (overhead) or to your hands (front)."), _cameraDistance, 0.25f, 1.2f);

            var p = WebcamMediaPipeProvider.Active;
            if (!EditorApplication.isPlaying || p == null)
            {
                EditorGUILayout.HelpBox("Enter Play mode with webcam input (toggle above, or F8 in Play) to see the camera and calibrate.\n" +
                                        "Setup once: python tools/webcam/fetch_mediapipe.py", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Status", $"{p.Status} · {p.InferenceFps:0} results/s · calibration: {(p.Calibration != null && p.Calibration.valid ? p.Calibration.mount + (p.Calibration.swapHandedness ? ", L/R swapped" : "") : "none")}");

            var cal = p.Calibrator;
            EditorGUILayout.HelpBox(cal.Message, cal.Running ? MessageType.Warning : MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!cal.Running && GUILayout.Button("Calibrate")) cal.Begin(_mount, _cameraDistance);
                if (cal.Running && GUILayout.Button("Cancel")) cal.Cancel();
            }
            if (cal.Running)
            {
                var r = GUILayoutUtility.GetRect(10, 8, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(r, cal.HoldProgress, "hold still");
            }

            DrawCamera(p);
        }

        private void DrawCamera(WebcamMediaPipeProvider p)
        {
            var tex = p.CameraTexture;
            if (tex == null || tex.width < 32) return;
            float aspect = tex.width / (float)tex.height;
            float w = position.width - 12f;
            float h = w / aspect;
            var rect = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);

            Handles.BeginGUI();
            foreach (var obs in p.Latest)
            {
                if (!obs.Present) continue;
                var side = p.Calibration != null ? WebcamHandMapper.SideOf(obs, p.Calibration) : HandSide.Left;
                Handles.color = side == HandSide.Left ? new Color(0.3f, 0.75f, 1f) : new Color(1f, 0.6f, 0.2f);
                foreach (var chain in Bones)
                    for (int i = 1; i < chain.Length; i++)
                        Handles.DrawLine(ToRect(rect, obs.Image[chain[i - 1]], aspect), ToRect(rect, obs.Image[chain[i]], aspect));
                Handles.DrawSolidDisc(ToRect(rect, obs.Image[(int)HandJoint.IndexTip], aspect), Vector3.forward, 4f);
            }
            Handles.EndGUI();
        }

        private static Vector3 ToRect(Rect r, Vector2 uv, float aspect) =>
            new Vector3(r.x + uv.x / aspect * r.width, r.y + uv.y * r.height, 0f);
    }
}
#endif
