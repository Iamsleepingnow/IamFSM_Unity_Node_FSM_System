using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using GraphProcessor;

namespace FSMGraph.Editor
{
    /// <summary>【基础节点画布】</summary>
    public class BasicGraph : BaseGraphWindow
    {
        readonly BaseGraph newGraph;
        BasicGraphToolbarView toolbarView;

        [MenuItem("Assets/Create/Node Graph 节点画布/New Basic Graph", false, 10)]
        public static void CreateBasicGraphAsset() {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            ProjectWindowUtil.CreateAsset(graph, "NewBasicGraph.asset");
        }

        [OnOpenAsset(0)]
        public static bool OnOpenBasicGraph(int instanceID, int line) {
            // 6000.3 尚无 EntityId 转换 API（EditorUtility.EntityIdToObject 自 6.4 才提供），
            // 此过时接口是当前版本由 instanceID 取对象的唯一途径，故用局部 pragma 压掉该条警告。
            // 升级至 6.4+ 时改为 [OnOpenAsset(EntityId, int)] 并直接使用 EntityId 即可。
#pragma warning disable CS0618
            var asset = EditorUtility.InstanceIDToObject(instanceID) as BaseGraph;
#pragma warning restore CS0618
            if (asset != null) {
                var window = CreateWindow<BasicGraph>();
                window.InitializeGraph(asset);
                window.Show();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 从 BaseGraph SO 的 Inspector 顶部按钮打开该节点图窗口：
        /// 若已有窗口打开同一图资产则聚焦，否则新建。graph 为 protected，故在此类内封装。
        /// </summary>
        public static void OpenGraph(BaseGraph asset) {
            foreach (var w in Resources.FindObjectsOfTypeAll<BasicGraph>()) {
                if (w.graph == asset) {
                    w.Focus();
                    return;
                }
            }
            var window = CreateWindow<BasicGraph>();
            window.InitializeGraph(asset);
            window.Show();
        }

        /// <summary>刷新本画布中所有节点的 Rid 标签（Inspector 切换开关后调用）</summary>
        public void RefreshRidLabels() {
            graphView?.RefreshRidLabelsVisibility();
        }

        protected override void OnDestroy() {
            graphView?.Dispose();
            DestroyImmediate(newGraph);
        }

        protected override void InitializeWindow(BaseGraph graph) {
            if (graphView == null) {
                graphView = new BaseGraphView(this);
                toolbarView = new BasicGraphToolbarView(graphView);
                graphView.Add(toolbarView);
            }
            titleContent = new GUIContent(graph.name);
            rootView.Add(graphView);
        }
    }
}