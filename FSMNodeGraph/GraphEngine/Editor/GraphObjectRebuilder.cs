using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GraphProcessor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FSMGraph.Editor
{
    /// <summary>
    /// 从导出的 FSM JSON 反向重建 BaseGraph ScriptableObject 节点图。
    /// 右键 .json 文件 → "Rebuild BaseGraph Asset"
    /// </summary>
    public static class GraphObjectRebuilder
    {
        private const float NodeSpacingY = 120f;

        // ==================== 入口 ====================

        [MenuItem("Tools/NodeGraph 节点画布/重建 BaseGraph 资产（json → BaseGraph）", priority = 501)]
        private static void RebuildSelected() {
            var texts = Selection.objects.OfType<TextAsset>().ToList();
            if (texts.Count == 0) {
                Debug.Log("[GraphObjectRebuilder] 未选择任何 json 的 TextAsset，已忽略。");
                return;
            }

            // 忽略选中文件中非 json TextAsset 的资产
            if (texts.Count < Selection.objects.Length) {
                int ignored = Selection.objects.Length - texts.Count;
                Debug.Log($"[GraphObjectRebuilder] 已忽略 {ignored} 个非 json TextAsset 资产。");
            }

            string defaultDir = AssetRelativeDefaultDir();

            // 单选：让用户指定完整路径 + 文件名
            if (texts.Count == 1) {
                var jsonAsset = texts[0];
                GraphData data = TryParseJson(jsonAsset);
                if (data == null) return;

                string defaultName = Path.GetFileNameWithoutExtension(jsonAsset.name) + "_rebuilt";
                string savePath = EditorUtility.SaveFilePanelInProject(
                    "保存重建的 BaseGraph", defaultName, "asset",
                    "选择保存位置", defaultDir);
                if (string.IsNullOrEmpty(savePath)) return;

                RebuildToAsset(data, savePath);
                return;
            }

            // 多选：只指定目标文件夹，批量重建（文件名取各 json 名）
            string folderAbs = EditorUtility.SaveFolderPanel("批量重建 BaseGraph 到文件夹", DefaultDirFromSelection(), "");
            if (string.IsNullOrEmpty(folderAbs)) return;

            foreach (var jsonAsset in texts) {
                GraphData data = TryParseJson(jsonAsset);
                if (data == null) continue;

                string assetName = Path.GetFileNameWithoutExtension(jsonAsset.name) + "_rebuilt.asset";
                RebuildToAsset(data, Path.Combine(folderAbs, assetName));
            }
        }

        /// <summary>解析 json 为 GraphData，失败则弹窗并返回 null</summary>
        private static GraphData TryParseJson(TextAsset jsonAsset) {
            GraphData data;
            try { data = JsonConvert.DeserializeObject<GraphData>(jsonAsset.text); }
            catch (Exception e) {
                EditorUtility.DisplayDialog("重建失败", $"{jsonAsset.name} JSON 解析错误:\n{e.Message}", "确定");
                return null;
            }
            if (data?.nodes == null || data.nodes.Count == 0) {
                EditorUtility.DisplayDialog("重建失败", $"{jsonAsset.name} JSON 中没有有效节点数据", "确定");
                return null;
            }
            return data;
        }

        /// <summary>重建单个 BaseGraph 并保存为资产（savePath 支持 Assets/… 相对路径或绝对路径）</summary>
        private static void RebuildToAsset(GraphData data, string savePath) {
            try {
                var graph = Rebuild(data);
                string internalPath = ToAssetRelativePath(savePath);
                AssetDatabase.CreateAsset(graph, internalPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUIUtility.PingObject(graph);
                Debug.Log($"[GraphObjectRebuilder] 重建完成: {internalPath}");
            }
            catch (Exception e) {
                Debug.LogError($"[GraphObjectRebuilder] 重建失败: {e}");
                EditorUtility.DisplayDialog("重建失败", e.Message, "确定");
            }
        }

        /// <summary>取"选中文件（最后一个）"所在目录（Assets/… 相对路径）</summary>
        private static string AssetRelativeDefaultDir() {
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0) return string.Empty;
            for (int i = objs.Length - 1; i >= 0; i--) {
                if (objs[i] == null) continue;
                string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(objs[i]));
                if (!string.IsNullOrEmpty(dir)) return dir;
            }
            return string.Empty;
        }

        /// <summary>取"选中文件（最后一个）"所在目录（绝对路径，供 SaveFolderPanel 初始目录）</summary>
        private static string DefaultDirFromSelection() {
            string rel = AssetRelativeDefaultDir();
            if (string.IsNullOrEmpty(rel)) return string.Empty;
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), rel));
        }

        /// <summary>把绝对路径（在项目内则转回 Assets/… 相对）转换为 AssetDatabase 可用路径</summary>
        private static string ToAssetRelativePath(string path) {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/'); // 形如 …/Assets
            if (full.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
                return "Assets/" + full.Substring(dataPath.Length + 1);
            // 项目外的绝对路径按原样处理（此时 CreateAsset 会失败，交由上层弹窗提示）
            return full;
        }

        // ==================== 重建主流程 ====================

        private static BaseGraph Rebuild(GraphData data) {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();

            // ---------- 1. 类型解析 ----------
            var typeMap = ResolveTypes(data);

            var exposedParamRidSet = new HashSet<int>(data.exposedParameterRids ?? new List<int>());

            // ---------- 2. 暴露参数先建（ParameterNode 依赖其 GUID）----------
            var epGuidMap = new Dictionary<int, string>(); // old rid → 新 GUID
            foreach (int rid in data.exposedParameterRids) {
                var defNode = data.nodes.Find(n => n.rid == rid);
                if (defNode == null) continue;
                string newGuid = RebuildExposedParameter(graph, defNode);
                epGuidMap[rid] = newGuid;
            }

            // ---------- 3. 创建节点（ParameterNode 先注入正确 GUID 再 AddNode）----------
            var ridToNode = new Dictionary<int, BaseNode>();
            var ridToGuid = new Dictionary<int, string>();

            foreach (var nd in data.nodes) {
                if (exposedParamRidSet.Contains(nd.rid)) continue;

                if (!typeMap.TryGetValue(nd.typeName, out var nodeType)) {
                    Debug.LogWarning($"[Rebuilder] 跳过未知类型: {nd.typeName} (rid={nd.rid})");
                    continue;
                }

                var node = (BaseNode)Activator.CreateInstance(nodeType);
                node.GUID = Guid.NewGuid().ToString();
                node.position = new Rect(nd.positionX, nd.positionY, 0, 0);

                // ParameterNode 必须在 AddNode 前注入正确的 parameterGUID，
                // 否则 Enable() → LoadExposedParameter() 找不到参数 → 自删
                if (node is ParameterNode pn) {
                    var paramRidProp = nd.properties?.FirstOrDefault(p => p.name == "parameterRid");
                    if (paramRidProp != null) {
                        int oldParamRid = Convert.ToInt32(paramRidProp.value);
                        if (epGuidMap.TryGetValue(oldParamRid, out var newGuid)) {
                            var gf = typeof(ParameterNode).GetField("parameterGUID",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            gf?.SetValue(pn, newGuid);
                        }
                    }
                }

                graph.AddNode(node);
                ridToNode[nd.rid] = node;
                ridToGuid[nd.rid] = node.GUID;
            }

            // ---------- 4. 属性赋值 ----------
            foreach (var nd in data.nodes) {
                if (!ridToNode.TryGetValue(nd.rid, out var node)) continue;
                ApplyProperties(node, nd);
                ApplyPortDefaults(node, nd);
            }

            // 属性赋值后刷新 ParameterNode 端口（accessor 变更需要重建端口以显示 exec 等）
            foreach (var nd in data.nodes) {
                if (nd.typeName != "ParameterNode") continue;
                if (!ridToNode.TryGetValue(nd.rid, out var node)) continue;
                ((BaseNode)node).UpdateAllPorts();
            }

            // ---------- 5. 连线 ----------
            RebuildConnections(graph, data, ridToNode, ridToGuid);

            // ---------- 6. 布局（注释：DTO 已包含 positionX/positionY，直接还原原始位置）----------
            // LayoutNodes(ridToNode, data, ridToGuid);

            EditorUtility.SetDirty(graph);
            return graph;
        }

        // ==================== 类型解析 ====================

        /// <summary>从 typeName + assembly 解析出 C# Type</summary>
        private static Dictionary<string, Type> ResolveTypes(GraphData data) {
            var map = new Dictionary<string, Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var nd in data.nodes) {
                if (map.ContainsKey(nd.typeName)) continue;

                Type found = null;
                foreach (var asm in assemblies) {
                    found = asm.GetType(nd.typeName);
                    if (found != null) {
                        // 也尝试用完整命名空间查找
                        break;
                    }
                }

                // 没找到 → 在所有程序集中搜索同名类型
                if (found == null) {
                    foreach (var asm in assemblies) {
                        foreach (var t in asm.GetTypes()) {
                            if (t.Name == nd.typeName || t.FullName == nd.typeName) {
                                found = t;
                                break;
                            }
                        }
                        if (found != null) break;
                    }
                }

                if (found != null)
                    map[nd.typeName] = found;
            }

            return map;
        }

        // ==================== 属性赋值 ====================

        /// <summary>将 DTO 属性写回节点实例</summary>
        private static void ApplyProperties(BaseNode node, GraphNodeData nd) {
            if (nd.properties == null) return;
            var nodeType = node.GetType();

            foreach (var prop in nd.properties) {
                var field = nodeType.GetField(prop.name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;

                object val = ConvertPropertyValue(prop.value, field.FieldType);
                if (val != null)
                    field.SetValue(node, val);
            }
        }

        /// <summary>将 JSON 反序列化的值转为目标字段类型</summary>
        private static object ConvertPropertyValue(object rawValue, Type targetType) {
            if (rawValue == null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            // 已经是目标类型 → 直接返回
            if (targetType.IsInstanceOfType(rawValue)) return rawValue;

            // Newtonsoft 反序列化出的 JObject（Color / Vector4 等）
            if (rawValue is Newtonsoft.Json.Linq.JObject jo) {
                if (targetType == typeof(Color))
                    return new Color(
                        (float)(jo["r"] ?? 0f), (float)(jo["g"] ?? 0f),
                        (float)(jo["b"] ?? 0f), (float)(jo["a"] ?? 1f));
                if (targetType == typeof(Vector4))
                    return new Vector4(
                        (float)(jo["x"] ?? 0f), (float)(jo["y"] ?? 0f),
                        (float)(jo["z"] ?? 0f), (float)(jo["w"] ?? 0f));
                if (targetType == typeof(Vector3))
                    return new Vector3(
                        (float)(jo["x"] ?? 0f), (float)(jo["y"] ?? 0f),
                        (float)(jo["z"] ?? 0f));
            }

            // 枚举
            if (targetType.IsEnum)
                return Enum.ToObject(targetType, Convert.ToInt64(rawValue));

            // 基本类型转换
            try { return Convert.ChangeType(rawValue, targetType); }
            catch { return null; }
        }

        /// <summary>将 DTO 端口默认值写回节点的 [Input]/[Output] 字段（覆盖 OnNodeCreated 的重置）</summary>
        private static void ApplyPortDefaults(BaseNode node, GraphNodeData nd) {
            var nodeType = node.GetType();

            foreach (var portData in nd.inputs ?? Enumerable.Empty<GraphPortData>()) {
                if (portData.defaultValue == null) continue;

                var field = nodeType.GetField(portData.fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;

                object val = ConvertPropertyValue(portData.defaultValue, field.FieldType);
                if (val != null)
                    field.SetValue(node, val);
            }

            // 输出端口一般没有 [ShowAsDrawer]，但保持一致也处理
            foreach (var portData in nd.outputs ?? Enumerable.Empty<GraphPortData>()) {
                if (portData.defaultValue == null) continue;

                var field = nodeType.GetField(portData.fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;

                object val = ConvertPropertyValue(portData.defaultValue, field.FieldType);
                if (val != null)
                    field.SetValue(node, val);
            }
        }

        // ==================== 暴露参数 ====================

        /// <summary>重建暴露参数，返回新 GUID</summary>
        private static string RebuildExposedParameter(BaseGraph graph, GraphNodeData def) {
            // 从 typeName 推断参数类型：FloatParameter → FloatParameter
            Type paramType = Type.GetType($"GraphProcessor.{def.typeName}");
            if (paramType == null) {
                // fallback: 从 properties 推断
                var valueProp = def.properties.FirstOrDefault(p => p.name == "value");
                if (valueProp != null) {
                    paramType = valueProp.typeName switch {
                        "float" => typeof(FloatParameter),
                        "int" => typeof(IntParameter),
                        "bool" => typeof(BoolParameter),
                        "string" => typeof(StringParameter),
                        "Color" => typeof(ColorParameter),
                        "Vector4" => typeof(Vector4Parameter),
                        _ => typeof(FloatParameter)
                    };
                }
                else paramType = typeof(FloatParameter);
            }

            var param = (ExposedParameter)Activator.CreateInstance(paramType);

            // 恢复暴露参数的默认值：优先取 DTO 中导出的 properties["value"]，
            // 否则退回新建实例的类型默认值（param.value）。
            object restoredValue = ReadExposedParamDefaultValue(def, param.GetValueType());
            if (restoredValue == null)
                restoredValue = param.value;
            param.Initialize(def.name, restoredValue);

            return graph.AddExposedParameter(param);
        }

        /// <summary>从暴露参数 DTO 中读取并转换其默认值（properties["value"]）</summary>
        private static object ReadExposedParamDefaultValue(GraphNodeData def, Type targetType) {
            if (def.properties == null) return null;
            var valueProp = def.properties.FirstOrDefault(p => p.name == "value");
            if (valueProp == null || valueProp.value == null) return null;
            return ConvertPropertyValue(valueProp.value, targetType);
        }

        // ==================== 连线 ====================

        /// <summary>根据 DTO 输出端口连接信息重建所有边</summary>
        private static void RebuildConnections(BaseGraph graph, GraphData data,
            Dictionary<int, BaseNode> ridToNode, Dictionary<int, string> ridToGuid) {
            var guidToNode = new Dictionary<string, BaseNode>();
            foreach (var kv in ridToNode) {
                if (ridToGuid.TryGetValue(kv.Key, out var g))
                    guidToNode[g] = kv.Value;
            }

            foreach (var nd in data.nodes) {
                if (!ridToNode.TryGetValue(nd.rid, out var srcNode)) continue;
                if (nd.outputs == null) continue;

                foreach (var outPort in nd.outputs) {
                    if (outPort.connections == null) continue;

                    var outputPort = srcNode.outputPorts.FirstOrDefault(p => p.fieldName == outPort.fieldName);
                    if (outputPort == null) continue;

                    foreach (var conn in outPort.connections) {
                        if (!ridToNode.TryGetValue(conn.targetRid, out var tgtNode)) continue;

                        var inputPort = tgtNode.inputPorts.FirstOrDefault(p => p.fieldName == conn.targetPort);
                        if (inputPort == null) continue;

                        try { graph.Connect(inputPort, outputPort); }
                        catch (Exception e) { Debug.LogWarning($"[Rebuilder] 连线失败 {nd.rid}:{outPort.fieldName}→{conn.targetRid}:{conn.targetPort}: {e.Message}"); }
                    }
                }
            }
        }

        // ==================== 布局 ====================

        /// <summary>
        /// 双重布局：
        /// - Y 轴（纵向）：拓扑排序，沿所有连接关系从上到下
        /// - X 轴（横向）：执行节点居中，数据节点左侧，暴露参数右侧
        /// </summary>
        private static void LayoutNodes(Dictionary<int, BaseNode> ridToNode,
            GraphData data, Dictionary<int, string> ridToGuid) {
            var exposedSet = new HashSet<int>(data.exposedParameterRids ?? new List<int>());

            // ----- 构建完整邻接图（包含 NGoToState → NInState 隐含边）-----
            // adjacency[srcRid] = list of downstream targetRids
            var adjacency = new Dictionary<int, List<int>>();
            foreach (var nd in data.nodes) {
                if (exposedSet.Contains(nd.rid)) continue;
                adjacency[nd.rid] = new List<int>();
            }

            foreach (var nd in data.nodes) {
                if (exposedSet.Contains(nd.rid)) continue;
                if (nd.outputs == null) continue;

                foreach (var op in nd.outputs) {
                    if (op.connections == null) continue;
                    foreach (var c in op.connections) {
                        if (!exposedSet.Contains(c.targetRid))
                            adjacency[nd.rid].Add(c.targetRid);
                    }
                }
            }

            // 隐含边：NGoToState(state="X") → NInState(state="X")
            var stateNameMap = new Dictionary<string, int>(); // stateName → NInState.rid
            foreach (var nd in data.nodes) {
                if (nd.typeName != "NInState") continue;
                var stateProp = nd.properties?.FirstOrDefault(p => p.name == "state");
                if (stateProp != null)
                    stateNameMap[Convert.ToString(stateProp.value)] = nd.rid;
            }
            foreach (var nd in data.nodes) {
                if (nd.typeName != "NGoToState") continue;
                var stateProp = nd.properties?.FirstOrDefault(p => p.name == "state");
                if (stateProp != null && stateNameMap.TryGetValue(Convert.ToString(stateProp.value), out int inStateRid))
                    adjacency[nd.rid].Add(inStateRid);
            }

            // ----- 拓扑排序 → Y 坐标 -----
            var yOrder = new List<int>();
            var visited = new HashSet<int>();
            var tempMark = new HashSet<int>();

            void Visit(int rid) {
                if (tempMark.Contains(rid)) return; // 环 → 跳过
                if (visited.Contains(rid)) return;
                tempMark.Add(rid);
                foreach (int next in adjacency.GetValueOrDefault(rid, new List<int>()))
                    Visit(next);
                tempMark.Remove(rid);
                visited.Add(rid);
                yOrder.Add(rid);
            }

            // 从 NEntryState 开始 DFS，再处理剩余未访问的
            var entryNode = data.nodes.Find(n => n.typeName == "NEntryState");
            if (entryNode != null) Visit(entryNode.rid);
            foreach (var nd in data.nodes)
                if (!exposedSet.Contains(nd.rid) && !visited.Contains(nd.rid))
                    Visit(nd.rid);
            // 暴露参数放最后
            foreach (int rid in exposedSet)
                if (!visited.Contains(rid))
                    yOrder.Add(rid);

            yOrder.Reverse(); // 拓扑排序是逆序的，反转得到从上到下

            // ----- 分类 → X 坐标 -----
            // 执行类节点（有 Executes* 输出 或 executed 输入）：居中列
            // 数据类节点：左侧列
            // 暴露参数：右侧列
            var isExecNode = new HashSet<int>();
            foreach (var nd in data.nodes) {
                if (exposedSet.Contains(nd.rid)) continue;
                bool hasExecOut = nd.outputs?.Any(op => op.portTypeName == "exec") == true;
                bool hasExecIn = nd.inputs?.Any(ip => ip.portTypeName == "exec") == true;
                if (hasExecOut || hasExecIn)
                    isExecNode.Add(nd.rid);
            }

            const float colExec = 400f;  // 执行节点 X
            const float colData = 0f;    // 数据节点 X
            const float colParam = 800f;  // 暴露参数 X
            float y = 0f;

            foreach (int rid in yOrder) {
                if (!ridToNode.TryGetValue(rid, out var node)) continue;

                float x;
                if (exposedSet.Contains(rid))
                    x = colParam;
                else if (isExecNode.Contains(rid))
                    x = colExec;
                else
                    x = colData;

                node.position = new Rect(new Vector2(x, y), Vector2.zero);
                y += NodeSpacingY;
            }
        }
    }
}
