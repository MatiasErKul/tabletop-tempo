using System;
using UnityEngine;

namespace TabletopTempo.Core.Webcam
{
    public enum WebcamMount
    {
        /// <summary>Camera above the desk looking down (recommended, ~50 cm up).</summary>
        Overhead = 0,
        /// <summary>Camera on the far side of the desk, facing the player.</summary>
        Front = 1,
    }

    /// <summary>
    /// Maps a single webcam's view into table space (pinhole model).
    ///
    /// Reference plane in table space: Overhead → the table surface (axes X/Z); Front → the vertical
    /// plane z = <see cref="FrontPlaneZ"/> (axes X/Y). The camera looks straight at that plane from
    /// <see cref="cameraDistance"/> away. Three calibration touches give a 2D affine image → plane;
    /// from it follow the camera position, the image axes in table space and the focal length.
    ///
    /// A hand's distance from the camera is solved per frame from MediaPipe's metric 3D landmarks
    /// and its 2D landmarks (linear least squares, see <see cref="WebcamHandMapper.RawPalmDepth"/>),
    /// then corrected by <see cref="handScale"/> (learned at calibration: real hands differ in size
    /// from MediaPipe's assumption).
    ///
    /// Image coordinates are aspect-corrected: u in [0, width/height], v in [0, 1], v pointing down.
    /// </summary>
    [Serializable]
    public sealed class WebcamCalibration
    {
        public const float FrontPlaneZ = 0.20f;

        public WebcamMount mount = WebcamMount.Overhead;
        [Tooltip("Distance from the camera to the reference plane, metres (overhead: camera height above the table). Measure it.")]
        public float cameraDistance = 0.50f;
        [Tooltip("Image aspect (width / height) the calibration was made with.")]
        public float aspect = 4f / 3f;
        [Tooltip("How far the palm centre sits off the plane (toward the camera) while the fingertip touches a calibration target.")]
        public float referenceHeight = 0.025f;
        [Tooltip("MediaPipe labels hands as if the image were mirrored; set when its Left/Right are swapped.")]
        public bool swapHandedness = true;

        // Affine image(u,v) → plane(a,b):  a = m00 u + m01 v + t0,  b = m10 u + m11 v + t1
        public float m00 = 1f, m01, t0, m10, m11 = 1f, t1;
        [Tooltip("Focal length in image heights (derived from the affine scale and cameraDistance).")]
        public float focal = 1f;
        [Tooltip("Real hand size / MediaPipe's assumed size, learned at calibration.")]
        public float handScale = 1f;
        public bool valid;

        // ------------------------------------------------------------------ plane geometry

        /// <summary>Plane coordinates (a, b) → table-space point on the reference plane.</summary>
        public Vector3 PlaneToTable(float a, float b) =>
            mount == WebcamMount.Overhead ? new Vector3(a, 0f, b) : new Vector3(a, b, FrontPlaneZ);

        /// <summary>Table-space point → plane coordinates (a, b) (ignores the depth axis).</summary>
        public Vector2 TableToPlane(Vector3 p) =>
            mount == WebcamMount.Overhead ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);

        /// <summary>Unit vector from the camera toward the plane (table space) = camera forward.</summary>
        public Vector3 ViewDirection => mount == WebcamMount.Overhead ? Vector3.down : Vector3.back;

        public Vector2 PrincipalPoint => new Vector2(aspect * 0.5f, 0.5f);

        public Vector2 ImageToPlane(Vector2 uv) =>
            new Vector2(m00 * uv.x + m01 * uv.y + t0, m10 * uv.x + m11 * uv.y + t1);

        public Vector3 ImageToTableOnPlane(Vector2 uv)
        {
            var ab = ImageToPlane(uv);
            return PlaneToTable(ab.x, ab.y);
        }

        /// <summary>Camera position (table space): behind the image centre, at cameraDistance.</summary>
        public Vector3 CameraPosition => ImageToTableOnPlane(PrincipalPoint) - ViewDirection * cameraDistance;

        /// <summary>Table-space point seen at <paramref name="uv"/>, <paramref name="depth"/> metres in front of the camera.</summary>
        public Vector3 ImageToTableAtDepth(Vector2 uv, float depth)
        {
            Vector3 onPlane = ImageToTableOnPlane(uv);
            Vector3 cam = CameraPosition;
            float ratio = Mathf.Clamp(depth / Mathf.Max(1e-3f, cameraDistance), 0.15f, 2.5f);
            return cam + (onPlane - cam) * ratio;
        }

