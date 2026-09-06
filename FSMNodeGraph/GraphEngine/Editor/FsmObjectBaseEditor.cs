using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FSMGraph.Editor
{
    /// <summary>
    /// FsmObjectBase 的 Inspector 美化基类（可折叠分区）。
    /// 三个播放器共用的公共字段在这里绘制；子类通过 OnDrawSource() 追加"数据源"区。
    /// 注意：CustomEditor 用 editorForChildClasses=true 仅为兜底，
    /// 真正的 FsmObject / FsmObjectAddressable 各自的 Editor 会更精确地匹配。
    /// </summary>
    [CustomEditor(typeof(FsmObjectBase), true)]
    public class FsmObjectBaseEditor : UnityEditor.Editor
    {
        // ---- 跨域重载强制播放（"播放 Play"按钮）----
        const string kForcePlayKey = "FSMGraph.Play.ForcePlayPath";   // EditorPrefs 键（域名重载不丢失）

        static FsmObjectBaseEditor() {
            // 进入 Play 模式且场景已加载（Awake 已完成）后再定位组件并强制播放
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.EnteredPlayMode) return;

                string path = EditorPrefs.GetString(kForcePlayKey, null);
                if (string.IsNullOrEmpty(path)) return;
                EditorPrefs.DeleteKey(kForcePlayKey);   // 一次性消费

                EditorApplication.delayCall += () => {
                    var player = FindFsmObjectBaseByPath(path);
                    // Awake 已注册执行器；用方法保证本会话只启动一次，与 autoPlay(Start) 去重
                    if (player != null) player.ForcePlayIfNotAutoStarted();
                };
            };
        }

        /// <summary>以斜杠分隔的层级路径定位目标 FsmObjectBase（不依赖已失效的旧引用）</summary>
        static FsmObjectBase FindFsmObjectBaseByPath(string path) {
            foreach (var p in Object.FindObjectsByType<FsmObjectBase>(FindObjectsSortMode.InstanceID)) {
                if (GetTransformPath(p.transform) == path) return p;
            }
            return null;
        }

        static string GetTransformPath(Transform t) {
            var sb = new StringBuilder(t.name);
            var parent = t.parent;
            while (parent != null) {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return sb.ToString();
        }

        // 公共字段的 SerializedProperty
        protected SerializedProperty spDebugLog, spAutoPlay, spMaxExec, spMaxData;
        protected SerializedProperty spStuckHandling;
        protected SerializedProperty spCurrentState, spCurrentNodeRid, spCurrentNodeName, spCurrentNodeTypeName;
        protected SerializedProperty spFsmInitialized, spFsmStarted, spFsmStopped, spFsmEvent, spFsmEnterState, spFsmExitState;

        // 分区折叠状态（默认展开 控制 / 数据源，其余收起）
        bool showControl = true;
        bool showSource  = true;
        bool showLimits;
        bool showVisualization;
        bool showEvents;

        protected virtual void OnEnable() {
            spDebugLog        = serializedObject.FindProperty("debugLog");
            spAutoPlay        = serializedObject.FindProperty("autoPlay");
            spStuckHandling   = serializedObject.FindProperty("stuckHandling");
            spMaxExec         = serializedObject.FindProperty("maxExecStepsPerFrame");
            spMaxData         = serializedObject.FindProperty("maxDataStepsPerFrame");
            spCurrentState     = serializedObject.FindProperty("_currentState");
            spCurrentNodeRid   = serializedObject.FindProperty("_currentNodeRid");
            spCurrentNodeName  = serializedObject.FindProperty("_currentNodeName");
            spCurrentNodeTypeName = serializedObject.FindProperty("_currentNodeTypeName");
            spFsmInitialized   = serializedObject.FindProperty("fsmInitialized");
            spFsmStarted     = serializedObject.FindProperty("fsmStarted");
            spFsmStopped     = serializedObject.FindProperty("fsmStopped");
            spFsmEvent         = serializedObject.FindProperty("fsmEvent");
            spFsmEnterState    = serializedObject.FindProperty("fsmEnterState");
            spFsmExitState     = serializedObject.FindProperty("fsmExitState");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            DrawPlaybackControls();
            DrawConfigWarning();
            DrawControlSection();
            DrawSection("数据源 Data Source", ref showSource, OnDrawSource);
            DrawLimitsSection();
            DrawVisualizationSection();
            DrawEventsSection();
            DrawDatasourceInspectorSection();

            serializedObject.ApplyModifiedProperties();

            // 运行中持续刷新，让调试可视化的只读字段实时更新
            if (Application.isPlaying) Repaint();
        }

        // ==================== 播放控制 ====================

        void DrawPlaybackControls() {
            var player = (FsmObjectBase)target;
            EditorGUILayout.Space();

            // 居中标题：FSM 状态机播放器
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 14
            };
            EditorGUILayout.LabelField("FSM 状态机播放器", titleStyle);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            // 非运行时：常显“播放”，点击即切入 Play 模式并启动，避免在编辑器态直接驱动
            if (!Application.isPlaying) {
                if (GUILayout.Button("播放 Play")) {
                    // 用 EditorPrefs 跨域名重载记录“强制播放”意图及其目标组件路径
                    EditorPrefs.SetString(kForcePlayKey, GetTransformPath((target as FsmObjectBase).transform));
                    EditorApplication.isPlaying = true;
                }
            }
            else {
                if (GUILayout.Button("播放 Play")) player.Play();      // 可恢复暂停状态
                if (GUILayout.Button("暂停 Pause")) player.Pause();
                if (GUILayout.Button("停止 Stop")) player.Stop();
                if (GUILayout.Button("重播 Restart")) player.ReStart();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        /// <summary>子类可返回配置缺失警告；空字符串表示无警告</summary>
        protected virtual string GetConfigWarning() => null;

        void DrawConfigWarning() {
            string warn = GetConfigWarning();
            if (!string.IsNullOrEmpty(warn))
                EditorGUILayout.HelpBox(warn, MessageType.Warning);
        }

        // ==================== 分区绘制 ====================

        void DrawControlSection() {
            DrawSection("控制 Control", ref showControl, () => {
                EditorGUILayout.PropertyField(spDebugLog);
                EditorGUILayout.PropertyField(spAutoPlay);
                EditorGUILayout.PropertyField(spStuckHandling, new GUIContent("失活处理 stuck Handling"));
            });
        }

        void DrawLimitsSection() {
            DrawSection("步数限制 Step Limits", ref showLimits, () => {
                EditorGUILayout.PropertyField(spMaxExec, new GUIContent("执行步数/帧 maxExecSteps"));
                EditorGUILayout.PropertyField(spMaxData, new GUIContent("数据递归深度 maxDataSteps"));
            });
        }

        void DrawVisualizationSection() {
            DrawSection("调试可视化 Visualization", ref showVisualization, () => {
                // 四联只读展示，运行时由 Update 同步
                bool wasEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.PropertyField(spCurrentState);
                EditorGUILayout.PropertyField(spCurrentNodeRid);
                EditorGUILayout.PropertyField(spCurrentNodeName);
                EditorGUILayout.PropertyField(spCurrentNodeTypeName);
                GUI.enabled = wasEnabled;

                // fsmData 为运行时数据结构（非 [Serializable]，无法用 PropertyField 渲染），只读展示摘要
                var player = (FsmObjectBase)target;
                string fsmDataInfo = player.fsmData == null
                    ? "未加载 (null)"
                    : $"节点数 {player.fsmData.nodes?.Count ?? 0}";
                EditorGUILayout.TextField(new GUIContent("运行时数据 fsmData"), fsmDataInfo);
            });
        }

        void DrawEventsSection() {
            DrawSection("事件 Events", ref showEvents, () => {
                EditorGUILayout.PropertyField(spFsmInitialized);
                EditorGUILayout.PropertyField(spFsmStarted);
                EditorGUILayout.PropertyField(spFsmStopped);
                EditorGUILayout.PropertyField(spFsmEvent);
                EditorGUILayout.PropertyField(spFsmEnterState);
                EditorGUILayout.PropertyField(spFsmExitState);
            });
        }

        /// <summary>可折叠分区通用绘制：标题行 + 内容（可空行折叠）</summary>
        protected void DrawSection(string title, ref bool state, Action body) {
            EditorGUILayout.Space(4f);
            state = EditorGUILayout.Foldout(state, title, true, new GUIStyle(EditorStyles.foldoutHeader) {
                fontStyle = FontStyle.Bold
            });
            if (!state) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            body();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        /// <summary>子类在此绘制各自的"数据源"区字段</summary>
        protected virtual void OnDrawSource() { }

        /// <summary>在 Inspector 最下方提供"输出数据源暴露变量与事件"的调试按钮</summary>
        void DrawDatasourceInspectorSection() {
            EditorGUILayout.Space(4f);
            if (!GUILayout.Button("节点表信息调试 Graph Log")) return;

            var player = (FsmObjectBase)target;
            string jsonText = player.GetDataSourceJson();
            if (string.IsNullOrEmpty(jsonText)) {
                Debug.LogWarning($"[FsmObject] 当前播放器（{player.GetType().Name}）未提供 JSON 数据源，无法检查暴露变量与事件。");
                return;
            }
            LogDataSourceSummary(jsonText);
        }

        /// <summary>
        /// 解析数据源 JSON 并输出暴露变量与事件的摘要日志。
        /// 仅在方法内解析与统计，DTO 不落入任何静态/实例字段，方法返回后即随 GC 释放，避免内存泄漏。
        /// </summary>
        static void LogDataSourceSummary(string jsonText) {
            GraphData data;
            try {
                data = JsonConvert.DeserializeObject<GraphData>(jsonText);
            }
            catch (Exception e) {
                Debug.LogError($"[FsmObject] 解析数据源 JSON 失败: {e.Message}");
                return;
            }
            if (data == null || data.nodes == null) {
                Debug.LogWarning("[FsmObject] 数据源 JSON 结构无效（缺少 nodes）。");
                return;
            }

            var paramRids = new System.Collections.Generic.HashSet<int>(data.exposedParameterRids ?? new System.Collections.Generic.List<int>());

            // ---- 状态信息（GoToState 的目标态为动态值，需回溯解析）----
            var gotoStateTargets = data.nodes
                .Where(n => n.typeName == "NGoToState")
                .Select(n => ResolveInputPortValue(data, paramRids, n, "state"))
                .ToList();
            var inStateNames = data.nodes
                .Where(n => n.typeName == "NInState")
                .Select(n => ReadPropertyString(n, "state"))
                .ToList();
            var allStates = gotoStateTargets.Concat(inStateNames).Distinct().OrderBy(s => s).ToList();
            Debug.Log($"[FsmObject] === 状态信息 States（共 {allStates.Count} 个状态）===");
            foreach (var st in allStates) {
                int gotoCount = gotoStateTargets.Count(x => x == st);
                int inCount = inStateNames.Count(x => x == st);
                Debug.Log($"[FsmObject] 状态 | {st} | GoToState 个数: {gotoCount} | InState 个数: {inCount}");
            }

            // ---- 暴露变量 ----
            Debug.Log($"[FsmObject] === 暴露变量 Exposed Parameters（共 {data.exposedParameterRids?.Count ?? 0} 个）===");
            foreach (var n in data.nodes) {
                if (!paramRids.Contains(n.rid)) continue;
                // 类型优先取属性 value 的友好类型，缺失时用参数节点类名
                var valueProp = n.properties?.FirstOrDefault(p => p.name == "value");
                string friendlyType = valueProp?.typeName ?? n.typeName;
                // ParameterNode 个数：统计图中所有引用该参数（parameterRid == 本参数 rid）的 ParameterNode
                long selfRid = n.rid;
                int refCount = data.nodes.Count(x =>
                    x.typeName == "ParameterNode"
                    && x.properties != null
                    && x.properties.Any(p => p.name == "parameterRid" && TryConvertLong(p.value, out long rid) && rid == selfRid));
                Debug.Log($"[FsmObject] 变量 | {n.name} | 类型: {friendlyType} | 调用次数: {refCount}");
            }

            // ---- 事件（Message 事件名为动态值，需回溯解析）----
            var eventInvokeNodes = data.nodes.Where(n => n.typeName == "NEventInvoke").ToList();
            Debug.Log($"[FsmObject] === 事件 Events（共 {eventInvokeNodes.Count} 个 EventInvoke）===");
            foreach (var group in eventInvokeNodes
                .Select(n => new { name = ResolveInputPortValue(data, paramRids, n, "message") })
                .GroupBy(t => t.name)
                .OrderBy(g => g.Key)) {
                Debug.Log($"[FsmObject] 事件 | {group.Key} | EventInvoke 数量: {group.Count()}");
            }
        }

        /// <summary>
        /// 回溯解析输入端口的动态值（GoToState 的 state、EventInvoke 的 message）：
        /// 若该端口有连接，则沿首个连接向上溯源取值；取不到静态值时回退到端口默认值（节点配置值）。
        /// 注：输入端口连接的 targetRid/targetPort 即上游源节点及其输出端口。
        /// </summary>
        static string ResolveInputPortValue(GraphData data, HashSet<int> paramRids, GraphNodeData node, string portField) {
            var port = node.inputs?.FirstOrDefault(p => p.fieldName == portField);
            if (port == null || port.connections == null || port.connections.Count == 0)
                return FormatValue(port?.defaultValue);

            var source = data.nodes.FirstOrDefault(n => n.rid == port.connections[0].targetRid);
            string traced = source != null ? ResolveNodeValue(data, paramRids, source) : null;
            return traced ?? FormatValue(port.defaultValue);
        }

        /// <summary>解析一个源节点产生的静态值：暴露参数的 value 或字面量节点的 value 属性；无法解析返回 null</summary>
        static string ResolveNodeValue(GraphData data, HashSet<int> paramRids, GraphNodeData node) {
            if (paramRids.Contains(node.rid)) {
                var v = node.properties?.FirstOrDefault(p => p.name == "value");
                if (v?.value != null) return FormatValue(v.value);
            }
            var literal = node.properties?.FirstOrDefault(p => p.name == "value");
            return literal?.value != null ? FormatValue(literal.value) : null;
        }

        /// <summary>读取节点属性字符串（InState 的 state 为普通字段属性）</summary>
        static string ReadPropertyString(GraphNodeData node, string propName) {
            var p = node.properties?.FirstOrDefault(x => x.name == propName);
            return p?.value == null ? "" : Convert.ToString(p.value, CultureInfo.InvariantCulture);
        }

        /// <summary>把 JSON 原生值格式化为字符串</summary>
        static string FormatValue(object v) => v == null ? "" : (v is string s ? s : Convert.ToString(v, CultureInfo.InvariantCulture));

        /// <summary>安全地把 JSON 值转为 long（parameterRid 在 JSON 中为数字，反序列化为 long）</summary>
        static bool TryConvertLong(object value, out long result) {
            if (value == null) { result = 0; return false; }
            try { result = Convert.ToInt64(value); return true; }
            catch { result = 0; return false; }
        }
    }
}