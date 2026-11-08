using TabletopTempo.Core.Input;
using UnityEngine;

namespace TabletopTempo.Input
{
    /// <summary>
    /// Draws tracked joints as small spheres (pooled; 42 renderers, one shared material). Used for
    /// providers that have no hand mesh of their own (webcam, replay) so recorded simulator footage
    /// shows the real hands. Hidden for Meta hands, which render their own mesh.
    /// </summary>
    public sealed class HandJointVisualizer : MonoBehaviour
    {
        [SerializeField] private Material jointMaterial;
        [SerializeField] private float jointSize = 0.012f;
        [SerializeField] private bool showForMetaHands = false;

        private HandInputService _service;
        private Transform[] _left, _right;

        private void Start()
        {
            _service = HandInputService.Instance;
            _left = Build("LeftJoints");
            _right = Build("RightJoints");
        }

        private Transform[] Build(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var arr = new Transform[HandData.JointCount];
            for (int i = 0; i < arr.Length; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.name = ((HandJoint)i).ToString();
                s.transform.SetParent(root, false);
                bool tip = i == 4 || i == 8 || i == 12 || i == 16 || i == 20;
                s.transform.localScale = Vector3.one * (tip ? jointSize * 1.25f : jointSize);
                var r = s.GetComponent<MeshRenderer>();
                if (jointMaterial != null) r.sharedMaterial = jointMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                arr[i] = s.transform;
            }
            root.gameObject.SetActive(false);
            return arr;
        }

        private void LateUpdate()
        {
            if (_service == null) _service = HandInputService.Instance;
            var frame = _service != null ? _service.History.Latest : null;
            bool visible = frame != null && (showForMetaHands || _service.ProviderName != "meta-hands");
            Apply(_left, visible ? frame.Left : null);
            Apply(_right, visible ? frame.Right : null);
        }

        private void Apply(Transform[] joints, HandData hand)
        {
            if (joints == null) return;
            var root = joints[0].parent.gameObject;
            bool on = hand != null && hand.Tracked;
            if (root.activeSelf != on) root.SetActive(on);
            if (!on) return;
            var table = _service.Table;
            for (int i = 0; i < joints.Length; i++) joints[i].position = table.ToWorld(hand.Joints[i]);
        }
    }
}
