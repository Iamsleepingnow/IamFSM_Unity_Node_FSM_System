using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Events;

namespace FSMGraph
{
    /// <summary>【FSM 状态机播放器 基类】—— 掌管注册、回放、生命周期；子类各自实现数据加载</summary>
    public abstract class FsmObjectBase : MonoBehaviour
    {
        /// <summary>【是否开启 Debug 日志】</summary>
        public bool debugLog = false;

        /// <summary>【是否在 Start 时自动初始化并播放】</summary>
        public bool autoPlay = true;

        /// <summary>
        /// 本会话是否已由 autoPlay 启动过一次（非序列化）。用于防止"autoPlay Start"与
        /// 编辑器"播放 Play"按钮（强制播放）各触发一次导致重复启动/失活重入。
        /// </summary>
        internal bool _autoPlayPerformed;

        /// <summary>【同帧内最大执行步骤数，防止无限状态切换循环导致栈溢出】</summary>
        public int maxExecStepsPerFrame = 100;

        /// <summary>【数据求值最大递归深度，防止数据流循环依赖导致栈溢出】</summary>
        public int maxDataStepsPerFrame = 100;

        /// <summary>【状态机失活（卡住）处理方式】—— 图解状态无监听接续、推进走到尽头时如何恢复活性</summary>
        public FsmObjectStuckHandling stuckHandling = FsmObjectStuckHandling.RestartGraph;

        // ==================== 调试可视化 ====================

        // [Header("调试可视化")]
        [SerializeField] private string _currentState;
        [SerializeField] private int    _currentNodeRid;
        [SerializeField] private string _currentNodeName;
        [SerializeField] private string _currentNodeTypeName;

        /// <summary>【运行时图数据（Debug 查看）】</summary>
        [SerializeField] public FsmGraphData fsmData;

        /// <summary>【FSM 引擎】</summary>
        public FsmEngine Engine { get; private set; } = new();

        /// <summary>【FSM 初始化完成】</summary>
        public UnityEvent fsmInitialized = new();

        /// <summary>【FSM 播放开始】</summary>
        public UnityEvent fsmStarted = new();

        /// <summary>【FSM 播放结束】</summary>
        public UnityEvent fsmStopped = new();

        /// <summary>【FSM 事件触发】</summary>
        public UnityEvent<string> fsmEvent = new();

        /// <summary>【状态进入事件】—— 进入新状态时触发，参数为状态名</summary>
        public UnityEvent<string> fsmEnterState = new();

        /// <summary>【状态退出事件】—— 离开当前状态时触发，参数为状态名</summary>
        public UnityEvent<string> fsmExitState = new();

        void Awake() {
            Engine.debugLog = debugLog;
            Engine.maxExecStepsPerFrame = maxExecStepsPerFrame;
            Engine.maxDataStepsPerFrame = maxDataStepsPerFrame;
            Engine.StuckHandling = stuckHandling;
            // 执行器注册：由 FsmEngine.DefaultRegistry 全局单例提供，无需在此重复注册。
            // 如需为实例定制执行器，可在 Play 前对 Engine.Registry 显式赋值覆盖。

            // 将引擎事件桥接到 UnityEvent（Inspector 可视化绑定）
            Engine.OnEventInvoked += (msg) => fsmEvent?.Invoke(msg);
            Engine.OnStateEnter += (state) => fsmEnterState?.Invoke(state);
            Engine.OnStateExit += (state) => fsmExitState?.Invoke(state);
            Engine.OnInitialized += () => fsmInitialized?.Invoke();
            Engine.OnStarted += () => fsmStarted?.Invoke();
            Engine.OnStopped += () => fsmStopped?.Invoke();
        }

        void Start() {
            // 若编辑器"播放 Play"按钮（ForcePlayIfNotAutoStarted）已在本会话启动过，则跳过 autoPlay，避免重复
            if (autoPlay && !_autoPlayPerformed) {
                _autoPlayPerformed = true;
                Play();
            }
        }

        void Update() {
            Engine.Tick();

            // 同步调试可视化字段到 Inspector
            _currentState      = Engine.CurrentStateName ?? "";
            _currentNodeRid    = Engine.CurrentNodeRid;
            _currentNodeName   = Engine.CurrentNodeName ?? "";
            _currentNodeTypeName = Engine.CurrentNodeTypeName ?? "";
        }

        void OnDestroy() {
            Engine.Stop();
        }

        // ==================== 物理事件缓冲（FixedUpdate 写入 → Tick 读取） ====================

        void OnCollisionEnter(Collision other)     => Engine.Context?.CollisionEnters.Add((other, Time.frameCount));
        void OnCollisionStay(Collision other)      => Engine.Context?.CollisionStays.Add((other, Time.frameCount));
        void OnCollisionExit(Collision other)      => Engine.Context?.CollisionExits.Add((other, Time.frameCount));
        void OnCollisionEnter2D(Collision2D other) => Engine.Context?.CollisionEnters2D.Add((other, Time.frameCount));
        void OnCollisionStay2D(Collision2D other)  => Engine.Context?.CollisionStays2D.Add((other, Time.frameCount));
        void OnCollisionExit2D(Collision2D other)  => Engine.Context?.CollisionExits2D.Add((other, Time.frameCount));
        void OnTriggerEnter(Collider other)        => Engine.Context?.TriggerEnters.Add((other, Time.frameCount));
        void OnTriggerStay(Collider other)         => Engine.Context?.TriggerStays.Add((other, Time.frameCount));
        void OnTriggerExit(Collider other)         => Engine.Context?.TriggerExits.Add((other, Time.frameCount));
        void OnTriggerEnter2D(Collider2D other)    => Engine.Context?.TriggerEnters2D.Add((other, Time.frameCount));
        void OnTriggerStay2D(Collider2D other)     => Engine.Context?.TriggerStays2D.Add((other, Time.frameCount));
        void OnTriggerExit2D(Collider2D other)     => Engine.Context?.TriggerExits2D.Add((other, Time.frameCount));

