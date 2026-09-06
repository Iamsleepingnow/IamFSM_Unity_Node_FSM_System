using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;
using System.IO;
using Status = UnityEngine.UIElements.DropdownMenuAction.Status;

namespace FSMGraph.Editor
{
    public class BasicGraphToolbarView : ToolbarView
    {
        // ProcessGraphProcessor processor; // 节点图执行器
        ToolbarButtonData showParameters; // 显示参数面板按钮
        BaseGraph graph; // 节点图

        public BasicGraphToolbarView(BaseGraphView graphView) : base(graphView) { }

        protected override void AddButtons() {
            graph = graphView.graph;
            AddButton("居中 Center", graphView.ResetPositionAndZoom);
            // AddButton("输出 Output Json", () => {
            //     // // 保存到文件
            //     // string fileName = $"OutputNodeGraph_{Random.Range(100000000, 999999999)}";
            //     // string path = Path.Combine("Assets", fileName + ".json");
            //     // WriteTextFile(JsonUtility.ToJson(graph, true), "Assets/", fileName, ".json");
            //     // AssetDatabase.Refresh(); // 刷新项目视图
            //     // Debug.Log($"已保存到 {path}");
            // }, left: false);
            // processor = new ProcessGraphProcessor(graphView.graph);
            // graphView.computeOrderUpdated += processor.UpdateComputeOrder;
            // AddButton("Run", processor.Run);
            bool exposedParamsVisible = graphView.GetPinnedElementStatus<ExposedParameterView>() != Status.Hidden;
            showParameters = AddToggle("显示参数 Show Parameters", exposedParamsVisible, (v) => graphView.ToggleView<ExposedParameterView>());
            AddButton("显示在项目中 Show In Project", () => EditorGUIUtility.PingObject(graphView.graph), false);
        }

        private void WriteTextFile(string txtText, string folderPath, string fileName, string fileExtension = ".txt") {
            string folder = $"{folderPath}";
            //若不存在路径
            if (!Directory.Exists(folder)) {
                DirectoryInfo info = new(folder);
                info.Create();
            }
            string path = Path.Combine(folder, $"{fileName}{fileExtension}");//文件流创建一个文本文件
            FileStream file = File.Exists(path) ? new FileStream(path, FileMode.Truncate) : new FileStream(path, FileMode.Create);
            byte[] bts = System.Text.Encoding.UTF8.GetBytes(txtText);//文件写入数据流
            file.Write(bts, 0, bts.Length);
            //当数据流存在时
            if (file != null) {
                file.Flush();//清空缓存
                file.Close();//关闭流
                file.Dispose();//销毁资源
            }
        }
    }
}