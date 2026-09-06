using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FSMGraph
{
    #region DTO —— 用于 JSON 序列化/反序列化
    /// <summary>【节点图数据 DTO】</summary>
    [Serializable]
    public class GraphData
    {
        public List<GraphNodeData> nodes;
        public List<int> exposedParameterRids; // 暴露参数节点的 rid 列表

        /// <summary>转换为运行时结构</summary>
        public FsmGraphData ToRuntime() {
            List<FsmNode> runtimeNodes = new(nodes.Count);
            foreach (var n in nodes)
                runtimeNodes.Add(n.ToRuntime());
            return new FsmGraphData {
                nodes = runtimeNodes,
                exposedParameterRids = exposedParameterRids ?? new List<int>(),
            };
        }
    }

    /// <summary>【节点数据 DTO】</summary>
    [Serializable]
    public class GraphNodeData
    {
        public int rid;
        public string name;               // nodeCustomName（空则用类名填充）
        public string typeName;           // 类名，如 "NGoToState"
        public string assembly;           // 程序集，如 "Assembly-CSharp"
        public float positionX;           // 节点在画布中的 X 坐标
        public float positionY;           // 节点在画布中的 Y 坐标
        public List<GraphPropertyData> properties;   // 自定义属性
        public List<GraphPortData> inputs;           // 输入端口
        public List<GraphPortData> outputs;          // 输出端口

        /// <summary>转换为运行时节点</summary>
        public FsmNode ToRuntime() {
            return new FsmNode {
                Rid = rid,
                Name = name,
                TypeName = typeName,
                Assembly = assembly,
                Properties = (properties ?? Enumerable.Empty<GraphPropertyData>())
                    .ToDictionary(p => p.name, p => p.ToRuntime()),
                Inputs = (inputs ?? Enumerable.Empty<GraphPortData>())
                    .ToDictionary(p => p.fieldName, p => p.ToRuntime()),
                Outputs = (outputs ?? Enumerable.Empty<GraphPortData>())
                    .ToDictionary(p => p.fieldName, p => p.ToRuntime()),
            };
        }
    }

    /// <summary>【节点属性 DTO】</summary>
    [Serializable]
    public class GraphPropertyData
    {
        public string name;       // 字段名
        public string typeName;   // "int" | "float" | "string" | "bool" | 枚举名如 "KeyCode"
        public object value;      // JSON 原生类型：数字 → double/long，字符串 → string，布尔 → bool

        /// <summary>转换为运行时 TypedValue</summary>
        public FsmTypedValue ToRuntime() {
            FsmTypedValue tv = new() { TypeName = typeName };
            if (value == null) return tv;
            switch (typeName) {
                case "int":
                case "long":
                    tv.IntValue = Convert.ToInt64(value);
                    break;
                case "float":
                case "double":
                    tv.FloatValue = Convert.ToDouble(value);
                    break;
                case "bool":
                    tv.BoolValue = Convert.ToBoolean(value);
                    break;
                case "string":
                    tv.StringValue = Convert.ToString(value);
                    break;
                // 复合类型：Color / Vector4 / Vector3 解析为 MultiValue
                case "Color":
                case "Vector4":
                case "Vector3":
                    if (value is JObject jo) {
                        tv.MultiValue = new Vector4(
                            (float)(jo["r"] ?? jo["x"] ?? 0f),
                            (float)(jo["g"] ?? jo["y"] ?? 0f),
                            (float)(jo["b"] ?? jo["z"] ?? 0f),
                            (float)(jo["a"] ?? jo["w"] ?? 0f));
                    }
                    break;
                // 枚举类型：KeyCode / LogType / MouseButton 等都按 int 存储
                default:
                    tv.IntValue = Convert.ToInt64(value);
                    break;
            }
            return tv;
        }
    }

    /// <summary>【端口数据 DTO】</summary>
    [Serializable]
    public class GraphPortData
    {
        public string fieldName;       // 端口字段名
        public string portTypeName;    // 端口数据类型："bool" | "exec" | "string" 等
        public object defaultValue;    // 端口默认值（输入端口无连接时使用）
        public List<GraphConnectionData> connections; // 连接列表

        /// <summary>转换为运行时端口</summary>
        public FsmPort ToRuntime() {
            return new FsmPort {
                FieldName = fieldName,
                PortTypeName = portTypeName,
                DefaultValue = defaultValue,
                Connections = connections ?? new List<GraphConnectionData>(),
            };
        }
    }

    /// <summary>【端口连接 DTO】</summary>
    [Serializable]
    public class GraphConnectionData
    {
        public int targetRid;          // 目标节点 rid
        public string targetPort;      // 目标节点输入端口的 fieldName
    }
    #endregion

    #region Runtime —— 用于 FSM 播放
    /// <summary>【FSM 节点图运行时数据】</summary>
    public class FsmGraphData
    {
        public List<FsmNode> nodes;
        public List<int> exposedParameterRids;

        /// <summary>按 rid 快速查找节点</summary>
        public Dictionary<int, FsmNode> nodeMap;

        /// <summary>构建查找字典，在反序列化后调用一次</summary>
        public void BuildLookup() {
            nodeMap = new Dictionary<int, FsmNode>(nodes.Count);
            foreach (var n in nodes)
                nodeMap[n.Rid] = n;
        }

        /// <summary>按 rid 获取节点</summary>
        public bool TryGetNode(int rid, out FsmNode node) => nodeMap.TryGetValue(rid, out node);
    }

    /// <summary>【FSM 运行时节点】</summary>
    public class FsmNode
    {
        public int Rid;
        public string Name;
        public string TypeName;
        public string Assembly;
        public Dictionary<string, FsmTypedValue> Properties;
        public Dictionary<string, FsmPort> Inputs;
        public Dictionary<string, FsmPort> Outputs;

        /// <summary>获取目标节点列表（从所有输出端口的连接中聚合）</summary>
        public IEnumerable<int> GetTargetRids() {
            foreach (var port in Outputs.Values)
                foreach (var conn in port.Connections)
                    yield return conn.targetRid;
        }
    }

    /// <summary>【强类型值 —— 导入后零拆箱访问】</summary>
    public struct FsmTypedValue
    {
        public string TypeName;
        public long IntValue;
        public double FloatValue;
        public bool BoolValue;
        public string StringValue;

        /// <summary>多分量值（Vector4 / Color 共用，4 个 float 布局一致）</summary>
        public Vector4 MultiValue;

        /// <summary>按类型自动选择存储字段读取 int（float→int 自动截断，Vector/Color→首分量）</summary>
        public int AsInt() => TypeName switch {
            "float" or "double"               => (int)FloatValue,
            "Vector4" or "Vector3" or "Color" => (int)MultiValue.x,
            _                                 => (int)IntValue
        };

        /// <summary>按类型自动选择存储字段读取 float（int→float 自动提升，Vector/Color→首分量）</summary>
        public float AsFloat() => TypeName switch {
            "int" or "long"                   => (float)IntValue,
            "Vector4" or "Vector3" or "Color" => MultiValue.x,
            _                                 => (float)FloatValue
        };

        /// <summary>读取完整 Vector4（x,y,z,w）</summary>
        public Vector4 AsVector4() => TypeName switch {
            "Vector4" or "Vector3" or "Color" => MultiValue,
            "float" or "double"               => new Vector4((float)FloatValue, 0, 0, 0),
            "int" or "long"                   => new Vector4(IntValue, 0, 0, 0),
            _                                 => Vector4.zero
        };

        /// <summary>读取 Color（r,g,b,a ≡ x,y,z,w）</summary>
        public Color AsColor() => new(
            MultiValue.x, MultiValue.y, MultiValue.z, MultiValue.w);

        public bool AsBool() => BoolValue;
        public string AsString() => StringValue;

        /// <summary>泛型取值（适用于枚举等需要 int 中转的类型）</summary>
        public T AsEnum<T>() where T : Enum => (T)Enum.ToObject(typeof(T), IntValue);

        /// <summary>快速构建 Vector4 值</summary>
        public static FsmTypedValue FromVector4(Vector4 v) => new() {
            TypeName = "Vector4", MultiValue = v
        };

        /// <summary>快速构建 Color 值</summary>
        public static FsmTypedValue FromColor(Color c) => new() {
            TypeName = "Color", MultiValue = new Vector4(c.r, c.g, c.b, c.a)
        };
    }

    /// <summary>【FSM 运行时端口】</summary>
    public class FsmPort
    {
        public string FieldName;
        public string PortTypeName;
        public object DefaultValue;
        public List<GraphConnectionData> Connections;
    }
    #endregion
}
