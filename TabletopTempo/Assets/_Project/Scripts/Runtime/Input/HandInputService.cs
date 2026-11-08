using System;
using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Input
{
    /// <summary>
    /// The single place gameplay gets hands from. Each frame: provider → table space → shape scores →
    /// history → <see cref="FrameReady"/>. Providers are swapped at runtime (device, webcam, replay).
    /// Time stamps use the audio DSP clock so the conductor can judge them directly.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class HandInputService : MonoBehaviour
    {
        public static HandInputService Instance { get; private set; }

        [SerializeField] private HandShapeTuningAsset tuning;

        private IHandFrameProvider _provider;
        private readonly HandFrame _scratch = new HandFrame();
        private TableSpace _table = TableSpace.Identity;

        public HandFrameHistory History { get; } = new HandFrameHistory(120);
        public IHandFrameProvider Provider => _provider;
        public string ProviderName => _provider?.Name ?? "none";
        public TableSpace Table => _table;

        /// <summary>Raised after each new frame is in <see cref="History"/>.</summary>
        public event Action<HandFrame> FrameReady;

        /// <summary>Raised when the provider changes (debug overlay, visualisers).</summary>
        public event Action<IHandFrameProvider> ProviderChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetProvider(IHandFrameProvider provider)
        {
            _provider = provider;
            History.Clear();
            Debug.Log($"[TT] Hand provider: {ProviderName}");
            ProviderChanged?.Invoke(provider);
        }

        /// <summary>Set by table calibration (M5). Until then the TableAnchor default is used.</summary>
        public void SetTable(TableSpace table)
        {
            _table = table;
            History.Clear();
        }

        private void Update()
        {
            if (_provider == null) return;
            double now = AudioSettings.dspTime;
            if (!_provider.Write(_scratch, now)) return;
            Process(_scratch, !_provider.OutputsTableSpace);
        }

        /// <summary>Processes one frame. Public so tests and recorders can push frames directly.</summary>
        public void Process(HandFrame frame, bool convertFromWorld)
        {
            if (convertFromWorld) _table.ToTableInPlace(frame);
            var t = tuning != null ? tuning.tuning : HandShapeTuning.Default;
            HandShape.Compute(frame.Left, t);
            HandShape.Compute(frame.Right, t);
            History.Push(frame);
            FrameReady?.Invoke(History.Latest);
        }
    }
}
