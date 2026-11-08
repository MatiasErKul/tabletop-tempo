using System;
using System.Diagnostics;
using System.IO;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Experimental;
using TabletopTempo.Core.Input;
using TabletopTempo.Core.Webcam;
using TabletopTempo.Settings;
using UnityEngine;
using Debug = UnityEngine.Debug;
using RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;

namespace TabletopTempo.Webcam
{
    /// <summary>
    /// Developer-only hand provider: an external webcam + MediaPipe HandLandmarker (LIVE_STREAM, CPU).
    /// Never part of the Quest build (assembly is Editor/desktop only and needs the fetched package).
    /// Outputs table space directly using <see cref="WebcamCalibration"/>; joints are One-Euro filtered.
    /// Frame times are the CAPTURE time on the DSP clock, so tracker latency doesn't shift judging.
    /// </summary>
    public sealed class WebcamMediaPipeProvider : MonoBehaviour, IHandFrameProvider
    {
        public const int MaxHands = 2;

        public static WebcamMediaPipeProvider Active { get; private set; }

        public string Name => "webcam-mediapipe";
        public bool OutputsTableSpace => true;

        public WebCamTexture CameraTexture => _webcam;
        public WebcamCalibration Calibration => _calibration;
        public WebcamCalibrator Calibrator { get; private set; }
        public string Status { get; private set; } = "starting";
        public float InferenceFps { get; private set; }

        /// <summary>Latest raw observations (main-thread copy), for the editor window overlay.</summary>
        public readonly WebcamHandObservation[] Latest = { new WebcamHandObservation(), new WebcamHandObservation() };

        private WebCamTexture _webcam;
        private HandLandmarker _landmarker;
        private TextureFramePool _pool;
        private WebcamCalibration _calibration;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastTimestampMs = -1;

        // Shared with the MediaPipe callback thread.
        private readonly object _lock = new object();
        private readonly WebcamHandObservation[] _incoming = { new WebcamHandObservation(), new WebcamHandObservation() };
        private int _incomingSeq, _consumedSeq;
        private double _incomingCaptureDsp;
        private readonly long[] _stampMs = new long[64];
        private readonly double[] _stampDsp = new double[64];
        private int _stampHead;
        private int _resultsThisSecond;
        private float _fpsWindowStart;

        private OneEuroFilter3[] _filtersL, _filtersR;

        public static string CalibrationPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSettings", "TabletopTempoWebcam.json"));

