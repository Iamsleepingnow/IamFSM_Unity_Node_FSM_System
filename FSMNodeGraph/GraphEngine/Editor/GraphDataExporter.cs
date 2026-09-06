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
    /// <summary>【编辑器工具：将 BaseGraph 转换为 GraphData DTO 并导出为 JSON，仅在 Editor 中运行，不进入 Runtime Build】</summary>
    public static class GraphDataExporter
    {
        /// <summary>将 BaseGraph 资产导出为 GraphData JSON 文件（自动确保目标目录存在）</summary>
        public static void Export(BaseGraph graph, string outputPath) {
            GraphData dto = ConvertToDTO(graph);
            var settings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
            };
            string json = JsonConvert.SerializeObject(dto, settings);
            string dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir)) dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(graph));
            Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json);
            AssetDatabase.Refresh();
            Debug.Log($"[GraphDataExporter] 已导出到 {outputPath}");
        }

        /// <summary>
        /// 将 Inspector 上用户输入的导出路径字符串，解析为最终的绝对 json 路径。
        /// 容错规则：
        ///  1. 允许 “/”、“\”、“\\” 任意分隔符（统一归一为 “/”）。
        ///  2. 支持绝对路径（如 D:/.../Graph001.json）。
        ///  3. 其余视为相对路径，基准为“节点表资产所在路径”：
        ///     - 以 “Assets/” 开头（或普通子目录）→ 解析为项目内路径。
        ///     - 以 “./” 或 “../” 开头 → 以节点表资产所在目录为参考基址。
        ///     - 若无文件扩展名，则把最后一级视为目录，最终文件名取节点表名称。
        ///  4. 空字符串 → 在节点表资产所在目录下，生成与节点表同名的 json。
        /// </summary>
        public static string ResolveOutputPath(BaseGraph graph, string inputPath) {
            string graphAssetPath = AssetDatabase.GetAssetPath(graph);
            // 新建未保存的节点表尚无磁盘路径，GetAssetPath 返回空串；
            // 此时对空串调用 Path.GetDirectoryName 在 .NET/Mono 下会抛 ArgumentException，
            // 故回退到项目 Assets 根目录，仅作展示与后续导出兜底。
            string graphDir = string.IsNullOrEmpty(graphAssetPath)
                ? "Assets"
                : Path.GetDirectoryName(graphAssetPath); // 如 "Assets/Graph"

            // 空串/纯空白 → 节点表所在目录 + 节点表名.json
            if (string.IsNullOrWhiteSpace(inputPath)) {
                return Path.Combine(graphDir, graph.name + ".json");
            }

            // 1. 分隔符归一化（\ 与 \\ 统一成 /）
            string p = inputPath.Replace('\\', '/');

            string finalPath;
            if (Path.IsPathRooted(p)) {
                // 2. 绝对路径直接采用
                finalPath = p;
            }
            else if (p.StartsWith("./") || p.StartsWith("../")) {
                // 4. 以节点表所在目录为参考基址
                finalPath = Path.Combine(graphDir, p);
            }
            else {
                // 3. Assets/…… 或普通子目录 → 以项目根（dataPath 的上一级）为基准
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                finalPath = Path.Combine(projectRoot, p);
            }

            // 归一化路径（解析 ./ ../ 及连续斜杠），便于判断扩展名
            finalPath = Path.GetFullPath(finalPath);

            // 3. 无文件扩展名 → 把最后一级当作目录，文件名取节点表名称
            if (!Path.HasExtension(finalPath))
                finalPath = Path.Combine(finalPath, graph.name + ".json");

            return finalPath;
        }

        /// <summary>BaseGraph → GraphData DTO</summary>
        public static GraphData ConvertToDTO(BaseGraph graph) {
            // 1. 构建 GUID → rid 的映射
            Dictionary<string, int> guidToRid = new();
            Dictionary<int, BaseNode> ridToNode = new();
            int nextRid = 1000;
            foreach (var node in graph.nodes) {
                int rid = nextRid++;
                guidToRid[node.GUID] = rid;
                ridToNode[rid] = node;
            }
            // 2. 收集暴露参数 rid
            List<int> exposedRids = new();
            foreach (var ep in graph.exposedParameters) {
                if (ep == null) continue;
                int rid = nextRid++;
                exposedRids.Add(rid);
                // 参数节点也用 GUID 映射（ParameterNode 通过 parameterGUID 引用参数）
                guidToRid[ep.guid] = rid;
            }
            // 3. 构建节点数据 DTO
            List<GraphNodeData> nodeList = new();
            foreach (var kv in ridToNode) {
                int rid = kv.Key;
                BaseNode node = kv.Value;

                GraphNodeData nodeData = new() {
                    rid = rid,
                    name = GetNodeName(node),
                    typeName = node.GetType().Name,
                    assembly = node.GetType().Assembly.GetName().Name,
                    positionX = node.position.x,
                    positionY = node.position.y,
                    properties = CollectProperties(node, guidToRid, graph),
                    inputs = CollectPorts(node, PortDirection.Input, guidToRid, graph),
                    outputs = CollectPorts(node, PortDirection.Output, guidToRid, graph),
                };
                nodeList.Add(nodeData);
            }
            // 4. 添加暴露参数节点
            foreach (var ep in graph.exposedParameters) {
                if (ep == null) continue;
                int rid = guidToRid[ep.guid];
                var paramData = new GraphNodeData {
                    rid = rid,
                    name = ep.name,
                    typeName = ep.GetType().Name,
                    assembly = ep.GetType().Assembly.GetName().Name,
                    properties = new List<GraphPropertyData> {
                    new() {
                        name     = "value",
                        typeName = GetFriendlyTypeName(ep.GetValueType()),
                        value    = SanitizeValue(ep.value, ep.GetValueType()),
                    },
                },
                    inputs = new List<GraphPortData>(),
                    outputs = new List<GraphPortData>(),
                };
                nodeList.Add(paramData);
            }
            // 5. 绕过 Relay 节点：Relay 只是布局辅助，运行时无意义，直接建立跨 relay 的连接
            BypassRelayNodes(nodeList);

            return new() {
                nodes = nodeList,
                exposedParameterRids = exposedRids,
            };
        }

        /// <summary>收集节点的自定义属性（排除 BaseNode 继承字段和端口字段）</summary>
        private static List<GraphPropertyData> CollectProperties(
            BaseNode node, Dictionary<string, int> guidToRid, BaseGraph graph) {
            List<GraphPropertyData> result = new();

            // ParameterNode 特殊处理：跳过内部字段，用 GUID 解析出参数 rid 和参数类型
            if (node is ParameterNode paramNode) {
                if (!string.IsNullOrEmpty(paramNode.parameterGUID)
                    && guidToRid.TryGetValue(paramNode.parameterGUID, out int paramRid)) {
                    result.Add(new GraphPropertyData {
                        name = "parameterRid",
                        typeName = "int",
                        value = paramRid,
                    });

                    // 附加参数的值类型，避免消费者二次查表
                    var param = graph.GetExposedParameterFromGUID(paramNode.parameterGUID);
                    if (param != null) {
                        result.Add(new GraphPropertyData {
                            name = "parameterValueType",
                            typeName = "string",
                            value = GetFriendlyTypeName(param.GetValueType()),
                        });
                    }
                }
                // 导出 Get/Set 模式标记
                result.Add(new GraphPropertyData {
                    name = "accessor",
                    typeName = "int",
                    value = (int)paramNode.accessor,
                });
                return result;
            }

            Type nodeType = node.GetType();
            // BaseNode 自身的字段名（需要排除）
            HashSet<string> baseNodeFields = new(
                typeof(BaseNode).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name));
            // 只拿当前类型声明的 public 实例字段
            IEnumerable<FieldInfo> fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !baseNodeFields.Contains(f.Name))
                .Where(f => !f.IsDefined(typeof(InputAttribute), false))
                .Where(f => !f.IsDefined(typeof(OutputAttribute), false))
                .Where(f => !f.IsDefined(typeof(NonSerializedAttribute), false));
            foreach (var field in fields) {
                object val = field.GetValue(node);
                result.Add(new() {
                    name = field.Name,
                    typeName = GetFriendlyTypeName(field.FieldType),
                    value = SanitizeValue(val, field.FieldType),
                });
            }
            return result;
        }

        /// <summary>
        /// 绕过 Relay 节点：Relay 是纯布局辅助节点，在 DTO 中应当透明。
        /// 将 "A → Relay → B" 转为 "A → B"，然后删除所有 Relay 节点。
        /// 这样 PullValue 和 FollowExecOutput 都能直接沿链路行走，无需特殊处理。
        /// </summary>
        private static void BypassRelayNodes(List<GraphNodeData> nodeList) {
            var ridMap = nodeList.ToDictionary(n => n.rid);
            var relays = nodeList.Where(n => n.typeName == "RelayNode").ToList();
            if (relays.Count == 0) return;

            foreach (var relay in relays) {
                var inputPort = relay.inputs.FirstOrDefault();
                var outputPort = relay.outputs.FirstOrDefault();
                if (inputPort == null || outputPort == null) continue;

                // 遍历所有 <上游 → Relay → 下游> 组合，建立直接连接
                foreach (var inConn in inputPort.connections) {
                    foreach (var outConn in outputPort.connections) {
                        // 更新上游节点的输出端口：Relay target → 真正下游 target
                        if (ridMap.TryGetValue(inConn.targetRid, out var srcNode)) {
                            var srcPort = srcNode.outputs.FirstOrDefault(p => p.fieldName == inConn.targetPort);
                            var relayConn = srcPort?.connections.FirstOrDefault(c => c.targetRid == relay.rid);
                            if (relayConn != null) {
                                relayConn.targetRid  = outConn.targetRid;
                                relayConn.targetPort = outConn.targetPort;
                            }
                        }
                        // 更新下游节点的输入端口：Relay source → 真正上游 source
                        if (ridMap.TryGetValue(outConn.targetRid, out var tgtNode)) {
                            var tgtPort = tgtNode.inputs.FirstOrDefault(p => p.fieldName == outConn.targetPort);
                            var relayConn = tgtPort?.connections.FirstOrDefault(c => c.targetRid == relay.rid);
                            if (relayConn != null) {
                                relayConn.targetRid  = inConn.targetRid;
                                relayConn.targetPort = inConn.targetPort;
                            }
                        }
                    }
                }
            }

            // 删除所有 Relay 节点
            nodeList.RemoveAll(n => n.typeName == "RelayNode");
        }

        /// <summary>收集节点的端口信息</summary>
        private static List<GraphPortData> CollectPorts(
            BaseNode node, PortDirection direction,
            Dictionary<string, int> guidToRid, BaseGraph graph) {
            List<GraphPortData> result = new();
            NodePortContainer ports = direction == PortDirection.Input
                ? node.inputPorts
                : node.outputPorts;

            // 直接以序列化的 graph.edges（inputNodeGUID/outputNodeGUID + inputFieldName/outputFieldName）
            // 为准构建连接，而不是依赖瞬态的 port.GetEdges() 与 edge.inputNode/outputNode（非序列化引用）。
            //
            // 旧实现缺陷：在 保存/导出/代码编译/进出 Play 等窗口期，若某条边因端口暂未解析
            // （Deserialize → GetPort 返回 null）而未被注册到 NodePort.edges，或 edge.inputNode/outputNode
            // 引用瞬时为 null，CollectPorts 就会静默跳过它 —— 这正是"Executes→Executed"有概率断连的根因：
            // 数据仍在 graph.edges（序列化原子），但导出 JSON 时被丢掉。
            //
            // 这里只按 GUI 与 fieldName 匹配，与运行时 DTO（FsmNode.Inputs/Outputs 按 fieldName 键）完全一致，
            // 对任意窗口期的加载竞态免疫；同时对同 (目标rid,目标端口) 去重，避免遗留脏边导致重复执行。
            var touchingEdges = graph.edges
                .Where(e => !string.IsNullOrEmpty(e.GUID)) // 过滤未序列化的残缺边
                .ToList();

            // 预筛"与本节点相关"的边（按序列化 GUID 定位 ±1 端），再按端口 fieldName 精确落入
            var mine = touchingEdges.Where(e =>
                    (direction == PortDirection.Input ? e.inputNodeGUID : e.outputNodeGUID) == node.GUID)
                .ToList();

            foreach (var port in ports) {
                Type portType = port.portData.displayType ?? port.fieldInfo?.FieldType;

                // ParameterNode 端口：displayType 可能未加载，从暴露参数获取真实类型
                if ((portType == null || portType == typeof(object)) && node is ParameterNode paramNode) {
                    var param = graph.GetExposedParameterFromGUID(paramNode.parameterGUID);
                    if (param != null)
                        portType = param.GetValueType();
                }
                GraphPortData portData = new() {
                    fieldName = port.fieldName,
                    portTypeName = GetFriendlyTypeName(portType),
                    defaultValue = SanitizeValue(port.fieldInfo?.GetValue(port.fieldOwner ?? port.owner), portType),
                    connections = new(),
                };
                var seen = new HashSet<string>();
                if (direction == PortDirection.Output) {
                    // 输出端口：连接目标 = 边的输入侧（下游节点）
                    foreach (var e in mine.Where(x => x.outputFieldName == port.fieldName)) {
                        if (string.IsNullOrEmpty(e.inputFieldName)) continue;
                        if (!guidToRid.TryGetValue(e.inputNodeGUID, out int targetRid)) continue;
                        if (!seen.Add(targetRid + ":" + e.inputFieldName)) continue;
                        portData.connections.Add(new() { targetRid = targetRid, targetPort = e.inputFieldName });
                    }
                }
                else {
                    // 输入端口：连接来源 = 边的输出侧（上游节点）
                    foreach (var e in mine.Where(x => x.inputFieldName == port.fieldName)) {
                        if (string.IsNullOrEmpty(e.outputFieldName)) continue;
                        if (!guidToRid.TryGetValue(e.outputNodeGUID, out int targetRid)) continue;
                        if (!seen.Add(targetRid + ":" + e.outputFieldName)) continue;
                        portData.connections.Add(new() { targetRid = targetRid, targetPort = e.outputFieldName });
                    }
                }
                result.Add(portData);
            }
            return result;
        }

        /// <summary>将 Type 转为友好的类型名简写</summary>
        private static string GetFriendlyTypeName(Type type) {
            if (type == null) return "unknown";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(string)) return "string";
            if (type == typeof(ConditionalLink)) return "exec";
            if (type.IsEnum) return type.Name;        // "KeyCode", "LogType"
            return type.Name;
        }

        /// <summary>
        /// 将任意值（属性值 / 端口默认值 / 暴露参数值）转为 JSON 友好格式。
        /// Unity 的 Vector4/Color 等 struct 有自引用属性（如 normalized），
        /// 直接序列化会触发 Self referencing loop 异常，需要先拆成简单匿名对象。
        /// </summary>
        private static object SanitizeValue(object rawValue, Type portType) {
            if (rawValue == null) return null;

            if (portType == typeof(Vector4)) {
                var v = (Vector4)rawValue;
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            }
            if (portType == typeof(Vector3)) {
                var v = (Vector3)rawValue;
                return new { x = v.x, y = v.y, z = v.z };
            }
            if (portType == typeof(Vector2)) {
                var v = (Vector2)rawValue;
                return new { x = v.x, y = v.y };
            }
            if (portType == typeof(Color)) {
                var c = (Color)rawValue;
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            }
            if (portType == typeof(Color32)) {
                var c = (Color32)rawValue;
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            }

            return rawValue;
        }

        /// <summary>获取节点显示名称（反射访问 internal 的 nodeCustomName）</summary>
        private static string GetNodeName(BaseNode node) {
            // nodeCustomName 是 internal 字段，跨程序集需反射获取
            FieldInfo field = typeof(BaseNode).GetField("nodeCustomName",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) {
                string customName = field.GetValue(node) as string;
                if (!string.IsNullOrEmpty(customName))
                    return customName;
            }
            return node.name; // fallback 到类名
        }

        private enum PortDirection { Input, Output }

        // ========== 菜单入口（Tools → NodeGraph 节点画布）==========

        [MenuItem("Tools/NodeGraph 节点画布/导出 FSM JSON（BaseGraph → json）", priority = 500)]
        private static void ExportSelected() {
            var graphs = Selection.objects.OfType<BaseGraph>().ToList();
            if (graphs.Count == 0) {
                Debug.Log("[GraphDataExporter] 未选择任何 BaseGraph 节点画布资产，已忽略。");
                return;
            }

            // 忽略选中文件中非 BaseGraph 的资产
            if (graphs.Count < Selection.objects.Length) {
                int ignored = Selection.objects.Length - graphs.Count;
                Debug.Log($"[GraphDataExporter] 已忽略 {ignored} 个非 BaseGraph 资产。");
            }

            // 默认路径取"选中的最后一个文件"所在目录
            string defaultDir = DefaultDirFromSelection(false);

            // 单选：让用户指定完整路径 + 文件名
            if (graphs.Count == 1) {
                string defaultName = graphs[0].name;
                string path = EditorUtility.SaveFilePanel("导出 FSM JSON", defaultDir, defaultName, "json");
                if (string.IsNullOrEmpty(path)) return;
                Export(graphs[0], path);
                return;
            }

            // 多选：只指定目标文件夹，批量导出（文件名取各节点表名）
            string folder = EditorUtility.SaveFolderPanel("批量导出 FSM JSON 到文件夹", defaultDir, "");
            if (string.IsNullOrEmpty(folder)) return;
            foreach (BaseGraph g in graphs)
                Export(g, Path.Combine(folder, g.name + ".json"));
        }

        /// <summary>取"选中文件（最后一个）"所在目录，作为文件浏览器的默认起始目录</summary>
        private static string DefaultDirFromSelection(bool assetRelative) {
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0) return string.Empty;

            for (int i = objs.Length - 1; i >= 0; i--) {
                if (objs[i] == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(objs[i]);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string dir = Path.GetDirectoryName(assetPath);
                if (string.IsNullOrEmpty(dir)) continue;
                // SaveFilePanel / SaveFolderPanel 需要绝对路径作为初始目录
                return assetRelative ? dir : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), dir));
            }
            return string.Empty;
        }
    }
}
