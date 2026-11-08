using TabletopTempo.Platform.Meta;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>Builds the Meta XR rig: OVRCameraRig + passthrough underlay + tracked hands.</summary>
    public static class MetaSceneSetup
    {
        private const string CameraRigPrefab = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string HandPrefab = "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";

        public static GameObject BuildRig(Scene scene)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefab);
            if (rigPrefab == null)
            {
                Debug.LogError("[TT-ERROR] OVRCameraRig prefab not found at " + CameraRigPrefab);
                return null;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.name = "OVRCameraRig";

            var manager = rig.GetComponent<OVRManager>();
            if (manager != null)
            {
                manager.isInsightPassthroughEnabled = true;
                manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }

            // Passthrough underlay on the rig.
            // Explicit null check: in the editor GetComponent returns a fake-null object, so ?? would not add it.
            var passthrough = rig.GetComponent<OVRPassthroughLayer>();
            if (passthrough == null) passthrough = rig.AddComponent<OVRPassthroughLayer>();
            // SDK 207: the passthrough layer is always composited as the background underlay.
            passthrough.textureOpacity = 1f;
            if (rig.GetComponent<PassthroughStyle>() == null) rig.AddComponent<PassthroughStyle>();

            // Cameras must clear to transparent black so the underlay shows through.
            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 50f;
            }

            AddHand(rig, "LeftHandAnchor", OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.HandLeft, OVRMesh.MeshType.HandLeft);
            AddHand(rig, "RightHandAnchor", OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.HandRight, OVRMesh.MeshType.HandRight);

            return rig;
        }

        private static void AddHand(GameObject rig, string anchorName, OVRHand.Hand hand,
            OVRSkeleton.SkeletonType skeleton, OVRMesh.MeshType mesh)
        {
            var anchor = FindDeep(rig.transform, anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"[TT] {anchorName} not found in OVRCameraRig");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[TT] OVRHandPrefab not found at " + HandPrefab);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            go.name = hand == OVRHand.Hand.HandLeft ? "LeftHand" : "RightHand";

            SetEnum(go.GetComponent<OVRHand>(), "HandType", (int)hand);
            SetEnum(go.GetComponent<OVRSkeleton>(), "_skeletonType", (int)skeleton);
            SetEnum(go.GetComponent<OVRMesh>(), "_meshType", (int)mesh);
        }

        private static void SetEnum(Object target, string field, int value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[TT] {target.GetType().Name}.{field} not found"); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