        /// <summary>
        /// Table-space directions of the camera axes: image +u (right), image +v (down) and forward.
        /// Used to rotate MediaPipe's metric world landmarks into table space.
        /// </summary>
        public void ImageAxes(out Vector3 right, out Vector3 down, out Vector3 forward)
        {
            Vector3 o = ImageToTableOnPlane(Vector2.zero);
            right = (ImageToTableOnPlane(Vector2.right) - o).normalized;
            down = (ImageToTableOnPlane(Vector2.up) - o).normalized;
            forward = ViewDirection;
        }

        // ------------------------------------------------------------------ solving

        /// <summary>Default table-space calibration targets for each mount (what the player touches).</summary>
        public static Vector3[] Targets(WebcamMount mount) => mount == WebcamMount.Overhead
            ? new[] { new Vector3(-0.20f, 0f, 0.10f), new Vector3(0.20f, 0f, 0.10f), new Vector3(0f, 0f, 0.35f) }
            : new[] { new Vector3(-0.20f, 0f, FrontPlaneZ), new Vector3(0.20f, 0f, FrontPlaneZ), new Vector3(0f, 0.30f, FrontPlaneZ) };

        /// <summary>
        /// Solves the affine from three fingertip image points to three table-space targets, the focal
        /// length, and (if hand observations are given) the hand scale. Returns false if the points are
        /// (nearly) collinear.
        /// </summary>
        public bool Solve(Vector2[] image, Vector3[] targets, WebcamHandObservation[] hands = null)
        {
            if (image == null || targets == null || image.Length < 3 || targets.Length < 3) return false;
            var ab = new Vector2[3];
            for (int i = 0; i < 3; i++) ab[i] = TableToPlane(targets[i]);

            // [u v 1] rows; solve for (m00, m01, t0) and (m10, m11, t1).
            float det = Det3(image[0].x, image[0].y, 1f, image[1].x, image[1].y, 1f, image[2].x, image[2].y, 1f);
            if (Mathf.Abs(det) < 1e-6f)
            {
                valid = false;
                return false;
            }

            Vector3 rowA = SolveRow(image, ab[0].x, ab[1].x, ab[2].x, det);
            Vector3 rowB = SolveRow(image, ab[0].y, ab[1].y, ab[2].y, det);
            m00 = rowA.x; m01 = rowA.y; t0 = rowA.z;
            m10 = rowB.x; m11 = rowB.y; t1 = rowB.z;

            // Metres on the plane per image height = cameraDistance / focal.
            float metresPerImageHeight = 0.5f * (new Vector2(m00, m10).magnitude + new Vector2(m01, m11).magnitude);
            focal = metresPerImageHeight > 1e-6f ? cameraDistance / metresPerImageHeight : 1f;

            handScale = 1f;
            if (hands != null)
            {
                float sum = 0f;
                int n = 0;
                float expected = cameraDistance - referenceHeight; // palm distance while touching a target
                foreach (var h in hands)
                {
                    if (h == null || !h.Present) continue;
                    float raw = WebcamHandMapper.RawPalmDepth(h, focal, PrincipalPoint);
                    if (raw > 1e-3f) { sum += expected / raw; n++; }
                }
                if (n > 0) handScale = Mathf.Clamp(sum / n, 0.6f, 1.6f);
            }

            valid = true;
            return true;
        }

        private static Vector3 SolveRow(Vector2[] p, float y0, float y1, float y2, float det)
        {
            // Cramer's rule on [u v 1] x = y
            float dx = Det3(y0, p[0].y, 1f, y1, p[1].y, 1f, y2, p[2].y, 1f);
            float dy = Det3(p[0].x, y0, 1f, p[1].x, y1, 1f, p[2].x, y2, 1f);
            float dz = Det3(p[0].x, p[0].y, y0, p[1].x, p[1].y, y1, p[2].x, p[2].y, y2);
            return new Vector3(dx / det, dy / det, dz / det);
        }

        internal static float Det3(float a, float b, float c, float d, float e, float f, float g, float h, float i) =>
            a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

        public string ToJson() => JsonUtility.ToJson(this, true);
        public static WebcamCalibration FromJson(string json) => JsonUtility.FromJson<WebcamCalibration>(json);
    }
}