        // ==================== 数据加载钩子 ====================

        /// <summary>子类重写此方法以提供各自的数据加载逻辑。基类默认报错。</summary>
        protected virtual void EnsureDeserialized() {
            Debug.LogError($"[{GetType().Name}] EnsureDeserialized() 未重写！请在子类中实现数据加载逻辑。");
        }

        /// <summary>将 JSON 文本反序列化为运行时数据结构</summary>
        protected void DeserializeFromJson(string jsonText) {
            GraphData data = JsonConvert.DeserializeObject<GraphData>(jsonText);
            fsmData = data.ToRuntime();
        }

        /// <summary>
        /// 解析当前数据源的 JSON 文本（仅供 Inspector 调试按钮调用；命中后立即解析并释放，
        /// 不缓存 GraphData DTO，避免长期持有造成内存泄漏）。无 JSON 数据源时返回 null。
        /// </summary>
        public virtual string GetDataSourceJson() => null;

        // ==================== 播放器生命周期 ====================

        /// <summary>初始化引擎并开始播放。如果引擎已初始化则直接恢复播放。</summary>
        public virtual void Play() {
            EnsureDeserialized();
            if (fsmData == null) return;

            if (!Engine.IsInitialized)
                Engine.Initialize(fsmData, this);

            Engine.Start();
        }

        /// <summary>
        /// 编辑器"播放 Play"按钮从非运行时进入 Play 模式后调用：保证本会话只启动一次，
        /// 与 autoPlay(Start) 互斥去重；否则"一进就失活"时 Stop 会重置 _isRunning，导致二次播放。
        /// </summary>
        public void ForcePlayIfNotAutoStarted() {
            if (_autoPlayPerformed) return;   // 本会话已由 autoPlay 启动
            _autoPlayPerformed = true;
            Play();
        }

        /// <summary>暂停播放（保留运行时状态，可通过 Play() 恢复）</summary>
        public void Pause() {
            Engine.Stop();
        }

        /// <summary>停止播放并重置所有参数为初始值，但不开始播放</summary>
        public void Stop() {
            Engine.Stop();
            Engine.ResetExposedParameters();
        }

        /// <summary>从头重新播放：先停止，重置所有参数为初始值，再开始播放</summary>
        public void ReStart() {
            Engine.Stop();
            Engine.ResetExposedParameters();
            Engine.Start();
        }

        // ==================== 外部 API ====================

        /// <summary>当前状态名（如 "Idle"）。字符串引用，零分配。</summary>
        public string CurrentStateName => Engine.CurrentStateName;

        /// <summary>按名称读取暴露参数原始值（struct 返回，栈分配）</summary>
        public bool TryGetParameter(string paramName, out FsmTypedValue value) =>
            Engine.TryGetParameter(paramName, out value);

        /// <summary>按名称读取 float 参数</summary>
        public bool TryGetFloat(string paramName, out float value) =>
            Engine.TryGetFloat(paramName, out value);

        /// <summary>按名称读取 int 参数</summary>
        public bool TryGetInt(string paramName, out int value) =>
            Engine.TryGetInt(paramName, out value);

        /// <summary>按名称读取 bool 参数</summary>
        public bool TryGetBool(string paramName, out bool value) =>
            Engine.TryGetBool(paramName, out value);

        /// <summary>按名称读取 string 参数</summary>
        public bool TryGetString(string paramName, out string value) =>
            Engine.TryGetString(paramName, out value);

        /// <summary>按名称读取 Vector4 参数</summary>
        public bool TryGetVector4(string paramName, out Vector4 value) =>
            Engine.TryGetVector4(paramName, out value);

        /// <summary>按名称读取 Color 参数</summary>
        public bool TryGetColor(string paramName, out Color value) =>
            Engine.TryGetColor(paramName, out value);

        // -------- 写 API --------

        /// <summary>设置暴露参数（浮点数）</summary>
        public void SetFloat(string paramName, float value) => Engine.SetExposedParameter(paramName, value);

        /// <summary>设置暴露参数（整数）</summary>
        public void SetInt(string paramName, int value) => Engine.SetExposedParameter(paramName, value);

        /// <summary>设置暴露参数（布尔）</summary>
        public void SetBool(string paramName, bool value) => Engine.SetExposedParameter(paramName, value);

        /// <summary>设置暴露参数（字符串）</summary>
        public void SetString(string paramName, string value) => Engine.SetExposedParameter(paramName, value);

        /// <summary>设置暴露参数（向量）</summary>
        public void SetVector4(string paramName, Vector4 value) => Engine.SetExposedParameter(paramName, value);

        /// <summary>设置暴露参数（颜色）</summary>
        public void SetColor(string paramName, Color value) => Engine.SetExposedParameter(paramName, value);
    }
}
