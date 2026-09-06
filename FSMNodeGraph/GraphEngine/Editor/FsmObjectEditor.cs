using UnityEditor;
using UnityEngine;

namespace FSMGraph.Editor
{
    /// <summary>FsmObject（TextAsset / StreamingAssets）的数据源区 Inspector</summary>
    [CustomEditor(typeof(FsmObject))]
    public class FsmObjectEditor : FsmObjectBaseEditor
    {
        SerializedProperty spFsmJson, spFilePathType, spFilePath;

        protected override void OnEnable() {
            base.OnEnable();
            spFsmJson     = serializedObject.FindProperty("fsmJson");
            spFilePathType = serializedObject.FindProperty("filePathType");
            spFilePath    = serializedObject.FindProperty("filePath");
        }

        protected override void OnDrawSource() {
            EditorGUILayout.PropertyField(spFsmJson);

            EditorGUILayout.PropertyField(spFilePathType);
            EditorGUILayout.PropertyField(spFilePath);

            // 联动提示：显示按当前类型拼接出的完整路径
            var type = (FSMLIBRARY.FsmFilePathType)spFilePathType.enumValueIndex;
            string combined = FSMLIBRARY.ConcatPathByPathType(type, spFilePath.stringValue);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.TextField(new GUIContent("解析路径 Resolved"), combined);
            GUI.enabled = wasEnabled;
        }

        protected override string GetConfigWarning() {
            bool hasJson = spFsmJson != null && spFsmJson.objectReferenceValue != null;
            bool hasPath = spFilePath != null && !string.IsNullOrEmpty(spFilePath.stringValue);
            if (!hasJson && !hasPath)
                return "未指定 fsmJson 或 filePath，运行时 Play 将报错。";
            return null;
        }
    }
}