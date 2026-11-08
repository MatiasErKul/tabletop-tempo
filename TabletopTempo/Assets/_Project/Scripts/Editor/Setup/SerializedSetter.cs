using UnityEditor;
using UnityEngine;

namespace TabletopTempo.EditorTools.Setup
{
    /// <summary>
    /// Sets serialized fields by name and warns (instead of failing) when a field does not exist,
    /// so an SDK/editor upgrade that renames a field shows up clearly in the batch log.
    /// </summary>
    internal static class SerializedSetter
    {
        public static bool Set(SerializedObject so, string path, object value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[TT] {so.targetObject.GetType().Name}: no serialized field '{path}'");
                return false;
            }

            switch (value)
            {
                case bool b when p.propertyType == SerializedPropertyType.Boolean: p.boolValue = b; break;
                case int i when p.propertyType == SerializedPropertyType.Integer: p.intValue = i; break;
                case int i when p.propertyType == SerializedPropertyType.Enum: p.intValue = i; break;
                case float f when p.propertyType == SerializedPropertyType.Float: p.floatValue = f; break;
                case string s when p.propertyType == SerializedPropertyType.String: p.stringValue = s; break;
                case Object o when p.propertyType == SerializedPropertyType.ObjectReference: p.objectReferenceValue = o; break;
                case null when p.propertyType == SerializedPropertyType.ObjectReference: p.objectReferenceValue = null; break;
                default:
                    Debug.LogWarning($"[TT] {so.targetObject.GetType().Name}.{path}: type mismatch ({p.propertyType} vs {value?.GetType().Name ?? "null"})");
                    return false;
            }
            return true;
        }
    }
}
