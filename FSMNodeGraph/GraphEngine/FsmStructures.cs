using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FSMGraph
{
    #region 执行结果

    /// <summary>节点执行结果 —— 告诉引擎下一步做什么</summary>
    public struct FsmExecutionResult
    {
        public enum ResultType
        {
            None,             // 无特殊操作，引擎沿所有 Executes* 端口推进
            ValueReady,       // 拉取求值完成，结果在 TypedValue 中
            SwitchState,      // 切换状态，StateName 为目标状态名
            WaitForSeconds,   // 等待 N 秒，FloatValue 为秒数
            WaitForFrames,    // 等待 N 帧，IntValue 为帧数
            ExecutePort,      // 沿指定名称的 Executes* 端口推进，PortName 为端口名
        }

        public ResultType Type;
        public FsmTypedValue TypedValue;
        public string StateName;
        public float FloatValue;
        public int IntValue;
        public string PortName;  // ExecutePort 时使用

        public static FsmExecutionResult None() => new() { Type = ResultType.None };
        public static FsmExecutionResult Value(FsmTypedValue v) => new() { Type = ResultType.ValueReady, TypedValue = v };
        public static FsmExecutionResult Switch(string state) => new() { Type = ResultType.SwitchState, StateName = state };
        public static FsmExecutionResult WaitSeconds(float s) => new() { Type = ResultType.WaitForSeconds, FloatValue = s };
        public static FsmExecutionResult WaitFrames(int f) => new() { Type = ResultType.WaitForFrames, IntValue = f };
        public static FsmExecutionResult ExecPort(string name) => new() { Type = ResultType.ExecutePort, PortName = name };
    }

    #endregion

    #region 执行器接口 & 注册表

    /// <summary>
    /// 节点执行器策略接口 —— 与具体节点类解耦，仅依赖运行时数据。
    /// 【约定】所有 IFsmNodeExecutor 实现必须为【无状态】：不得持有可变实例字段，
    /// 每次调用所需的上下文一律经 ctx/node 参数传入。执行器由 FsmEngine.DefaultRegistry
    /// 全局单例共享，若违反该约定会导致跨播放器实例的状态串扰。
    /// </summary>
    public interface IFsmNodeExecutor
    {
        /// <summary>是否可处理该类型节点</summary>
        bool CanExecute(string typeName);

        /// <summary>
        /// 执行节点逻辑。
        /// portName == null 表示控制流执行（沿 exec 链路推进）。
        /// portName != null 表示拉取求值（递归计算该输出端口的值）。
        /// </summary>
        FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName);
    }

    /// <summary>执行器注册表 —— typeName → executor 映射</summary>
    public class FsmNodeExecutorRegistry
    {
        private readonly Dictionary<string, IFsmNodeExecutor> _map = new();

        /// <summary>注册执行器（一对多：一个 executor 可处理多个 typeName）</summary>
        public void Register(string typeName, IFsmNodeExecutor executor) => _map[typeName] = executor;

        /// <summary>批量注册</summary>
        public void RegisterAll(params (string typeName, IFsmNodeExecutor executor)[] entries) {
            foreach (var (t, e) in entries) _map[t] = e;
        }

        /// <summary>按 typeName 获取执行器</summary>
        public IFsmNodeExecutor Get(string typeName) {
            _map.TryGetValue(typeName, out var e);
            return e;
        }
    }

    #endregion

    #region 执行上下文

    /// <summary>FSM 执行上下文 —— 持有运行时状态、参数、缓存</summary>
    public class FsmContext
    {
        /// <summary>所属引擎引用（用于递归 PullValue）</summary>
        public FsmEngine Engine;

        /// <summary>MonoBehaviour 宿主（用于 StartCoroutine）</summary>
        public MonoBehaviour Owner;

        /// <summary>运行时图数据</summary>
        public FsmGraphData GraphData;

        /// <summary>暴露参数值表 rid → 运行时值（外部可写）</summary>
        public readonly Dictionary<int, FsmTypedValue> ExposedParameters = new();

        /// <summary>每帧拉取求值缓存 (nodeRid, portName) → 值</summary>
        public readonly Dictionary<(int rid, string portName), FsmTypedValue> EvalCache = new();

        /// <summary>物理检测节流的随机相位偏移（秒），避免多实例同帧扎堆查询</summary>
        public float ThrottleOffset;

        /// <summary>当前激活的状态名（如 "Idle"），由引擎在 EnterState 时更新。外部只读，零分配。</summary>
        public string CurrentStateName = "Default";

        /// <summary>当前激活的状态节点（NInState）</summary>
        public FsmNode CurrentStateNode;

        /// <summary>物理检测节流缓存：rid → (上次计算时间, 命中数量)。跨帧保留，refreshRate=每秒次数。</summary>
        public readonly Dictionary<int, (float time, int hitCount)> PhysicsCache = new();

        /// <summary>射线检测节流缓存：rid → (上次计算时间, 命中结果)</summary>
        public readonly Dictionary<int, (float time, RaycastHit? hit)> RayDetectCache = new();

        /// <summary>FromJson 帧内解析缓存：rid → (帧号, 解析结果)。同帧多端口只解析一次。</summary>
        public readonly Dictionary<int, (int frame, JObject root)> FromJsonCache = new();

        // ---- 每帧物理事件缓冲（FixedUpdate 写入，Tick 读取并清理）----
        public readonly List<(Collision col, int frame)> CollisionEnters = new();
        public readonly List<(Collision col, int frame)> CollisionStays = new();
        public readonly List<(Collision col, int frame)> CollisionExits = new();
        public readonly List<(Collision2D col, int frame)> CollisionEnters2D = new();
        public readonly List<(Collision2D col, int frame)> CollisionStays2D = new();
        public readonly List<(Collision2D col, int frame)> CollisionExits2D = new();
        public readonly List<(Collider other, int frame)> TriggerEnters = new();
        public readonly List<(Collider other, int frame)> TriggerStays = new();
        public readonly List<(Collider other, int frame)> TriggerExits = new();
        public readonly List<(Collider2D other, int frame)> TriggerEnters2D = new();
        public readonly List<(Collider2D other, int frame)> TriggerStays2D = new();
        public readonly List<(Collider2D other, int frame)> TriggerExits2D = new();

        /// <summary>暴露参数 名称 → rid 映射（O(1) 查找，避免每次遍历列表）</summary>
        private Dictionary<string, int> _paramNameToRid;

        /// <summary>由引擎在 Initialize 时调用，构建 O(1) 参数名索引</summary>
        public void BuildParamLookup() {
            _paramNameToRid = new Dictionary<string, int>(GraphData.exposedParameterRids.Count);
            foreach (int r in GraphData.exposedParameterRids) {
                if (GraphData.nodeMap.TryGetValue(r, out var n))
                    _paramNameToRid[n.Name] = r;
            }
        }

        /// <summary>清空每帧临时缓存</summary>
        public void ClearFrameCache() => EvalCache.Clear();

        /// <summary>按参数名查找暴露参数的 rid（O(1)）</summary>
        public bool TryGetExposedParamRid(string paramName, out int rid) {
            if (_paramNameToRid != null)
                return _paramNameToRid.TryGetValue(paramName, out rid);
            rid = 0;
            return false;
        }
    }
    
    /// <summary>【FSM 对象失活处理方式】</summary>
    public enum FsmObjectStuckHandling {
        /// <summary>【不处理】Do nothing</summary>
        DoNothing = 0,
        /// <summary>【重启节点表，返回入口状态EntryState】Restart graph</summary>
        RestartGraph = 1,
        /// <summary>【停止状态机，不再恢复】Stop graph</summary>
        StopGraph = 2,
    }

    #endregion

    #region 方法库

    /// <summary>【FSM 方法库】</summary>
    public static partial class FSMLIBRARY
    {
        /// <summary>【FSM 文件路径类型】</summary>
        public enum FsmFilePathType
        {
            /// <summary>【地址：工程目录/Assets/StreamingAssets，打包目录/工程_Data/StreamingAssets】Path：*Project path*/Assets/StreamingAssets，*Build path*/*Project name*_Data/StreamingAssets</summary>
            StreamingAssets = 0,
            /// <summary>【地址：C:/Users/用户名/AppData/LocalLow/工程公司名/工程名】Path：C:/Users/*User name*/AppData/LocalLow/*Company name*/*Project name*</summary>
            AppData = 1,
            /// <summary>【自定义地址】Custom path</summary>
            Custom = 2,
        }

        /// <summary>【FSM Addressables 类型】</summary>
        public enum FsmAddressablesType
        {
            /// <summary>【路径名称】Path name</summary>
            PathName = 0,
            /// <summary>【资产引用】Asset reference</summary>
            AssetReference = 1,
        }

        /// <summary>【根据文件路径类型来拼合路径字符串】Assemble the path string according to the file path type.</summary>
        /// <param name="pathType">【文件路径类型】</param>
        /// <param name="path">【剩余路径】</param>
        /// <param name="pathType">【文件路径类型】File path type</param>
        /// <param name="path">【剩余路径】Remaining path</param>
        /// <returns>【拼合输出字符串（未进行存在性检测）】Combine output string (without existence check)</returns>
        public static string ConcatPathByPathType(FsmFilePathType pathType, string path) {
            string outPath = pathType switch {
                FsmFilePathType.StreamingAssets => path == string.Empty ? Application.streamingAssetsPath : System.IO.Path.Combine(Application.streamingAssetsPath, path),
                FsmFilePathType.AppData => path == string.Empty ? Application.persistentDataPath : System.IO.Path.Combine(Application.persistentDataPath, path),
                FsmFilePathType.Custom => path,
                _ => path == string.Empty ? Application.streamingAssetsPath : System.IO.Path.Combine(Application.streamingAssetsPath, path),
            };
            return outPath;
        }

        /// <summary>
        /// <para>【将文本txtText写入"folderPath\fileName.fileSuffix"中】Write txtText into "folderPath\fileName.fileSuffix"</para>
        /// <para>EX: Global.WriteTextFile("测试文本", $"D:\\TestSample\\Test", "SampleText");</para>
        /// <para>EX: Global.WriteTextFile("测试文本", $"{Application.persistentDataPath}\\Test", "SampleText");</para>
        /// </summary>
        /// <param name="txtText">【写入文本】Text</param>
        /// <param name="folderPath">【写入文件路径】Folder path</param>
        /// <param name="fileName">【写入文件名】File name</param>
        /// <param name="fileExtension">【写入文件后缀】File extension</param>
        public static void WriteTextFile(string txtText, string folderPath, string fileName, string fileExtension = ".txt") {
            string folder = $"{folderPath}";
            //若不存在路径
            if (!Directory.Exists(folder)) {
                DirectoryInfo info = new DirectoryInfo(folder);
                info.Create();
            }
            string path = $"{folder}\\{fileName}{fileExtension}";//文件流创建一个文本文件
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
        /// <summary>
        /// <para>【将文本txtText写入fullFilePath中】Write txtText into "folderPath\fileName.fileSuffix"</para>
        /// <para>EX: Global.WriteTextFile("测试文本", $"D:\\TestSample\\Test\\SampleText.txt");</para>
        /// <para>EX: Global.WriteTextFile("测试文本", $"{Application.persistentDataPath}\\Test\\SampleText.txt");</para>
        /// </summary>
        /// <param name="txtText">【写入文本】Text</param>
        /// <param name="fullFilePath">【写入文件路径】Full file path</param>
        public static void WriteTextFile(string txtText, string fullFilePath) {
            string fileDirectory = Path.GetDirectoryName(fullFilePath);
            string fileName = Path.GetFileNameWithoutExtension(fullFilePath);
            string fileExtension = Path.GetExtension(fullFilePath);
            WriteTextFile(txtText, fileDirectory, fileName, fileExtension);
        }
        
        /// <summary>
        /// <para>【读取"folderPath\fileName.fileSuffix"中文本】Read text from "folderPath\fileName.fileSuffix"</para>
        /// <para>EX: print(Global.ReadTextFile($"D:\\TestSample\\Test", "SampleText"));</para>
        /// <para>EX: print(Global.ReadTextFile($"{Application.persistentDataPath}\\Test", "SampleText"));</para>
        /// </summary>
        /// <param name="folderPath">【读取文件路径】Folder path</param>
        /// <param name="fileName">【读取文件名】File name</param>
        /// <param name="fileExtension">【读取文件后缀】File extension</param>
        /// <returns>【读取文件内容】Read file content</returns>
        public static string ReadTextFile(string folderPath, string fileName, string fileExtension = ".txt") {
            string folder = $"{folderPath}";
            if (!Directory.Exists(folder))//若不存在路径
            {
                return string.Empty;
            }
            string path = $"{folder}\\{fileName}{fileExtension}";
            StreamReader reader = null;
            reader = File.OpenText(path);
            string fileRawText = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            return fileRawText;
        }
        /// <summary>
        /// <para>【读取fullFilePath中文本】Read text from fullFilePath</para>
        /// <para>EX: print(Global.ReadTextFile($"D:\\TestSample\\Test\\SampleText.txt"));</para>
        /// <para>EX: print(Global.ReadTextFile($"{Application.persistentDataPath}\\Test\\SampleText.txt"));</para>
        /// </summary>
        /// <param name="fullFilePath">【读取文件路径】Full file path</param>
        /// <returns>【读取文件内容】Read file content</returns>
        public static string ReadTextFile(string fullFilePath) {
            string fileDirectory = Path.GetDirectoryName(fullFilePath);
            string fileName = Path.GetFileNameWithoutExtension(fullFilePath);
            string fileExtension = Path.GetExtension(fullFilePath);
            return ReadTextFile(fileDirectory, fileName, fileExtension);
        }
    }

    #endregion
}