        public static string ModelPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "WebcamModels", "hand_landmarker.task"));

        private void OnEnable()
        {
            Active = this;
            _filtersL = NewFilters();
            _filtersR = NewFilters();
            _calibration = LoadCalibration();
            Calibrator = new WebcamCalibrator(this);
            StartCamera();
            StartLandmarker();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            try { _landmarker?.Close(); } catch (Exception e) { Debug.LogWarning("[TT] Webcam: closing landmarker: " + e.Message); }
            _landmarker = null;
            if (_webcam != null)
            {
                _webcam.Stop();
                Destroy(_webcam);
                _webcam = null;
            }
            _pool?.Dispose();
            _pool = null;
        }

        private static OneEuroFilter3[] NewFilters()
        {
            var f = new OneEuroFilter3[HandData.JointCount];
            for (int i = 0; i < f.Length; i++) f[i] = new OneEuroFilter3(1.5f, 4f);
            return f;
        }

        // ------------------------------------------------------------------ setup

        private void StartCamera()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Status = "no webcam found";
                Debug.LogWarning("[TT] Webcam: no camera devices.");
                return;
            }
            string wanted = TabletopTempoSettings.Load().webcamDeviceName;
            string name = devices[0].name;
            foreach (var d in devices)
                if (!string.IsNullOrEmpty(wanted) && d.name == wanted) name = d.name;

            _webcam = new WebCamTexture(name, 640, 480, 30);
            _webcam.Play();
            Status = "camera: " + name;
            Debug.Log($"[TT] Webcam: using '{name}' ({devices.Length} device(s) found)");
        }

        private void StartLandmarker()
        {
            if (!File.Exists(ModelPath))
            {
                Status = "model missing: run tools/webcam/fetch_mediapipe.py";
                Debug.LogError("[TT-ERROR] Webcam: " + Status);
                return;
            }
            try
            {
                var options = new HandLandmarkerOptions(
                    new Mediapipe.Tasks.Core.BaseOptions(Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU, modelAssetPath: ModelPath),
                    runningMode: RunningMode.LIVE_STREAM,
                    numHands: MaxHands,
                    minHandDetectionConfidence: 0.5f,
                    minHandPresenceConfidence: 0.5f,
                    minTrackingConfidence: 0.5f,
                    resultCallback: OnResult);
                _landmarker = HandLandmarker.CreateFromOptions(options);
            }
            catch (Exception e)
            {
                Status = "MediaPipe failed: " + e.Message;
                Debug.LogError("[TT-ERROR] Webcam: " + Status);
            }
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (_webcam == null || _landmarker == null || !_webcam.isPlaying || !_webcam.didUpdateThisFrame) return;
            if (_webcam.width < 32) return; // not started yet

            if (_pool == null) _pool = new TextureFramePool(_webcam.width, _webcam.height, TextureFormat.RGBA32, 4);
            if (!_pool.TryGetTextureFrame(out var frame)) return;

            long ts = _clock.ElapsedMilliseconds;
            if (ts <= _lastTimestampMs) ts = _lastTimestampMs + 1; // LIVE_STREAM needs increasing timestamps
            _lastTimestampMs = ts;
            Remember(ts, AudioSettings.dspTime);

            frame.ReadTextureOnCPU(_webcam, flipHorizontally: false, flipVertically: !_webcam.videoVerticallyMirrored);
            var image = frame.BuildCPUImage();
            frame.Release();
            _landmarker.DetectAsync(image, ts);

            Calibrator.Tick(Time.unscaledTime);
        }

        private void Remember(long ms, double dsp)
        {
            lock (_lock)
            {
                _stampMs[_stampHead] = ms;
                _stampDsp[_stampHead] = dsp;
                _stampHead = (_stampHead + 1) % _stampMs.Length;
            }
        }

        /// <summary>MediaPipe callback (worker thread): copy into the incoming buffers only.</summary>
        private void OnResult(HandLandmarkerResult result, Mediapipe.Image image, long timestampMs)
        {
            lock (_lock)
            {
                double dsp = 0;
                for (int i = 0; i < _stampMs.Length; i++)
                    if (_stampMs[i] == timestampMs) { dsp = _stampDsp[i]; break; }
                _incomingCaptureDsp = dsp;

                int n = result.handLandmarks?.Count ?? 0;
                float aspect = image != null && image.Height() > 0 ? image.Width() / (float)image.Height() : 4f / 3f;
                for (int h = 0; h < MaxHands; h++)
                {
                    var obs = _incoming[h];
                    obs.Present = h < n;
                    if (!obs.Present) continue;

                    var cat = result.handedness[h].categories;
                    obs.LabelLeft = cat.Count > 0 && cat[0].categoryName == "Left";
                    obs.Score = cat.Count > 0 ? cat[0].score : 0.5f;

                    var img = result.handLandmarks[h].landmarks;
                    var world = result.handWorldLandmarks[h].landmarks;
                    for (int j = 0; j < HandData.JointCount && j < img.Count; j++)
                    {
                        obs.Image[j] = new Vector2(img[j].x * aspect, img[j].y);
                        obs.World[j] = j < world.Count ? new Vector3(world[j].x, world[j].y, world[j].z) : Vector3.zero;
                    }
                }
                _incomingSeq++;
                _resultsThisSecond++;
            }
        }

        public bool Write(HandFrame target, double inputTime)
        {
            double capture;
            lock (_lock)
            {
                if (_incomingSeq == _consumedSeq) return false;
                _consumedSeq = _incomingSeq;
                for (int h = 0; h < MaxHands; h++) Latest[h].CopyFrom(_incoming[h]);
                capture = _incomingCaptureDsp > 0 ? _incomingCaptureDsp : inputTime;
                if (Time.unscaledTime - _fpsWindowStart >= 1f)
                {
                    InferenceFps = _resultsThisSecond / Mathf.Max(0.001f, Time.unscaledTime - _fpsWindowStart);
                    _resultsThisSecond = 0;
                    _fpsWindowStart = Time.unscaledTime;
                }
            }

            target.Time = capture;
            target.Left.SetUntracked();
            target.Right.SetUntracked();
            if (_calibration == null || !_calibration.valid) return true; // visible in the window, not in the game

            AssignAndMap(target, capture);
            return true;
        }

        private void AssignAndMap(HandFrame target, double time)
        {
            WebcamHandObservation left = null, right = null;
            for (int h = 0; h < MaxHands; h++)
            {
                var o = Latest[h];
                if (!o.Present) continue;
                var side = WebcamHandMapper.SideOf(o, _calibration);
                if (side == HandSide.Left && left == null) left = o;
                else if (side == HandSide.Right && right == null) right = o;
                else if (left == null) left = o;
                else if (right == null) right = o;
            }

            MapFiltered(left, target.Left, _filtersL, time);
            MapFiltered(right, target.Right, _filtersR, time);
        }

        private void MapFiltered(WebcamHandObservation obs, HandData dst, OneEuroFilter3[] filters, double time)
        {
            if (obs == null)
            {
                for (int i = 0; i < filters.Length; i++) filters[i].Reset();
                dst.SetUntracked();
                return;
            }
            WebcamHandMapper.Map(obs, _calibration, dst);
            for (int i = 0; i < HandData.JointCount; i++) dst.Joints[i] = filters[i].Filter(dst.Joints[i], time);
        }

        // ------------------------------------------------------------------ calibration storage

        public void SaveCalibration(WebcamCalibration cal)
        {
            _calibration = cal;
            Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath));
            File.WriteAllText(CalibrationPath, cal.ToJson());
            Debug.Log("[TT] Webcam calibration saved: " + CalibrationPath);
        }

        private static WebcamCalibration LoadCalibration()
        {
            var settings = TabletopTempoSettings.Load();
            try
            {
                if (File.Exists(CalibrationPath))
                {
                    var cal = WebcamCalibration.FromJson(File.ReadAllText(CalibrationPath));
                    if (cal != null) return cal;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TT] Webcam calibration unreadable, recalibrate: " + e.Message);
            }
            return new WebcamCalibration { mount = (WebcamMount)(int)settings.webcamMount, valid = false };
        }
    }
}
