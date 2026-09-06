using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FSMGraph
{
    /// <summary>FSM 解释器引擎 —— 驱动状态机执行</summary>
    public class FsmEngine
    {
        /// <summary>
        /// 惰性全局单例默认注册表：executor 均为无状态策略，可被所有引擎实例共享，
        /// 避免每个播放器在 Awake 重复 new 出 60+ 执行器。首次访问时经 CreateDefaultRegistry 构建一份。
        /// </summary>
        private static readonly Lazy<FsmNodeExecutorRegistry> _defaultRegistry =
            new Lazy<FsmNodeExecutorRegistry>(CreateDefaultRegistry);
        public static FsmNodeExecutorRegistry DefaultRegistry => _defaultRegistry.Value;

        /// <summary>
        /// 执行器注册表：默认共享全局单例（DefaultRegistry）。如需为个别播放器定制/裁剪
        /// 执行器，可在 Play 前显式赋值覆盖此属性；赋值后该实例不再使用全局单例。
        /// </summary>
        public FsmNodeExecutorRegistry Registry { get; set; } = DefaultRegistry;

        private FsmContext _ctx;
        private bool _isRunning;
        private bool _isInitialized;

        // ==================== 无限循环保护 ====================

        /// <summary>同帧内最大执行步骤数，防止无限状态切换导致栈溢出</summary>
        public int maxExecStepsPerFrame = 100;
        private int _execStepCount;

        /// <summary>同帧内最大数据求值递归深度，防止数据流循环依赖导致栈溢出（PullValue 递归）</summary>
        public int maxDataStepsPerFrame = 100;
        private int _dataRecursionDepth;
        private bool _dataOverflowLogged;

        // ==================== 调试可视化 ====================

        /// <summary>当前正在执行的节点 Rid（调试用）</summary>
        public int CurrentNodeRid { get; private set; }
        /// <summary>当前正在执行的节点名称</summary>
        public string CurrentNodeName { get; private set; }
        /// <summary>当前正在执行的节点类型名</summary>
        public string CurrentNodeTypeName { get; private set; }

        // ==================== 并行阻塞监听（多路条件/等待同时挂起） ====================

        private enum FsmBlockType { Condition, WaitSeconds, WaitFrames }

        /// <summary>单个阻塞节点：条件满足 / 等待秒数 / 等待帧数</summary>
        private sealed class FsmBlocker
        {
            public FsmNode Node;
            public FsmBlockType Type;
            public float Timer;
            public float Duration;
            public int FrameCount;
            public int FrameTarget;
            /// <summary>是否为后台等待：true 时跨状态切换存活，不随切换被中断</summary>
            public bool IsBackground;
            /// <summary>是否源自 NInAnyState 全局链：true 时其阻塞被消费耗尽不触发失活兜底</summary>
            public bool FromInAnyState;
            /// <summary>创建时所在的状态语境版本，用于判定其是否已随状态切换过期</summary>
            public int StateVersion;
        }

        private readonly List<FsmBlocker> _blockers = new();

        /// <summary>本帧内是否发生过状态切换（SwitchState）。Wait/协程等非后台等待据此在调度时直接 Break。</summary>
        private bool _stateSwitchedThisFrame;

        /// <summary>状态语境版本：每次真正进入状态时递增，用于区分跨状态存活的后台等待所派生的条件是否过期</summary>
        private int _stateVersion;

        /// <summary>当前正处于"过期语境"执行的 exec 链（源自跨状态存活的后台 Wait 放行），其派生的新条件（NPerformed）失效</summary>
        private bool _expiredChain;

        /// <summary>【状态机失活（卡住）处理方式】—— 当前状态无任何阻塞监听接续（推进走到尽头）时按配置恢复活性</summary>
        public FsmObjectStuckHandling StuckHandling = FsmObjectStuckHandling.RestartGraph;

        /// <summary>卡住处理时的防重入标志</summary>
        private bool _stuckHandling;

        /// <summary>连续失活自动重启计数，达到 maxExecStepsPerFrame 后停止，防止无监听图死循环</summary>
        private int _stuckCount;

        /// <summary>失活自动重启已耗尽（达到上限后彻底静默，直到重新建立监听才解除）</summary>
        private bool _stuckExhausted;

        /// <summary>中断 Wait 阻塞时的防重入标志</summary>
        private bool _interruptingWaits;

        /// <summary>当前是否正在推进 NInAnyState 全局链（用于给其派生的阻塞标记 FromInAnyState）</summary>
        private bool _advancingFromInAnyState;

        /// <summary>抑制失活兜底：由 NInAnyState 链阻塞被消费耗尽引起，等下次状态切换重跑后再恢复</summary>
        private bool _suppressStuckFromInAnyState;

        /// <summary>【是否开启 Debug 日志】</summary>
        public bool debugLog = true;

        /// <summary>获取指定延时节点（rid）的实时等待进度 0~1；未在等待中返回 0</summary>
        public float GetWaitProgress(int nodeRid) {
            for (int i = 0; i < _blockers.Count; i++) {
                var b = _blockers[i];
                if (b.Node == null || b.Node.Rid != nodeRid) continue;
                if (b.Type == FsmBlockType.WaitSeconds)
                    return b.Duration <= 0f ? 0f : Mathf.Clamp01(b.Timer / b.Duration);
                return Mathf.Clamp01((float)b.FrameCount / b.FrameTarget);
            }
            return 0f;
        }

        // ==================== 初始化 ====================

        /// <summary>构建默认的注册表（注册所有内置执行器）</summary>
        public static FsmNodeExecutorRegistry CreateDefaultRegistry() {
            var reg = new FsmNodeExecutorRegistry();
            reg.Register("NEntryState",         new EntryStateExecutor());
            reg.Register("NGoToState",          new GoToStateExecutor());
            reg.Register("NInState",            new InStateExecutor());
            reg.Register("NInAnyState",         new InAnyStateExecutor());
            reg.Register("NPerformed",          new PerformedExecutor());
            reg.Register("NCompareNumber",      new CompareNumberExecutor());
            reg.Register("NListenInputKey",     new ListenInputKeyExecutor());
            reg.Register("NListenInputMouse",   new ListenInputMouseExecutor());
            reg.Register("NRayDetect",          new RayDetectExecutor());
            reg.Register("NOverlapBoxDetect",    new OverlapBoxDetectExecutor());
            reg.Register("NOverlapSphereDetect", new OverlapSphereDetectExecutor());
            reg.Register("NCollisionDetect",    new CollisionDetectExecutor());
            reg.Register("NTriggerDetect",      new TriggerDetectExecutor());
            reg.Register("NCollisionProxyDetect", new CollisionProxyDetectExecutor());
            reg.Register("NTriggerProxyDetect",   new TriggerProxyDetectExecutor());
            reg.Register("NLogicAnd",           new LogicAndExecutor());
            reg.Register("NLogicOr",            new LogicOrExecutor());
            reg.Register("NLogicXor",           new LogicXorExecutor());
            reg.Register("NLogicNegate",        new LogicNegateExecutor());
            reg.Register("NWaitSeconds",        new WaitSecondsExecutor());
            reg.Register("NWaitFrames",         new WaitFramesExecutor());
            reg.Register("NDebugLog",           new DebugLogExecutor());
            reg.Register("NEventInvoke",        new EventInvokeExecutor());
            reg.Register("NGameTime",           new GameTimeExecutor());
            reg.Register("NInteger",            new PrimitivesExecutor());
            reg.Register("NFloat",              new PrimitivesExecutor());
            reg.Register("NString",             new PrimitivesExecutor());
            reg.Register("NVector",             new VectorExecutor());
            reg.Register("NColor",              new ColorExecutor());
            reg.Register("ParameterNode",       new ParameterNodeExecutor());
            reg.Register("NVectorSplit",        new VectorSplitExecutor());
            reg.Register("NVectorCombine",      new VectorCombineExecutor());
            reg.Register("NConvertColorToVector", new ConvertColorToVectorExecutor());
            reg.Register("NConvertVectorToColor", new ConvertVectorToColorExecutor());
            reg.Register("NConvertToBool",      new ConvertToBoolExecutor());
            reg.Register("NConvertToNumber",    new ConvertToNumberExecutor());
            reg.Register("NConvertToString",    new ConvertToStringExecutor());
            reg.Register("NConvertToVector",    new ConvertToVectorExecutor());
            reg.Register("NConvertToColor",     new ConvertToColorExecutor());
            reg.Register("NClampFloat",         new ClampFloatExecutor());
            reg.Register("NLerpFloat",          new LerpFloatExecutor());
            reg.Register("NMapFloat",           new MapFloatExecutor());
            reg.Register("NMultiMath",          new MultiMathExecutor());
            reg.Register("NSingleMath",         new SingleMathExecutor());
            reg.Register("NTrigonometry",       new TrigonometryExecutor());
            reg.Register("NRadianToDegree",     new RadianToDegreeExecutor());
            reg.Register("NMathConstant",       new MathConstantExecutor());
            reg.Register("NClampVector",        new ClampVectorExecutor());
            reg.Register("NLerpVector",         new LerpVectorExecutor());
            reg.Register("NMapVector",          new MapVectorExecutor());
            reg.Register("NVectorMath",         new VectorMathExecutor());
            reg.Register("NNoiseFloat",         new NoiseFloatExecutor());
            reg.Register("NNoiseVector",        new NoiseVectorExecutor());
            reg.Register("NBranch",             new BranchExecutor());
            reg.Register("NIfElse",             new IfElseExecutor());
            reg.Register("NNodeOrder",          new NodeOrderExecutor());
            reg.Register("NRandomFloat",        new RandomFloatExecutor());
            reg.Register("NRandomVector",       new RandomVectorExecutor());
            reg.Register("NColorFlip",          new ColorFlipExecutor());
            reg.Register("NColorWrap",          new ColorWrapExecutor());
            reg.Register("NStringConcat",       new StringConcatExecutor());
            reg.Register("NStringSplit",        new StringSplitExecutor());
            reg.Register("NToJson",             new ToJsonExecutor());
            reg.Register("NFromJson",           new FromJsonExecutor());
            reg.Register("NRegularEx",          new RegularExExecutor());
            reg.Register("NRegularExMatch",     new RegularExMatchExecutor());
            reg.Register("NCurrentState",       new CurrentStateExecutor());
            reg.Register("NIsEqualTo",          new IsEqualToExecutor());
            reg.Register("NLocalTransform",     new LocalTransformExecutor());
            reg.Register("NLocalGameObject",    new LocalGameObjectExecutor());

            // InputSystem兼容：
            reg.Register("NListenInput",        new ListenInputExecutor());
            
            return reg;
        }

        /// <summary>初始化引擎：注入图数据、宿主、构建运行时结构</summary>
        public void Initialize(FsmGraphData graphData, MonoBehaviour owner) {
            _ctx = new FsmContext {
                Engine    = this,
                Owner     = owner,
                GraphData = graphData,
            };
            // 随机相位偏移：将多实例的物理检测错峰分散到不同帧
            _ctx.ThrottleOffset = UnityEngine.Random.Range(0f, 1f);
            graphData.BuildLookup();
            _ctx.BuildParamLookup();

            // 填充暴露参数的初始值
            foreach (int rid in graphData.exposedParameterRids) {
                if (graphData.nodeMap.TryGetValue(rid, out var paramNode)
                    && paramNode.Properties.TryGetValue("value", out var val))
                {
                    _ctx.ExposedParameters[rid] = val;
                }
            }
            _isInitialized = true;
            OnInitialized?.Invoke();
        }

        // ==================== 启动 ====================

        /// <summary>启动/恢复 FSM：如果正处于阻塞监听中则从中断处恢复，否则从 NEntryState 开始</summary>
        public void Start() {
            if (_isRunning) return;

            // 从暂停中恢复 —— 直接继续阻塞监听，不从入口重新开始
            if (_blockers.Count > 0) {
                _isRunning = true;
                OnStarted?.Invoke();
                return;
            }

            var entryNode = _ctx.GraphData.nodes.Find(n => n.TypeName == "NEntryState");
            if (entryNode == null) {
                if (debugLog)
                    Debug.LogError("[FsmEngine] JSON 中缺少 NEntryState 入口节点！");
                return;
            }

            _execStepCount = 0;
            _isRunning = true;
            OnStarted?.Invoke();
            _stateSwitchedThisFrame = false;   // 全新启动，重置帧级状态切换标志
            _stateVersion = 0;                 // 重置状态语境版本
            _expiredChain = false;             // 清除过期语境标记
            _stuckCount = 0;                     // 重置失活重试计数
            _stuckExhausted = false;
            FollowExecOutput(entryNode);
            TryHandleStuck();                  // 启动推进结束即检测是否失活
        }

        /// <summary>暂停/停止 FSM（保留运行时状态，包括等待进度）</summary>
        public void Stop() {
            bool wasRunning = _isRunning;
            _isRunning = false;
            if (wasRunning)
                OnStopped?.Invoke();
        }

        // ==================== 阻塞监听管理 ====================

        /// <summary>添加条件阻塞（NPerformed），同节点同类型去重。记录当前状态语境版本。</summary>
        private void AddConditionBlocker(FsmNode node) {
            for (int i = 0; i < _blockers.Count; i++)
                if (_blockers[i].Node.Rid == node.Rid && _blockers[i].Type == FsmBlockType.Condition)
                    return;
            _blockers.Add(new FsmBlocker {
                Node = node, Type = FsmBlockType.Condition, StateVersion = _stateVersion,
                FromInAnyState = _advancingFromInAnyState,
            });
            if (debugLog)
                Debug.Log($"[FsmEngine] 开始监测条件: rid={node.Rid}");
        }

        /// <summary>添加秒数等待阻塞（NWaitSeconds）。isBackground=true 表示后台等待，跨状态切换存活。记录状态语境版本。</summary>
        private void AddWaitBlocker(FsmNode node, float seconds, bool isBackground) {
            _blockers.Add(new FsmBlocker {
                Node = node, Type = FsmBlockType.WaitSeconds, Duration = Mathf.Max(0f, seconds),
                IsBackground = isBackground, StateVersion = _stateVersion,
                FromInAnyState = _advancingFromInAnyState,
            });
            if (debugLog)
                Debug.Log($"[FsmEngine] 开始等待秒数: rid={node.Rid}, {seconds}s{(isBackground ? " (后台)" : "")}");
        }

        /// <summary>添加帧数等待阻塞（NWaitFrames）。isBackground=true 表示后台等待，跨状态切换存活。记录状态语境版本。</summary>
        private void AddWaitBlocker(FsmNode node, int frames, bool isBackground) {
            _blockers.Add(new FsmBlocker {
                Node = node, Type = FsmBlockType.WaitFrames, FrameTarget = Mathf.Max(1, frames),
                IsBackground = isBackground, StateVersion = _stateVersion,
                FromInAnyState = _advancingFromInAnyState,
            });
            if (debugLog)
                Debug.Log($"[FsmEngine] 开始等待帧数: rid={node.Rid}, {frames}帧{(isBackground ? " (后台)" : "")}");
        }

        /// <summary>清除所有阻塞监听（状态迁移或条件胜出时调用）</summary>
        private void ClearBlockers() {
            _blockers.Clear();
        }

        /// <summary>
        /// 状态迁移时中断仍挂起的、非后台的 Wait 类阻塞节点：先沿其 breakLink 触发中断，
        /// 再交由调用方 ClearBlockersOnStateSwitch 统一清除。后台等待（IsBackground）不受状态切换影响。
        /// breakLink 分支的执行可能再次切换状态/清空阻塞，故先搜集快照、逐节点触发，并防重入。
        /// </summary>
        private void InterruptPendingWaitsOnStateSwitch() {
            if (_interruptingWaits) return;      // 防重入：breakLink 分支内再次切换状态时跳过
            if (_blockers.Count == 0) return;

            _interruptingWaits = true;
            try {
                // 快照所有仍挂起的、非后台的 Wait 类阻塞节点（条件阻塞不打断，后台等待不打断）
                var waitNodes = _blockers
                    .Where(b => b.Type != FsmBlockType.Condition && !b.IsBackground)
                    .Select(b => b.Node).ToList();
                foreach (var node in waitNodes) {
                    // 触发中断：breakLink 端口（若该节点恰已不在阻塞列表，跳过）
                    if (!_blockers.Any(b => ReferenceEquals(b.Node, node)))
                        continue;
                    FollowExecOutput(node, "breakLink");
                }
            }
            finally {
                _interruptingWaits = false;
            }
        }

        /// <summary>
        /// 状态迁移时清理阻塞：保留后台等待（IsBackground=true），
        /// 其余（条件阻塞、非后台等待）全部清除。
        /// </summary>
        private void ClearBlockersOnStateSwitch() {
            for (int i = _blockers.Count - 1; i >= 0; i--) {
                if (_blockers[i].IsBackground)
                    continue;
                _blockers.RemoveAt(i);
            }
        }

        /// <summary>
        /// 状态机失活（卡住）检测与处理：处于某状态但无任何阻塞监听接续时自动重启。
        /// 在"推进活动"结束和"空闲帧"都会触发；连续失活用 _stuckCount 计数，达 maxExecStepsPerFrame 上限即停止，防止无监听图死循环。
        /// </summary>
        private void TryHandleStuck() {
            if (_stuckHandling) return;
            if (_stuckExhausted) return;                          // 已达上限放弃 → 彻底静默
            if (_suppressStuckFromInAnyState) return;             // NInAnyState 链消耗耗尽，等下次状态切换重跑
            if (!_isRunning || _ctx == null) return;
            if (_blockers.Count > 0) return;                            // 仍有监听接续 → 未失活
            // DoNothing：不自动恢复
            if (StuckHandling != FsmObjectStuckHandling.RestartGraph
                && StuckHandling != FsmObjectStuckHandling.StopGraph) return;
            if (string.IsNullOrEmpty(_ctx.CurrentStateName)) return;    // 尚未进入任何状态

            _stuckHandling = true;
            try {
                if (_stuckCount >= maxExecStepsPerFrame) {             // 持续失活，用步数上限防死循环
                    _stuckExhausted = true;
                    if (debugLog)
                        Debug.Log($"[FsmEngine] 状态机持续失活，已达自动重启上限 {maxExecStepsPerFrame}，停止恢复");
                    return;
                }
                _stuckCount++;
                if (StuckHandling == FsmObjectStuckHandling.StopGraph) {
                    if (debugLog)
                        Debug.Log("[FsmEngine] 状态机失活(无监听接续)，按配置停止状态机(StopGraph)");
                    Stop();
                }
                else {
                    if (debugLog)
                        Debug.Log("[FsmEngine] 状态机失活(无监听接续)，重启节点表(RestartGraph)");
                    RestartGraph();
                }
            }
            finally {
                _stuckHandling = false;
            }
        }

        /// <summary>从入口节点(NEntryState)重新开始整张节点表，并重置参数为初始值</summary>
        private void RestartGraph() {
            ClearBlockers();
            _ctx.ClearFrameCache();
            ResetExposedParameters();
            var entryNode = _ctx.GraphData.nodes.Find(n => n.TypeName == "NEntryState");
            if (entryNode == null) return;
            _stateSwitchedThisFrame = false;
            _stateVersion = 0;
            _expiredChain = false;
            FollowExecOutput(entryNode);
        }

        /// <summary>重置所有暴露参数为图数据中的初始值</summary>
        public void ResetExposedParameters() {
            ClearBlockers();
            if (_ctx == null) return;
            _ctx.ExposedParameters.Clear();
            foreach (int rid in _ctx.GraphData.exposedParameterRids) {
                if (_ctx.GraphData.nodeMap.TryGetValue(rid, out var paramNode)
                    && paramNode.Properties.TryGetValue("value", out var val))
                {
                    _ctx.ExposedParameters[rid] = val;
                }
            }
        }

        // ==================== Update 轮询 ====================

        /// <summary>每帧调用：并行阻塞监听 —— 条件满足检测 / 等待计时</summary>
        public void Tick() {
            if (!_isRunning || _ctx == null) return;

            _execStepCount = 0;
            CleanPhysicsBuffers();
            _ctx.ClearFrameCache();

            // 帧级状态切换标志：记录"从 (含)上一帧 Tick 到本帧本次推进链中"是否切换过状态
            _stateSwitchedThisFrame = false;
            _expiredChain = false;   // 每帧清除过期语境标记，仅由本次推进链按需临时设置

            // 失活恢复：空闲（无监听接续）时按配置自动重启，受 maxExecStepsPerFrame 限制；监听中则清零失活重试计数
            if (_blockers.Count == 0) {
                TryHandleStuck();
                if (_blockers.Count == 0)     // 重启后仍无监听（可能已达上限放弃）→ 本帧无推进可做
                    return;
            }
            else {
                _stuckCount = 0;
                _stuckExhausted = false;   // 重新建立监听，解除失活静默
            }

            // ---- 第1遍：条件阻塞 —— 任一满足即胜出，清空所有阻塞，沿胜出分支推进 ----
            for (int i = 0; i < _blockers.Count; i++) {
                var b = _blockers[i];
                if (b.Type != FsmBlockType.Condition) continue;

                if (!EvaluateNPerformedCondition(b.Node)) continue;

                if (debugLog)
                    Debug.Log($"[FsmEngine] 条件满足，离开状态: {_ctx.CurrentStateNode?.Name}");
                var node = b.Node;
                if (b.FromInAnyState) {
                    // InAnyState 全局链：并行不干扰 —— 只消费本条阻塞，保留当前状态等其它监听；
                    // 走完不自行续命，等到下次状态切换再重跑该链；也不触发失活兜底。
                    _blockers.RemoveAt(i);
                    _ctx.ClearFrameCache();
                    FollowExecOutput(node);
                    _suppressStuckFromInAnyState = true;
                    return;
                }

                // 其余条件/非后台等待阻塞全部停止；后台等待（IsBackground）保留，实现可叠加
                ClearBlockersOnStateSwitch();
                _ctx.ClearFrameCache();
                FollowExecOutput(node);
                TryHandleStuck();
                return;
            }

            // ---- 第2遍：等待阻塞 —— 倒序迭代便于移除 ----
            for (int i = _blockers.Count - 1; i >= 0; i--) {
                var b = _blockers[i];
                if (b.Type == FsmBlockType.Condition) continue;

                // 1. isBreak 中断检查（最高优先级）→ 触发 breakLink 端口
                if (PullValue(_ctx, b.Node.Rid, "isBreak").AsBool()) {
                    var node = b.Node;
                    _blockers.RemoveAt(i);
                    FollowExecOutput(node, "breakLink");
                    if (b.FromInAnyState)
                        _suppressStuckFromInAnyState = true;
                    else
                        TryHandleStuck();
                    return;
                }

                // 2. 每帧触发 delayUpdate 输出端口（可能触发状态切换 → 阻塞集合被重建）
                FollowExecOutput(b.Node, "delayUpdate");
                if (_blockers.Count == 0 || i >= _blockers.Count || !ReferenceEquals(_blockers[i], b)) {
                    if (b.FromInAnyState)
                        _suppressStuckFromInAnyState = true;
                    else
                        TryHandleStuck();
                    return;
                }

                // 3. 计时 Tick
                bool done;
                if (b.Type == FsmBlockType.WaitSeconds) {
                    b.Timer += Time.deltaTime;
                    done = b.Timer >= b.Duration;
                }
                else {
                    b.FrameCount++;
                    done = b.FrameCount >= b.FrameTarget;
                }

                if (done) {
                    var node = b.Node;
                    _blockers.RemoveAt(i);
                    // 后台等待跨状态存活：若其等待期间状态语境已更迭（StateVersion 过期），
                    // 仍沿 executes 放行（如输出 Hello），但标记该链为"过期语境"，
                    // 使链上派生的非后台条件（NPerformed）失效，不再挂起新监听。
                    bool expired = b.StateVersion != _stateVersion;
                    bool prevExpired = _expiredChain;
                    _expiredChain = expired;
                    FollowExecOutput(node, "executes");
                    _expiredChain = prevExpired;
                    if (b.FromInAnyState)
                        _suppressStuckFromInAnyState = true;
                    else
                        TryHandleStuck();
                    return;
                }
            }

            // 遍历完所有阻塞都无推进（本帧无事可做），也顺带检测一次是否失活
            TryHandleStuck();
        }

        // ==================== 控制流：沿 exec 链路执行 ====================

        /// <summary>查找节点所有 Executes* 输出端口名（大小写不敏感）</summary>
        private static IEnumerable<string> GetExecPortNames(FsmNode node) {
            foreach (var kv in node.Outputs)
                if (kv.Key.StartsWith("Executes", StringComparison.OrdinalIgnoreCase))
                    yield return kv.Key;
        }

        /// <summary>判断目标节点是否为 Set 模式的 ParameterNode（需要优先执行以确保写入先于读取）</summary>
        private bool IsSetParameterNode(int nodeRid) {
            if (!_ctx.GraphData.TryGetNode(nodeRid, out var node)) return false;
            return node.TypeName == "ParameterNode" && node.Inputs.ContainsKey("input");
        }

        /// <summary>沿节点的所有 Executes* 输出端口推进控制流（默认行为）。ParameterNode(Set) 优先执行。</summary>
        private void FollowExecOutput(FsmNode node) {
            foreach (var portName in GetExecPortNames(node)) {
                if (node.Outputs.TryGetValue(portName, out var port)) {
                    // ParameterNode(Set) 优先执行，确保写入先于读取
                    ProcessConnections(port.Connections);
                }
            }
        }

        /// <summary>沿节点指定名称的 Executes* 端口推进控制流（如 NIfElse 分叉，大小写不敏感）。ParameterNode(Set) 优先执行。</summary>
        private void FollowExecOutput(FsmNode node, string portName) {
            // 大小写不敏感查找，与 GetExecPortNames 保持一致
            var match = node.Outputs.FirstOrDefault(kv =>
                string.Equals(kv.Key, portName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key)) {
                ProcessConnections(match.Value.Connections);
            }
        }

        /// <summary>按优先级处理连接列表：稳定排序后依次执行。
        /// 排序键 = ParameterNode(Set) 恒为 -1（写优先）；NNodeOrder 取其链上最右 Order；其余为 0。
        /// Linq OrderBy 为稳定排序，同优先级保持原始连线顺序。</summary>
        private void ProcessConnections(List<GraphConnectionData> connections) {
            var ordered = connections
                .OrderBy(c => GetBranchPriority(c.targetRid))
                .ToList();
            foreach (var conn in ordered)
                ExecuteControlFlow(conn.targetRid);
        }

        /// <summary>读取支线排序键：Set 参数节点恒最高优先；否则沿 executes 链取最右 NNodeOrder 的 Order，无则 0</summary>
        private int GetBranchPriority(int nodeRid) {
            if (!_ctx.GraphData.TryGetNode(nodeRid, out var node)) return 0;
            if (node.TypeName == "ParameterNode" && node.Inputs.ContainsKey("input"))
                return -1;
            return GetOrderFromChain(node);
        }

        /// <summary>沿 executes 单输出链遍历连续出现的 NNodeOrder，取最右（最后）一个的 Order；起点非 NNodeOrder 时为 0</summary>
        private int GetOrderFromChain(FsmNode start) {
            int order = 0;
            var cur = start;
            for (int guard = 0; guard < 32 && cur != null && cur.TypeName == "NNodeOrder"; guard++) {
                if (cur.Inputs.TryGetValue("order", out var p) && p.DefaultValue != null)
                    order = Convert.ToInt32(p.DefaultValue);
                // 仅有唯一 executes 后继时继续，否则停在当前 NNodeOrder
                if (!cur.Outputs.TryGetValue("executes", out var ex) || ex.Connections.Count != 1)
                    break;
                _ctx.GraphData.TryGetNode(ex.Connections[0].targetRid, out cur);
            }
            return order;
        }

        /// <summary>对目标节点执行控制流逻辑</summary>
        private void ExecuteControlFlow(int nodeRid) {
            _execStepCount++;
            if (_execStepCount > maxExecStepsPerFrame) {
                Debug.LogError($"[FsmEngine] 同帧内执行步骤超过上限 {maxExecStepsPerFrame}，" +
                               "可能存在无限状态切换循环。已中止本帧执行。");
                return;
            }

            if (!_ctx.GraphData.TryGetNode(nodeRid, out var node)) return;

            CurrentNodeRid = nodeRid;
            CurrentNodeName = node.Name;
            CurrentNodeTypeName = node.TypeName;

            var executor = Registry.Get(node.TypeName);
            if (executor == null) {
                if (debugLog)
                    Debug.LogWarning($"[FsmEngine] 未注册的执行器: {node.TypeName} (rid={nodeRid})");
                return;
            }

            var result = executor.Execute(_ctx, node, portName: null);

            // 只有 ParameterNode(Set) 会修改参数值，此时才需清空求值缓存
            // 其他节点（纯计算/日志/事件等）不改变数据状态，缓存可跨节点复用
            if (node.TypeName == "ParameterNode" && node.Inputs.ContainsKey("input"))
                _ctx.ClearFrameCache();

            // NPerformed 在任何执行位置出现 → 转为并行阻塞监听，不直接放行。
            // NPerformed 无 IsBackground，等价于非后台：本帧已切换状态 或 处于过期语境
            //（跨状态存活的后台 Wait 放行所派生）时，不再挂起（原条件上下文已失效）。
            if (node.TypeName == "NPerformed") {
                if (_stateSwitchedThisFrame || _expiredChain) return;
                AddConditionBlocker(node);
                return;
            }

            switch (result.Type) {
                case FsmExecutionResult.ResultType.SwitchState:
                    _stateSwitchedThisFrame = true;   // 记录本帧发生状态切换
                    OnStateExit?.Invoke(_ctx.CurrentStateName ?? "");
                    EnterState(result.StateName);
                    break;

                case FsmExecutionResult.ResultType.WaitForSeconds:
                    {
                    // 读取是否后台等待：后台等待不受状态切换/过期语境影响，直接挂起
                    bool bgWait = PullValue(_ctx, node.Rid, "isBackground").AsBool();
                    // 非后台等待：本帧已切换过状态，或处于过期语境（跨状态存活的后台 Wait 放行所派生），
                    // 则不再挂起，直接沿 breakLink 退出
                    if (!bgWait && (_stateSwitchedThisFrame || _expiredChain)) { FollowExecOutput(node, "breakLink"); break; }
                    AddWaitBlocker(node, result.FloatValue, bgWait);
                    break;
                    }

                case FsmExecutionResult.ResultType.WaitForFrames:
                    {
                    bool bgWait = PullValue(_ctx, node.Rid, "isBackground").AsBool();
                    if (!bgWait && (_stateSwitchedThisFrame || _expiredChain)) { FollowExecOutput(node, "breakLink"); break; }
                    AddWaitBlocker(node, result.IntValue, bgWait);
                    break;
                    }

                case FsmExecutionResult.ResultType.None:
                default:
                    FollowExecOutput(node);
                    break;

                case FsmExecutionResult.ResultType.ExecutePort:
                    FollowExecOutput(node, result.PortName);
                    break;
            }
        }

        // ==================== NPerformed 复合条件评估 ====================

        /// <summary>
        /// 评估 NPerformed 节点的 condition 输入端口。
        /// 单连接时兼容旧行为直接拉取；多连接时按 isAnd 进行 AND/OR 逻辑组合。
        /// </summary>
        private bool EvaluateNPerformedCondition(FsmNode performedNode) {
            if (!performedNode.Inputs.TryGetValue("condition", out var port))
                return false;

            // 无连接 → 使用端口 UI 中设置的默认值
            if (port.Connections.Count == 0)
                return port.DefaultValue != null && Convert.ToBoolean(port.DefaultValue);

            // 单连接 → 兼容旧行为，直接拉取
            if (port.Connections.Count == 1) {
                var conn = port.Connections[0];
                return PullValue(_ctx, conn.targetRid, conn.targetPort).AsBool();
            }

            // 多连接 → 按 isAnd 组合（默认 AND）
            bool isAnd = !performedNode.Properties.TryGetValue("isAnd", out var v) || v.AsBool();

            if (isAnd) {
                foreach (var conn in port.Connections) {
                    if (!PullValue(_ctx, conn.targetRid, conn.targetPort).AsBool())
                        return false;
                }
                return true;
            }
            else {
                foreach (var conn in port.Connections) {
                    if (PullValue(_ctx, conn.targetRid, conn.targetPort).AsBool())
                        return true;
                }
                return false;
            }
        }

        // ==================== 状态管理 ====================

        /// <summary>进入指定名称的状态</summary>
        private void EnterState(string stateName) {
            // 状态迁移：先沿 breakLink 触发中断所有非后台的 Wait 类阻塞节点，再清理阻塞——
            // 后台等待（IsBackground）跨状态切换保留，其余（条件、非后台等待）清除。
            InterruptPendingWaitsOnStateSwitch();
            ClearBlockersOnStateSwitch();
            // 新一次状态切换：解除"由 NInAnyState 链消耗引起的失活抑制"，本轮会重跑 InAnyState 链补齐监听
            _suppressStuckFromInAnyState = false;
            if (string.IsNullOrEmpty(stateName)) return;

            // 查找与 stateName 匹配的 NInState 节点
            var inStateNode = _ctx.GraphData.nodes.Find(n =>
                n.TypeName == "NInState"
                && n.Properties.TryGetValue("state", out var s)
                && s.AsString() == stateName);

            if (inStateNode == null) {
                if (debugLog)
                    Debug.LogError($"[FsmEngine] 未找到状态: {stateName}");
                return;
            }

            _ctx.CurrentStateName = stateName;
            _ctx.CurrentStateNode = inStateNode;
            _stateVersion++;            // 进入状态即开启新一轮状态语境
            OnStateEnter?.Invoke(stateName);
            if (debugLog)
                Debug.Log($"[FsmEngine] 进入状态: {stateName} (rid={inStateNode.Rid})");

            // 沿所有 NInAnyState 全局入口推进控制流（先于当前状态 InState，落实 AnyState 优先）。
            // 每次状态切换都会执行；其链内 GoToState 触发的新切换导致的自递归由 maxExecStepsPerFrame 兜底。
            foreach (var anyNode in _ctx.GraphData.nodes.Where(n => n.TypeName == "NInAnyState")) {
                if (anyNode.Outputs.TryGetValue("executes", out var anyExec)) {
                    bool prevSwitched = _stateSwitchedThisFrame;
                    _stateSwitchedThisFrame = false;
                    bool prevAdvancing = _advancingFromInAnyState;
                    _advancingFromInAnyState = true;   // 该链派生的阻塞标记 FromInAnyState，耗尽不触发失活兜底
                    ProcessConnections(anyExec.Connections);
                    _advancingFromInAnyState = prevAdvancing;
                    _stateSwitchedThisFrame = prevSwitched;
                }
                // InAnyState 链已驱动了新的状态切换 → 当前 InState 已被取代，结束本次进入
                if (!ReferenceEquals(_ctx.CurrentStateNode, inStateNode))
                    return;
            }

            // 沿 NInState 的 exec 输出推进控制流。
            // NPerformed / NWaitSeconds / NWaitFrames 经 ExecuteControlFlow 自动转为并行阻塞监听。
            if (inStateNode.Outputs.TryGetValue("executes", out var execPort)) {
                // 新状态体推进时清零"状态切换"标志：本状态内部的直通 Wait 是合法等待，
                // 不该被"进入本状态"误判为失效。而外层平行批（同批兄弟）仍保留该标志，
                // 使先于 Wait 执行的 GoToState 能据此把同批 Wait 判为失效并 breakLink。
                bool prevStateSwitched = _stateSwitchedThisFrame;
                _stateSwitchedThisFrame = false;
                ProcessConnections(execPort.Connections);
                _stateSwitchedThisFrame = prevStateSwitched;
            }
        }

        // ==================== 数据流：拉取求值 ====================

        /// <summary>
        /// 递归拉取某个节点的输出端口值。
        /// 这是框架的核心 —— 数据流从叶子节点（输入、参数）向根节点（条件判断）递归求值。
        /// </summary>
        public FsmTypedValue PullValue(FsmContext ctx, int nodeRid, string portName) {
            // 0. 数据流循环保护：按递归深度计数（try/finally 保证出栈时回退），
            //    数据环会持续加深直至达到上限 → 返回 default 打破环并回退，环外节点不受影响。
            //    注意：这里不能用"帧内累计计数"，否则环烧光配额后整帧其他求值全被拦成 default，导致状态机停摆。
            _dataRecursionDepth++;
            if (_dataRecursionDepth > maxDataStepsPerFrame) {
                if (debugLog && !_dataOverflowLogged) {
                    _dataOverflowLogged = true;
                    Debug.LogError($"[FsmEngine] 数据求值递归深度超过上限 {maxDataStepsPerFrame}，" +
                                   "可能存在数据流循环依赖。已中止本段求值。");
                }
                _dataRecursionDepth--;
                return default;
            }
            try {
                // 1. 缓存检查（同一帧内同节点同端口不重复计算）
                var key = (nodeRid, portName);
                if (ctx.EvalCache.TryGetValue(key, out var cached))
                    return cached;

                if (!ctx.GraphData.TryGetNode(nodeRid, out var node))
                    return default;

                // 2. 如果 portName 对应的是一个输入端口，先解析其连接/默认值
                if (node.Inputs.TryGetValue(portName, out var inputPort)) {
                    FsmTypedValue val;
                    if (inputPort.Connections.Count > 0) {
                        // 有连接 → 递归拉取上游节点的输出端口
                        var conn = inputPort.Connections[0];
                        val = PullValue(ctx, conn.targetRid, conn.targetPort);
                    }
                    else {
                        // 无连接 → 使用端口默认值
                        val = ConvertDefaultValue(inputPort.DefaultValue, inputPort.PortTypeName);
                    }
                    ctx.EvalCache[key] = val;
                    return val;
                }

                // 3. portName 是输出端口 → 调度到 Executor 计算
                var executor = Registry.Get(node.TypeName);
                if (executor != null) {
                    var result = executor.Execute(ctx, node, portName);
                    if (result.Type == FsmExecutionResult.ResultType.ValueReady) {
                        ctx.EvalCache[key] = result.TypedValue;
                        return result.TypedValue;
                    }
                }

                return default;
            }
            finally {
                // 保证任意返回路径都回退递归深度
                _dataRecursionDepth--;
            }
        }

        /// <summary>将端口默认值（object）转为 FsmTypedValue</summary>
        private static FsmTypedValue ConvertDefaultValue(object defaultValue, string typeName) {
            if (defaultValue == null) return default;

            try {
                return typeName switch {
                    "bool" => new FsmTypedValue { TypeName = "bool",   BoolValue   = Convert.ToBoolean(defaultValue) },
                    "int" or "long" => new FsmTypedValue { TypeName = "int",  IntValue    = Convert.ToInt64(defaultValue) },
                    "float" or "double" => new FsmTypedValue { TypeName = "float",FloatValue  = Convert.ToDouble(defaultValue) },
                    "string" => new FsmTypedValue { TypeName = "string",StringValue = Convert.ToString(defaultValue) },
                    "Color" or "Vector3" or "Vector4" => ParseMultiDefault(defaultValue, typeName),
                    _ => new FsmTypedValue { TypeName = typeName, IntValue = Convert.ToInt64(defaultValue) },
                };
            }
            catch {
                return default;
            }
        }

        /// <summary>解析 Color / Vector4 默认值</summary>
        private static FsmTypedValue ParseMultiDefault(object defaultValue, string typeName) {
            if (defaultValue is Newtonsoft.Json.Linq.JObject jo) {
                Vector4 v = new(
                    (float)(jo["r"] ?? jo["x"] ?? jo["a"] ?? 0f),
                    (float)(jo["g"] ?? jo["y"] ?? jo["b"] ?? 0f),
                    (float)(jo["b"] ?? jo["z"] ?? jo["c"] ?? 0f),
                    (float)(jo["a"] ?? jo["w"] ?? jo["d"] ?? 0f));
                return new FsmTypedValue { TypeName = typeName, MultiValue = v };
            }
            return default;
        }

        // ==================== 物理缓冲清理 ====================

        /// <summary>移除上一帧的物理事件（FixedUpdate 在 Tick 之前执行，旧帧数据不再需要）</summary>
        private void CleanPhysicsBuffers() {
            if (_ctx == null) return;
            int frame = Time.frameCount;
            _ctx.CollisionEnters.RemoveAll(x => x.frame < frame);
            _ctx.CollisionStays.RemoveAll(x => x.frame < frame);
            _ctx.CollisionExits.RemoveAll(x => x.frame < frame);
            _ctx.CollisionEnters2D.RemoveAll(x => x.frame < frame);
            _ctx.CollisionStays2D.RemoveAll(x => x.frame < frame);
            _ctx.CollisionExits2D.RemoveAll(x => x.frame < frame);
            _ctx.TriggerEnters.RemoveAll(x => x.frame < frame);
            _ctx.TriggerStays.RemoveAll(x => x.frame < frame);
            _ctx.TriggerExits.RemoveAll(x => x.frame < frame);
            _ctx.TriggerEnters2D.RemoveAll(x => x.frame < frame);
            _ctx.TriggerStays2D.RemoveAll(x => x.frame < frame);
            _ctx.TriggerExits2D.RemoveAll(x => x.frame < frame);
        }

        // ==================== 对外 API ====================

        /// <summary>【事件触发回调】—— Executor 调用此事件，外部通过 FsmObject 订阅</summary>
        public event Action<string> OnEventInvoked;

        /// <summary>【状态进入事件】—— 进入新状态时触发，参数为状态名</summary>
        public event Action<string> OnStateEnter;

        /// <summary>【状态退出事件】—— 离开当前状态时触发，参数为状态名</summary>
        public event Action<string> OnStateExit;

        /// <summary>【FSM 初始化完成事件】—— 引擎初始化完成时触发</summary>
        public event Action OnInitialized;

        /// <summary>【FSM 播放开始事件】—— 引擎开始/恢复播放时触发</summary>
        public event Action OnStarted;

        /// <summary>【FSM 播放结束事件】—— 引擎停止/暂停时触发</summary>
        public event Action OnStopped;

        /// <summary>触发事件（供 Executor 调用）</summary>
        public void RaiseEventInvoked(string message) => OnEventInvoked?.Invoke(message);

        /// <summary>按参数名设置暴露参数值（外部驱动 FSM 的入口）</summary>
        public bool SetExposedParameter(string paramName, float value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = new FsmTypedValue { TypeName = "float", FloatValue = value };
            return true;
        }

        public bool SetExposedParameter(string paramName, int value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = new FsmTypedValue { TypeName = "int", IntValue = value };
            return true;
        }

        public bool SetExposedParameter(string paramName, bool value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = new FsmTypedValue { TypeName = "bool", BoolValue = value };
            return true;
        }

        public bool SetExposedParameter(string paramName, string value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = new FsmTypedValue { TypeName = "string", StringValue = value };
            return true;
        }

        public bool SetExposedParameter(string paramName, Vector4 value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = FsmTypedValue.FromVector4(value);
            return true;
        }

        public bool SetExposedParameter(string paramName, Color value) {
            if (!_ctx.TryGetExposedParamRid(paramName, out int rid)) return false;
            _ctx.ExposedParameters[rid] = FsmTypedValue.FromColor(value);
            return true;
        }

        /// <summary>当前状态名（如 "Idle"）。字符串引用，零分配。</summary>
        public string CurrentStateName => _ctx?.CurrentStateName;

        /// <summary>当前是否正在运行</summary>
        public bool IsRunning => _isRunning;

        /// <summary>引擎是否已完成初始化</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>当前上下文（只读访问）</summary>
        public FsmContext Context => _ctx;

        // ==================== 参数读取（高性能、零分配） ====================

        /// <summary>按名称读取暴露参数原始值（struct 返回，栈分配）</summary>
        public bool TryGetParameter(string paramName, out FsmTypedValue value) {
            if (_ctx != null && _ctx.TryGetExposedParamRid(paramName, out int rid)) {
                value = _ctx.ExposedParameters[rid];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>按名称读取 float 参数</summary>
        public bool TryGetFloat(string paramName, out float value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsFloat();
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>按名称读取 int 参数</summary>
        public bool TryGetInt(string paramName, out int value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsInt();
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>按名称读取 bool 参数</summary>
        public bool TryGetBool(string paramName, out bool value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsBool();
                return true;
            }
            value = false;
            return false;
        }

        /// <summary>按名称读取 string 参数</summary>
        public bool TryGetString(string paramName, out string value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsString();
                return true;
            }
            value = "";
            return false;
        }

        /// <summary>按名称读取 Vector4 参数</summary>
        public bool TryGetVector4(string paramName, out Vector4 value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsVector4();
                return true;
            }
            value = Vector4.zero;
            return false;
        }

        /// <summary>按名称读取 Color 参数</summary>
        public bool TryGetColor(string paramName, out Color value) {
            if (TryGetParameter(paramName, out var tv)) {
                value = tv.AsColor();
                return true;
            }
            value = Color.black;
            return false;
        }
    }
}
