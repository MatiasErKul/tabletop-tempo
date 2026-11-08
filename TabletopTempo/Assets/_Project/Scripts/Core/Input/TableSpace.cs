using UnityEngine;

namespace TabletopTempo.Core.Input
{
    /// <summary>
    /// The table-local frame: origin = calibrated table point (on the surface, at the player's edge
    /// centre), +Y up (always world up), +Z away from the player. Only yaw is taken from calibration,
    /// so "up" never tilts.
    /// </summary>
    public readonly struct TableSpace
    {
        public readonly Vector3 Origin;
        public readonly Quaternion Rotation;
        private readonly Quaternion _inverse;

        public TableSpace(Vector3 origin, float yawDegrees)
        {
            Origin = origin;
            Rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _inverse = Quaternion.Inverse(Rotation);
        }

        public static TableSpace Identity => new TableSpace(Vector3.zero, 0f);

        public Vector3 ToTable(Vector3 world) => _inverse * (world - Origin);
        public Vector3 ToWorld(Vector3 table) => Origin + Rotation * table;
        public Quaternion ToTable(Quaternion world) => _inverse * world;
        public Quaternion ToWorld(Quaternion table) => Rotation * table;
        public Vector3 DirToTable(Vector3 worldDir) => _inverse * worldDir;

        /// <summary>Converts a whole frame from world (tracking) space to table space, in place.</summary>
        public void ToTableInPlace(HandFrame f)
        {
            ToTableInPlace(f.Left);
            ToTableInPlace(f.Right);
        }

        private void ToTableInPlace(HandData h)
        {
            var j = h.Joints;
            for (int i = 0; i < j.Length; i++) j[i] = ToTable(j[i]);
            h.WristRotation = ToTable(h.WristRotation);
        }
    }
}
