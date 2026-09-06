using UnityEditor;
using UnityEngine;

namespace FSMGraph.Editor
{
    /// <summary>FsmObjectAddressable 的数据源区 Inspector：按 addressablesType 条件显隐</summary>
    [CustomEditor(typeof(FsmObjectAddressable))]
    public class FsmObjectAddressableEditor : FsmObjectBaseEditor
    {
        SerializedProperty spAddressablesType, spAddress, spAssetRef;

        protected override void OnEnable() {
            base.OnEnable();
            spAddressablesType = serializedObject.FindProperty("addressablesType");
            spAddress     = serializedObject.FindProperty("address");
            spAssetRef    = serializedObject.FindProperty("assetRef");
        }

        protected override void OnDrawSource() {
            EditorGUILayout.PropertyField(spAddressablesType);

            // 条件显隐：PathName 显示 address，AssetReference 显示 assetRef
            var mode = (FSMLIBRARY.FsmAddressablesType)spAddressablesType.enumValueIndex;
            if (mode == FSMLIBRARY.FsmAddressablesType.PathName)
                EditorGUILayout.PropertyField(spAddress);
            else
                EditorGUILayout.PropertyField(spAssetRef);
        }

        protected override string GetConfigWarning() {
            var mode = (FSMLIBRARY.FsmAddressablesType)spAddressablesType.enumValueIndex;
            if (mode == FSMLIBRARY.FsmAddressablesType.PathName) {
                if (spAddress == null || string.IsNullOrEmpty(spAddress.stringValue))
                    return "address 为空，运行将加载失败。";
            }
            else {
                // AssetReference 非字符串类型，校验其序列化内部 GUID 子字段是否有效
                var guidProp = spAssetRef?.FindPropertyRelative("m_AssetGUID");
                bool guidOk = guidProp != null &&
                              guidProp.propertyType == SerializedPropertyType.String &&
                              !string.IsNullOrEmpty(guidProp.stringValue);
                if (!guidOk)
                    return "assetRef 无效，运行将加载失败。";
            }
            return null;
        }
    }
}