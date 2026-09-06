using UnityEngine;

namespace FSMGraph
{
    /// <summary>【FSM 状态机播放器】—— 通过 TextAsset 或 StreamingAssets 文件同步加载 FSM JSON</summary>
    public class FsmObject : FsmObjectBase
    {
        /// <summary>【FSM 图 JSON 资产（直接拖入，优先级最高）】</summary>
        [Tooltip("当fsmJson为空时，才能从filePath路径读取JSON文件")]
        [SerializeField] public TextAsset fsmJson;

        /// <summary>【FSM 文件路径类型】</summary>
        [SerializeField] public FSMLIBRARY.FsmFilePathType filePathType = FSMLIBRARY.FsmFilePathType.StreamingAssets;

        /// <summary>【FSM 文件路径（filePathType 混合拼接）】</summary>
        [SerializeField] public string filePath = "";

        /// <summary>解析当前数据源的 JSON 文本：优先 fsmJson 资产，其次 filePathType + filePath 文件读取。仅供 Inspector 调试按钮调用，不改变运行时 fsmData。</summary>
        public override string GetDataSourceJson() {
            if (fsmJson != null)
                return fsmJson.text;
            if (!string.IsNullOrEmpty(filePath)) {
                string fullPath = FSMLIBRARY.ConcatPathByPathType(filePathType, filePath);
                // ReadTextFile 内部已关闭/释放 FileStream，不涉内存泄漏
                return FSMLIBRARY.ReadTextFile(fullPath);
            }
            return null;
        }

        /// <summary>反序列化 JSON：优先 fsmJson 资产，其次 filePathType + filePath 文件读取</summary>
        protected override void EnsureDeserialized() {
            if (fsmData != null) return;

            string jsonText = null;

            // 优先使用直接拖入的 TextAsset
            if (fsmJson != null) {
                jsonText = fsmJson.text;
            }
            // 其次通过 filePathType + filePath 拼接路径读取文件
            else if (!string.IsNullOrEmpty(filePath)) {
                string fullPath = FSMLIBRARY.ConcatPathByPathType(filePathType, filePath);
                jsonText = FSMLIBRARY.ReadTextFile(fullPath);
                if (string.IsNullOrEmpty(jsonText)) {
                    Debug.LogError($"[FsmObject] 文件读取失败或为空: {fullPath}");
                    return;
                }
            }
            else {
                Debug.LogError("[FsmObject] 未指定 fsmJson 或 filePath！请在 Inspector 中设置。");
                return;
            }

            DeserializeFromJson(jsonText);
        }
    }
}
