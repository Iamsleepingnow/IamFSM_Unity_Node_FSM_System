using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using GraphProcessor;

namespace FSMGraph.Editor
{
    [CustomEditor(typeof(BaseGraph), true)]
    public class BaseGraphInspector : GraphInspector
    {
        /// <summary>导出路径在 EditorPrefs 中的前缀（用资产 GUID 隔离，避免跨节点表串扰）</summary>
        const string ExportPathPrefsPrefix = "FSMGraph.ExportJsonPath.";

        /// <summary>编辑节点表资产时，额外提供"打开节点图 + 导出 JSON"等单元</summary>
        protected override void CreateInspector() {
            base.CreateInspector();
            // 最顶部放"打开节点图窗口"，其下是导出 JSON 区
            root.Insert(0, CreateExportSection());
            root.Insert(0, CreateOpenGraphSection());
        }

        /// <summary>构建"打开节点图窗口"按钮 + 是否显示节点 Rid 标签开关</summary>
        VisualElement CreateOpenGraphSection() {
            var graphAsset = target as BaseGraph;
            var section = new VisualElement { style = { marginBottom = 8 } };

            // "打开节点图窗口"按钮
            section.Add(new Button(() => BasicGraph.OpenGraph(graphAsset)) { text = "打开节点图窗口 Open Graph" });

            // 是否在画布节点顶部显示 Rid 标签（默认 false）
            var ridToggle = new Toggle("显示节点 Rid 标签") {
                value = graphAsset.showNodeRidLabel,
                tooltip = "在画布每个节点顶部显示其 Rid（对应运行时日志），随节点表 SO 保存",
            };
            ridToggle.RegisterValueChangedCallback(e => {
                graphAsset.showNodeRidLabel = e.newValue;
                EditorUtility.SetDirty(graphAsset);
                // 同步刷新已打开的对应画布窗口，无需重新打开即生效
                foreach (var w in Resources.FindObjectsOfTypeAll<BasicGraph>()) {
                    if (w.graph == graphAsset)
                        w.RefreshRidLabels();
                }
            });
            section.Add(ridToggle);

            return section;
        }

        /// <summary>构建"导出 JSON"区域：路径输入框 + 解析提示 + 导出按钮</summary>
        VisualElement CreateExportSection() {
            var graphAsset = target as BaseGraph;
            string prefsKey = EditorPrefsKey(graphAsset);
            string savedPath = EditorPrefs.GetString(prefsKey, "");

            var section = new VisualElement { style = { marginBottom = 8 } };
            section.Add(new Label("导出 JSON Export") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var pathField = new TextField("导出路径 Path") { tooltip = "可留空；支持绝对路径 / Assets/… / ./… / ../…；无扩展名则视末级为目录" };
            pathField.value = savedPath;
            pathField.RegisterValueChangedCallback(e => EditorPrefs.SetString(prefsKey, e.newValue));
            section.Add(pathField);

            // 实时显示解析后的实际输出路径（参考节点表据名）
            var resolvedLabel = new Label();
            resolvedLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            resolvedLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            resolvedLabel.style.whiteSpace = WhiteSpace.Normal;

            void RefreshResolved() {
                string final = GraphDataExporter.ResolveOutputPath(graphAsset, pathField.value);
                // 若位于项目 Assets/ 目录下，改为显示相对路径，提升阅读效率
                resolvedLabel.text = "→ " + ToDisplayPath(final);
            }
            RefreshResolved();
            pathField.RegisterValueChangedCallback(_ => RefreshResolved());
            section.Add(resolvedLabel);

            var exportBtn = new Button(() => {
                string final = GraphDataExporter.ResolveOutputPath(graphAsset, pathField.value);
                try {
                    GraphDataExporter.Export(graphAsset, final);
                    AssetDatabase.Refresh();
                }
                catch (Exception e) {
                    Debug.LogError($"[BaseGraphInspector] 导出失败: {e.Message}");
                    EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
                }
            }) { text = "导出本节点表 JSON" };
            section.Add(exportBtn);

            return section;
        }

        /// <summary>
        /// 若绝对路径位于项目 Assets/ 目录下，则转换为以 "Assets/" 开头的相对路径显示，
        /// 否则原样返回绝对路径。仅影响展示，不影响实际导出。
        /// </summary>
        static string ToDisplayPath(string absolutePath) {
            string dataPath = Application.dataPath; // 形如 D:/项目/Assets
            string normAbsolute = absolutePath.Replace('\\', '/');
            // 需要保证以目录边界切割，避免误伤诸如 "Assets2" 的目录
            if (normAbsolute.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) {
                string rest = normAbsolute.Substring(dataPath.Length);
                if (rest.Length == 0 || rest[0] == '/')
                    return "Assets" + rest;
            }
            return absolutePath;
        }

        /// <summary>EditorPrefs 的键：按节点表资产 GUID 隔离</summary>
        static string EditorPrefsKey(BaseGraph graph) {
            string assetPath = AssetDatabase.GetAssetPath(graph);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return ExportPathPrefsPrefix + (string.IsNullOrEmpty(guid) ? assetPath : guid);
        }
    }
}