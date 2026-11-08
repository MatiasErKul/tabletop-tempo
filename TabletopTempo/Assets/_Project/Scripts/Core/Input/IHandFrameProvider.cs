namespace TabletopTempo.Core.Input
{
    /// <summary>Where hands come from. Implementations: Meta device/simulator, webcam (editor), replay (tests).</summary>
    public interface IHandFrameProvider
    {
        string Name { get; }

        /// <summary>
        /// True if <see cref="Write"/> writes positions already in table space (replays), false if in
        /// world/tracking space (live trackers; the input service converts them).
        /// </summary>
        bool OutputsTableSpace { get; }

        /// <summary>
        /// Fills raw joints + tracked/confidence for both hands at <paramref name="inputTime"/>.
        /// Must not allocate. Returns false if no new data this frame.
        /// </summary>
        bool Write(HandFrame target, double inputTime);
    }
}
