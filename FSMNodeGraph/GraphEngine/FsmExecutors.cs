using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FSMGraph
{
    #region 状态节点

    /// <summary>NEntryState: 入口节点 —— 仅控制流，由引擎在 Start 时查找</summary>
    public class EntryStateExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NEntryState";
        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) => FsmExecutionResult.None();
    }

    /// <summary>NGoToState: 切换状态 —— 返回 SwitchState 指令</summary>
    public class GoToStateExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NGoToState";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName == null) {
                string target;
                // state 作为输入端口时 → PullValue 处理动态连接/默认值
                // state 作为属性时 → 从 Properties 读取（向后兼容旧版 JSON）
                if (node.Inputs.TryGetValue("state", out _))
                    target = ctx.Engine.PullValue(ctx, node.Rid, "state").AsString();
                else
                    target = node.Properties.TryGetValue("state", out var v) ? v.AsString() : "";
                return FsmExecutionResult.Switch(target);
            }
            return FsmExecutionResult.None();
        }
    }

    /// <summary>NInState: 进入状态 —— 仅控制流，由引擎在 EnterState 时处理</summary>
    public class InStateExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NInState";
        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) => FsmExecutionResult.None();
    }

    /// <summary>NInAnyState: 进入任意状态 —— 仅控制流，由引擎在每次 EnterState 时优先推进其 Executes 链</summary>
    public class InAnyStateExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NInAnyState";
        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) => FsmExecutionResult.None();
    }

    #endregion

    #region 条件节点

    /// <summary>NPerformed: 条件满足节点 —— 控制流，由引擎每帧监测其 condition 输入</summary>
    public class PerformedExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NPerformed";
        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) => FsmExecutionResult.None();
    }

    /// <summary>NCompareNumber: 数字比较 —— 数据拉取，按 function 枚举比较 a 和 b</summary>
    public class CompareNumberExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NCompareNumber";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            float a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsFloat();
            float b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsFloat();
            int func = node.Properties.TryGetValue("function", out var f) ? f.AsInt() : 0;

            // NodeNumberCompareMethod: 0=Equal, 1=NotEqual, 2=Greater, 3=Less, 4=GreaterEqual, 5=LessEqual
            bool result = func switch {
                0 => Mathf.Approximately(a, b),
                1 => !Mathf.Approximately(a, b),
                2 => a > b,
                3 => a < b,
                4 => a >= b,
                5 => a <= b,
                _ => false
            };
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = result });
        }
    }

    /// <summary>NListenInputKey: 按键监听 —— 数据拉取，返回实时按键状态</summary>
    public class ListenInputKeyExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NListenInputKey";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int keyCode = node.Properties.TryGetValue("keyCode", out var k) ? k.AsInt() : 0;
            KeyCode key = (KeyCode)keyCode;

            bool value = portName switch {
                "onKeyDown" => Input.GetKeyDown(key),
                "onKeyUp" => Input.GetKeyUp(key),
                "onKey" => Input.GetKey(key),
                _ => false
            };
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = value });
        }
    }

    /// <summary>NRayDetect: 射线检测 —— 按秒级 refreshRate 节流，忽略自身碰撞箱</summary>
    public class RayDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NRayDetect";

        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int rate = ctx.Engine.PullValue(ctx, node.Rid, "detectRefreshRate").AsInt();
            rate = Mathf.Max(1, rate);
            float interval = 1f / rate;

            // ---- 秒级节流：窗口内直接返回缓存 ----
            if (ctx.RayDetectCache.TryGetValue(node.Rid, out var cached)
                && Time.time - cached.time < interval) {
                return HitResult(cached.hit, portName);
            }

            // ---- 拉取输入 ----
            Vector4 origin4    = ctx.Engine.PullValue(ctx, node.Rid, "origin").AsVector4();
            Vector4 direction4 = ctx.Engine.PullValue(ctx, node.Rid, "direction").AsVector4();
            float distance     = ctx.Engine.PullValue(ctx, node.Rid, "distance").AsFloat();
            bool relative      = ctx.Engine.PullValue(ctx, node.Rid, "relative").AsBool();
            int layerMask      = ctx.Engine.PullValue(ctx, node.Rid, "layerMask").AsInt();
            bool inclChildren  = ctx.Engine.PullValue(ctx, node.Rid, "includeChildren").AsBool();
            bool showDebug     = node.Properties.TryGetValue("showDebug", out var sd) && sd.AsBool();

            Vector3 origin = new(origin4.x, origin4.y, origin4.z);
            Vector3 dir = new Vector3(direction4.x, direction4.y, direction4.z).normalized;

            if (relative && ctx.Owner != null) {
                origin = ctx.Owner.transform.TransformPoint(origin);
                dir    = ctx.Owner.transform.TransformDirection(dir);
            }

            float maxDist = distance < 0f ? Mathf.Infinity : distance;

            int hitCount  = Physics.RaycastNonAlloc(origin, dir, _hitBuffer, maxDist, layerMask);

            if (showDebug)
                Debug.DrawRay(origin, dir * Mathf.Min(maxDist, 100f),
                    hitCount > 0 ? Color.green : Color.red, 0f);

            // ---- 过滤：自身碰撞箱永远跳过；includeChildren=false 时跳过子级 ----
            RaycastHit? best = null;
            GameObject ownerGo = ctx.Owner != null ? ctx.Owner.gameObject : null;
            for (int i = 0; i < hitCount; i++) {
                var t = _hitBuffer[i].transform;
                // 自身根碰撞箱 — 永远跳过
                if (_hitBuffer[i].collider.gameObject == ownerGo) continue;
                // 子级 — includeChildren=false 时也跳过
                if (!inclChildren && ownerGo != null && t.IsChildOf(ownerGo.transform)) continue;
                if (best == null || _hitBuffer[i].distance < best.Value.distance)
                    best = _hitBuffer[i];
            }

            // 缓存真实查询时间（不能加 ThrottleOffset）：节流判断用 Time.time - cached.time < interval，
            // 若存的是未来时间戳，结果会被冻结约 ThrottleOffset 秒，导致检测的进入/退出明显滞后。
            ctx.RayDetectCache[node.Rid] = (Time.time, best);
            return HitResult(best, portName);
        }

        private static FsmExecutionResult HitResult(RaycastHit? hit, string portName) {
            if (!hit.HasValue) return NoHitDefault(portName);
            var h = hit.Value;
            return portName switch {
                "didHit"        => Bool(true),
                "hitPoint"      => Vec4(h.point),
                "hitNormal"     => Vec4(h.normal),
                "hitDistance"   => Float(h.distance),
                "hitObjectName" => Str(h.collider.gameObject.name),
                _               => NoHitDefault(portName),
            };
        }

        private static FsmExecutionResult NoHitDefault(string portName) => portName switch {
            "hitPoint"      => Vec4(Vector3.zero),
            "hitNormal"     => Vec4(Vector3.zero),
            "hitObjectName" => Str(""),
            "hitDistance"   => Float(0f),
            _               => Bool(false),
        };
        private static FsmExecutionResult Bool(bool v)   => Val(new FsmTypedValue { TypeName = "bool",   BoolValue = v });
        private static FsmExecutionResult Float(float v) => Val(new FsmTypedValue { TypeName = "float",  FloatValue = v });
        private static FsmExecutionResult Str(string v)  => Val(new FsmTypedValue { TypeName = "string", StringValue = v });
        private static FsmExecutionResult Vec4(Vector3 v)=> Val(FsmTypedValue.FromVector4(new Vector4(v.x, v.y, v.z, 0f)));
        private static FsmExecutionResult Val(FsmTypedValue tv) =>
            new() { Type = FsmExecutionResult.ResultType.ValueReady, TypedValue = tv };
    }

    /// <summary>
    /// NOverlapBoxDetect: 箱型重叠检测 —— 按秒级 refreshRate 节流，自身碰撞箱永远排除。
    /// </summary>
    public class OverlapBoxDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NOverlapBoxDetect";

        private static readonly Collider[] _colBuffer = new Collider[64];

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int rate = ctx.Engine.PullValue(ctx, node.Rid, "detectRefreshRate").AsInt();
            rate = Mathf.Max(1, rate);
            float interval = 1f / rate;

            // ---- 秒级节流：窗口内直接返回缓存 ----
            if (ctx.PhysicsCache.TryGetValue(node.Rid, out var cached)
                && Time.time - cached.time < interval) {
                return portName switch {
                    "didOverlap"        => Bool(cached.hitCount > 0),
                    "overlapObjectCount"=> Int(cached.hitCount),
                    _                   => Bool(false),
                };
            }

            // ---- 拉取输入 ----
            Vector4 origin4   = ctx.Engine.PullValue(ctx, node.Rid, "origin").AsVector4();
            Vector4 size4     = ctx.Engine.PullValue(ctx, node.Rid, "size").AsVector4();
            bool relative     = ctx.Engine.PullValue(ctx, node.Rid, "relative").AsBool();
            int layerMask     = ctx.Engine.PullValue(ctx, node.Rid, "layerMask").AsInt();
            bool inclChildren = ctx.Engine.PullValue(ctx, node.Rid, "includeChildren").AsBool();
            bool showDebug    = node.Properties.TryGetValue("showDebug", out var sd) && sd.AsBool();

            Vector3 center = new(origin4.x, origin4.y, origin4.z);
            Vector3 half   = new(size4.x * 0.5f, size4.y * 0.5f, size4.z * 0.5f);

            if (relative && ctx.Owner != null)
                center = ctx.Owner.transform.TransformPoint(center);

            Quaternion rot = relative && ctx.Owner ? ctx.Owner.transform.rotation : Quaternion.identity;

            int hitCount = Physics.OverlapBoxNonAlloc(center, half, _colBuffer, rot, layerMask);

            // ---- 过滤：自身碰撞箱永远跳过；includeChildren=false 时跳过子级 ----
            int validHits = 0;
            GameObject ownerGo = ctx.Owner != null ? ctx.Owner.gameObject : null;
            for (int i = 0; i < hitCount; i++) {
                if (_colBuffer[i].gameObject == ownerGo) continue;
                if (!inclChildren && ownerGo != null && _colBuffer[i].transform.IsChildOf(ownerGo.transform)) continue;
                validHits++;
            }

            ctx.PhysicsCache[node.Rid] = (Time.time, validHits); // 存真实时间，避免未来时间戳冻结检测结果

            if (showDebug)
                DrawBoxDebug(center, half, rot, validHits > 0 ? Color.green : Color.red);

            return portName switch {
                "didOverlap"         => Bool(validHits > 0),
                "overlapObjectCount" => Int(validHits),
                _                    => Bool(false),
            };
        }

        private static void DrawBoxDebug(Vector3 center, Vector3 half, Quaternion rot, Color color) {
            Vector3 x = rot * new Vector3(half.x, 0, 0);
            Vector3 y = rot * new Vector3(0, half.y, 0);
            Vector3 z = rot * new Vector3(0, 0, half.z);
            Vector3[] corners = {
                center - x - y - z, center + x - y - z, center + x + y - z, center - x + y - z,
                center - x - y + z, center + x - y + z, center + x + y + z, center - x + y + z,
            };
            // 底面
            Debug.DrawLine(corners[0], corners[1], color); Debug.DrawLine(corners[1], corners[2], color);
            Debug.DrawLine(corners[2], corners[3], color); Debug.DrawLine(corners[3], corners[0], color);
            // 顶面
            Debug.DrawLine(corners[4], corners[5], color); Debug.DrawLine(corners[5], corners[6], color);
            Debug.DrawLine(corners[6], corners[7], color); Debug.DrawLine(corners[7], corners[4], color);
            // 侧边
            Debug.DrawLine(corners[0], corners[4], color); Debug.DrawLine(corners[1], corners[5], color);
            Debug.DrawLine(corners[2], corners[6], color); Debug.DrawLine(corners[3], corners[7], color);
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>
    /// NOverlapSphereDetect: 球型重叠检测 —— 按秒级 refreshRate 节流，自身碰撞箱永远排除。
    /// </summary>
    public class OverlapSphereDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NOverlapSphereDetect";

        private static readonly Collider[] _colBuffer = new Collider[64];

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int rate = ctx.Engine.PullValue(ctx, node.Rid, "detectRefreshRate").AsInt();
            rate = Mathf.Max(1, rate);
            float interval = 1f / rate;

            // ---- 秒级节流：窗口内直接返回缓存 ----
            if (ctx.PhysicsCache.TryGetValue(node.Rid, out var cached)
                && Time.time - cached.time < interval) {
                return portName switch {
                    "didOverlap"        => Bool(cached.hitCount > 0),
                    "overlapObjectCount"=> Int(cached.hitCount),
                    _                   => Bool(false),
                };
            }

            // ---- 拉取输入 ----
            Vector4 origin4   = ctx.Engine.PullValue(ctx, node.Rid, "origin").AsVector4();
            float radius      = ctx.Engine.PullValue(ctx, node.Rid, "radius").AsFloat();
            bool relative     = ctx.Engine.PullValue(ctx, node.Rid, "relative").AsBool();
            int layerMask     = ctx.Engine.PullValue(ctx, node.Rid, "layerMask").AsInt();
            bool inclChildren = ctx.Engine.PullValue(ctx, node.Rid, "includeChildren").AsBool();
            bool showDebug    = node.Properties.TryGetValue("showDebug", out var sd) && sd.AsBool();

            Vector3 center = new(origin4.x, origin4.y, origin4.z);

            if (relative && ctx.Owner != null)
                center = ctx.Owner.transform.TransformPoint(center);

            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, _colBuffer, layerMask);

            // ---- 过滤：自身碰撞箱永远跳过；includeChildren=false 时跳过子级 ----
            int validHits = 0;
            GameObject ownerGo = ctx.Owner != null ? ctx.Owner.gameObject : null;
            for (int i = 0; i < hitCount; i++) {
                if (_colBuffer[i].gameObject == ownerGo) continue;
                if (!inclChildren && ownerGo != null && _colBuffer[i].transform.IsChildOf(ownerGo.transform)) continue;
                validHits++;
            }

            ctx.PhysicsCache[node.Rid] = (Time.time, validHits); // 存真实时间，避免未来时间戳冻结检测结果

            if (showDebug)
                DrawSphereDebug(center, radius, validHits > 0 ? Color.green : Color.red);

            return portName switch {
                "didOverlap"         => Bool(validHits > 0),
                "overlapObjectCount" => Int(validHits),
                _                    => Bool(false),
            };
        }

        private static void DrawSphereDebug(Vector3 center, float radius, Color color) {
            int segs = 24;
            for (int i = 0; i < segs; i++) {
                float a0 = i * 2f * Mathf.PI / segs;
                float a1 = (i + 1) * 2f * Mathf.PI / segs;
                Debug.DrawLine(
                    center + new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0),
                    center + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0), color);
                Debug.DrawLine(
                    center + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius),
                    center + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius), color);
                Debug.DrawLine(
                    center + new Vector3(0, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius),
                    center + new Vector3(0, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius), color);
            }
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>
    /// NCollisionDetect: 碰撞检测 —— 从 FsmContext 缓冲读取本帧 Collision/Collision2D 事件。
    /// </summary>
    public class CollisionDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NCollisionDetect";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int mask = ctx.Engine.PullValue(ctx, node.Rid, "passLayerMask").AsInt();

            // 配置自身刚体睡眠参数（仅自身，轻量 idempotent 操作）
            float threshold = node.Properties.TryGetValue("selfRBDSleepThreshold", out var t) ? t.AsFloat() : 0.005f;
            int   sleepMode = node.Properties.TryGetValue("selfRBG2DSleepMode", out var m) ? m.AsInt() : 0;
            var rb   = ctx.Owner?.GetComponent<Rigidbody>();
            if (rb   != null) rb.sleepThreshold = threshold;
            var rb2D = ctx.Owner?.GetComponent<Rigidbody2D>();
            if (rb2D != null) rb2D.sleepMode = (RigidbodySleepMode2D)sleepMode;

            int frame = Time.frameCount;

            int ent3D = Count3D(ctx.CollisionEnters, frame, mask);
            int sty3D = Count3D(ctx.CollisionStays,  frame, mask);
            int ext3D = Count3D(ctx.CollisionExits,  frame, mask);
            int ent2D = Count2D(ctx.CollisionEnters2D, frame, mask);
            int sty2D = Count2D(ctx.CollisionStays2D,  frame, mask);
            int ext2D = Count2D(ctx.CollisionExits2D,  frame, mask);

            return portName switch {
                "onCollisionEnter"   => Bool(ent3D > 0),
                "onCollisionStay"    => Bool(sty3D > 0),
                "onCollisionExit"    => Bool(ext3D > 0),
                "onCollisionEnter2D" => Bool(ent2D > 0),
                "onCollisionStay2D"  => Bool(sty2D > 0),
                "onCollisionExit2D"  => Bool(ext2D > 0),
                "enterObjectCount"   => Int(ent3D + ent2D),
                "stayObjectCount"    => Int(sty3D + sty2D),
                "exitObjectCount"    => Int(ext3D + ext2D),
                _                    => Bool(false),
            };
        }

        private static int Count3D(List<(Collision col, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.col.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }
        private static int Count2D(List<(Collision2D col, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.col.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>
    /// NTriggerDetect: 触发检测 —— 从 FsmContext 缓冲读取本帧 Collider/Collider2D 事件。
    /// </summary>
    public class TriggerDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NTriggerDetect";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int mask = ctx.Engine.PullValue(ctx, node.Rid, "passLayerMask").AsInt();

            // 配置自身刚体睡眠参数（仅自身，轻量 idempotent 操作）
            float threshold = node.Properties.TryGetValue("selfRBDSleepThreshold", out var t) ? t.AsFloat() : 0.005f;
            int   sleepMode = node.Properties.TryGetValue("selfRBG2DSleepMode", out var m) ? m.AsInt() : 0;
            var rb   = ctx.Owner?.GetComponent<Rigidbody>();
            if (rb   != null) rb.sleepThreshold = threshold;
            var rb2D = ctx.Owner?.GetComponent<Rigidbody2D>();
            if (rb2D != null) rb2D.sleepMode = (RigidbodySleepMode2D)sleepMode;

            int frame = Time.frameCount;

            int ent3D = Count3D(ctx.TriggerEnters, frame, mask);
            int sty3D = Count3D(ctx.TriggerStays,  frame, mask);
            int ext3D = Count3D(ctx.TriggerExits,  frame, mask);
            int ent2D = Count2D(ctx.TriggerEnters2D, frame, mask);
            int sty2D = Count2D(ctx.TriggerStays2D,  frame, mask);
            int ext2D = Count2D(ctx.TriggerExits2D,  frame, mask);

            return portName switch {
                "onTriggerEnter"   => Bool(ent3D > 0),
                "onTriggerStay"    => Bool(sty3D > 0),
                "onTriggerExit"    => Bool(ext3D > 0),
                "onTriggerEnter2D" => Bool(ent2D > 0),
                "onTriggerStay2D"  => Bool(sty2D > 0),
                "onTriggerExit2D"  => Bool(ext2D > 0),
                "enterObjectCount" => Int(ent3D + ent2D),
                "stayObjectCount"  => Int(sty3D + sty2D),
                "exitObjectCount"  => Int(ext3D + ext2D),
                _                  => Bool(false),
            };
        }

        private static int Count3D(List<(Collider other, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.other.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }
        private static int Count2D(List<(Collider2D other, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.other.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>
    /// NCollisionProxyDetect: 远程碰撞检测 —— 通过 proxyId 读取 FsmCollisionProxy 本帧缓冲事件。
    /// </summary>
    public class CollisionProxyDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NCollisionProxyDetect";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            string proxyId = ctx.Engine.PullValue(ctx, node.Rid, "proxyId").AsString();
            var proxy = FsmCollisionProxy.Find(proxyId);
            int mask = ctx.Engine.PullValue(ctx, node.Rid, "passLayerMask").AsInt();

            if (proxy == null) {
                if (ctx.Engine.debugLog)
                    Debug.LogWarning($"[FsmEngine] NCollisionProxyDetect 未找到 proxyId=[{proxyId}]，全部输出为 false/0。");
                return Bool(false);
            }

            int frame = Time.frameCount;
            int ent3D = Count3D(proxy.CollisionEnters, frame, mask);
            int sty3D = Count3D(proxy.CollisionStays,  frame, mask);
            int ext3D = Count3D(proxy.CollisionExits,  frame, mask);
            int ent2D = Count2D(proxy.CollisionEnters2D, frame, mask);
            int sty2D = Count2D(proxy.CollisionStays2D,  frame, mask);
            int ext2D = Count2D(proxy.CollisionExits2D,  frame, mask);

            return portName switch {
                "onCollisionEnter"   => Bool(ent3D > 0),
                "onCollisionStay"    => Bool(sty3D > 0),
                "onCollisionExit"    => Bool(ext3D > 0),
                "onCollisionEnter2D" => Bool(ent2D > 0),
                "onCollisionStay2D"  => Bool(sty2D > 0),
                "onCollisionExit2D"  => Bool(ext2D > 0),
                "enterObjectCount"   => Int(ent3D + ent2D),
                "stayObjectCount"    => Int(sty3D + sty2D),
                "exitObjectCount"    => Int(ext3D + ext2D),
                _                    => Bool(false),
            };
        }

        private static int Count3D(List<(Collision col, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.col.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }
        private static int Count2D(List<(Collision2D col, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.col.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>
    /// NTriggerProxyDetect: 远程触发检测 —— 通过 proxyId 读取 FsmCollisionProxy 本帧缓冲事件。
    /// </summary>
    public class TriggerProxyDetectExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NTriggerProxyDetect";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            string proxyId = ctx.Engine.PullValue(ctx, node.Rid, "proxyId").AsString();
            var proxy = FsmCollisionProxy.Find(proxyId);
            int mask = ctx.Engine.PullValue(ctx, node.Rid, "passLayerMask").AsInt();

            if (proxy == null) {
                if (ctx.Engine.debugLog)
                    Debug.LogWarning($"[FsmEngine] NTriggerProxyDetect 未找到 proxyId=[{proxyId}]，全部输出为 false/0。");
                return Bool(false);
            }

            int frame = Time.frameCount;
            int ent3D = Count3D(proxy.TriggerEnters, frame, mask);
            int sty3D = Count3D(proxy.TriggerStays,  frame, mask);
            int ext3D = Count3D(proxy.TriggerExits,  frame, mask);
            int ent2D = Count2D(proxy.TriggerEnters2D, frame, mask);
            int sty2D = Count2D(proxy.TriggerStays2D,  frame, mask);
            int ext2D = Count2D(proxy.TriggerExits2D,  frame, mask);

            return portName switch {
                "onTriggerEnter"   => Bool(ent3D > 0),
                "onTriggerStay"    => Bool(sty3D > 0),
                "onTriggerExit"    => Bool(ext3D > 0),
                "onTriggerEnter2D" => Bool(ent2D > 0),
                "onTriggerStay2D"  => Bool(sty2D > 0),
                "onTriggerExit2D"  => Bool(ext2D > 0),
                "enterObjectCount" => Int(ent3D + ent2D),
                "stayObjectCount"  => Int(sty3D + sty2D),
                "exitObjectCount"  => Int(ext3D + ext2D),
                _                  => Bool(false),
            };
        }

        private static int Count3D(List<(Collider other, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.other.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }
        private static int Count2D(List<(Collider2D other, int f)> list, int frame, int mask) {
            int n = 0;
            for (int i = 0; i < list.Count; i++) {
                var e = list[i];
                if (e.f != frame) continue;
                if (mask != -1 && ((1 << e.other.gameObject.layer) & mask) == 0) continue;
                n++;
            }
            return n;
        }

        private static FsmExecutionResult Bool(bool v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "bool", BoolValue = v } };
        private static FsmExecutionResult Int(int v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "int", IntValue = v } };
    }

    /// <summary>NListenInputMouse: 鼠标监听 —— 数据拉取</summary>
    public class ListenInputMouseExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NListenInputMouse";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int btn = node.Properties.TryGetValue("mouseButton", out var b) ? b.AsInt() : 0;

            bool value = portName switch {
                "onMouseDown" => Input.GetMouseButtonDown(btn),
                "onMouseUp" => Input.GetMouseButtonUp(btn),
                "onMouse" => Input.GetMouseButton(btn),
                _ => false
            };
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = value });
        }
    }

    #endregion

    #region 逻辑节点

    /// <summary>NLogicAnd: 逻辑与 —— 数据拉取</summary>
    public class LogicAndExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NLogicAnd";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            bool a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsBool();
            bool b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsBool();
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = a && b });
        }
    }

    /// <summary>NLogicOr: 逻辑或 —— 数据拉取</summary>
    public class LogicOrExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NLogicOr";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            bool a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsBool();
            bool b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsBool();
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = a || b });
        }
    }

    /// <summary>NLogicXor: 逻辑异或 —— 数据拉取，a != b 时为真</summary>
    public class LogicXorExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NLogicXor";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            bool a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsBool();
            bool b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsBool();
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = a != b });
        }
    }

    /// <summary>NLogicNegate: 逻辑取反 —— 数据拉取，!a</summary>
    public class LogicNegateExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NLogicNegate";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            bool a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsBool();
            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = !a });
        }
    }

    #endregion

    #region 延时节点

    /// <summary>NWaitSeconds: 等待秒数 —— 控制流，返回 WaitForSeconds 指令</summary>
    public class WaitSecondsExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NWaitSeconds";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            // 数据端口：progress —— 实时等待进度 0~1
            if (portName == "progress")
                return Float(ctx.Engine.GetWaitProgress(node.Rid));
            // 控制流：进入等待
            float seconds = node.Inputs.TryGetValue("seconds", out _)
                ? ctx.Engine.PullValue(ctx, node.Rid, "seconds").AsFloat()
                : (node.Properties.TryGetValue("seconds", out var v) ? v.AsFloat() : 0f);
            return FsmExecutionResult.WaitSeconds(seconds);
        }

        private static FsmExecutionResult Float(float v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "float", FloatValue = v } };
    }

    /// <summary>NWaitFrames: 等待帧数 —— 控制流，返回 WaitForFrames 指令</summary>
    public class WaitFramesExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NWaitFrames";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            // 数据端口：progress —— 实时等待进度 0~1
            if (portName == "progress")
                return Float(ctx.Engine.GetWaitProgress(node.Rid));
            // 控制流：进入等待
            int frames = node.Inputs.TryGetValue("frames", out _)
                ? ctx.Engine.PullValue(ctx, node.Rid, "frames").AsInt()
                : (node.Properties.TryGetValue("frames", out var v) ? v.AsInt() : 0);
            return FsmExecutionResult.WaitFrames(frames);
        }

        private static FsmExecutionResult Float(float v) => new() {
            Type = FsmExecutionResult.ResultType.ValueReady,
            TypedValue = new FsmTypedValue { TypeName = "float", FloatValue = v } };
    }

    /// <summary>NNodeOrder: 分支排序 —— 纯透传执行节点，排序已由引擎 ProcessConnections 完成</summary>
    public class NodeOrderExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NNodeOrder";
        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            // 透传：不产生行为，返回 None 让引擎沿 executes 继续推进下游
            return new FsmExecutionResult { Type = FsmExecutionResult.ResultType.None };
        }
    }

    #endregion

    #region 调试 / 事件节点

    /// <summary>NDebugLog: 日志输出 —— 控制流，打印后继续。支持 obj 输入端口附加任意数据</summary>
    public class DebugLogExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NDebugLog";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            string text = node.Properties.TryGetValue("text", out var t) ? t.AsString() : "";

            // 拉取 obj 输入端口的连接值（若有连接则递归求值，否则取默认值）
            FsmTypedValue objVal = ctx.Engine.PullValue(ctx, node.Rid, "obj");
            if (!string.IsNullOrEmpty(objVal.TypeName)) {
                string objStr = TypedValueToString(objVal);
                text = $"{text} | {objStr}";
            }

            int logType = node.Properties.TryGetValue("logType", out var lt) ? lt.AsInt() : 0;

            switch ((LogType)logType) {
                case LogType.Warning: Debug.LogWarning(text); break;
                case LogType.Error: Debug.LogError(text); break;
                default: Debug.Log(text); break;
            }
            return FsmExecutionResult.None();
        }

        /// <summary>将 FsmTypedValue 转为人类可读字符串</summary>
        private static string TypedValueToString(FsmTypedValue v) {
            return v.TypeName switch {
                "string"              => $"\"{v.AsString()}\"",
                "float" or "double"   => v.AsFloat().ToString("0.####"),
                "int" or "long"       => v.AsInt().ToString(),
                "bool"                => v.AsBool().ToString(),
                "Color"               => $"RGBA({v.MultiValue.x:F4}, {v.MultiValue.y:F4}, {v.MultiValue.z:F4}, {v.MultiValue.w:F4})",
                "Vector4"             => $"({v.MultiValue.x:F4}, {v.MultiValue.y:F4}, {v.MultiValue.z:F4}, {v.MultiValue.w:F4})",
                "Vector3"             => $"({v.MultiValue.x:F4}, {v.MultiValue.y:F4}, {v.MultiValue.z:F4})",
                _                     => v.AsInt().ToString()     // 枚举等按 int 输出
            };
        }
    }

    /// <summary>NEventInvoke: 事件触发 —— 控制流，拉取 message 端口值并触发引擎事件</summary>
    public class EventInvokeExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NEventInvoke";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            string message = ctx.Engine.PullValue(ctx, node.Rid, "message").AsString();
            ctx.Engine.RaiseEventInvoked(message);
            return FsmExecutionResult.None();
        }
    }

    #endregion

    #region 信息 / 原语节点

    /// <summary>NGameTime: 游戏时间 —— 数据拉取，返回实时 Unity 时间值</summary>
    public class GameTimeExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NGameTime";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            return portName switch {
                "gameTime" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = Time.time }),
                "gameFrames" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "int", IntValue = Time.frameCount }),
                "gameTimeScale" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = Time.timeScale }),
                "deltaTime" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = Time.deltaTime }),
                _ => FsmExecutionResult.None()
            };
        }
    }

    /// <summary>基本类型节点（NInteger / NFloat / NString）—— 数据拉取，返回节点属性中的常数值</summary>
    public class PrimitivesExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) =>
            typeName is "NInteger" or "NFloat" or "NString";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            if (node.Properties.TryGetValue("value", out var val))
                return FsmExecutionResult.Value(val);

            return FsmExecutionResult.None();
        }
    }

    /// <summary>NVector: 向量 —— 输出完整 Vector4 MultiValue</summary>
    public class VectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float x = node.Properties.TryGetValue("valueX", out var vx) ? vx.AsFloat() : 0f;
            float y = node.Properties.TryGetValue("valueY", out var vy) ? vy.AsFloat() : 0f;
            float z = node.Properties.TryGetValue("valueZ", out var vz) ? vz.AsFloat() : 0f;
            float w = node.Properties.TryGetValue("valueW", out var vw) ? vw.AsFloat() : 0f;

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(new Vector4(x, y, z, w)));
        }
    }

    /// <summary>NColor: 颜色节点 —— 输出完整 Color MultiValue</summary>
    public class ColorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NColor";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            // 读取 "value" 属性（ToRuntime 已解析 Color JObject → MultiValue）
            if (node.Properties.TryGetValue("value", out var colorVal) && colorVal.TypeName == "Color")
                return FsmExecutionResult.Value(colorVal);

            return FsmExecutionResult.Value(FsmTypedValue.FromColor(Color.black));
        }
    }

    #endregion

    #region 参数节点

    /// <summary>ParameterNode: Get 模式读取参数，Set 模式通过 exec 端口写入参数</summary>
    public class ParameterNodeExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "ParameterNode";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            int paramRid = node.Properties.TryGetValue("parameterRid", out var r) ? r.AsInt() : 0;

            // Get 模式：数据拉取
            if (portName == "output") {
                if (paramRid != 0 && ctx.ExposedParameters.TryGetValue(paramRid, out var sharedVal))
                    return FsmExecutionResult.Value(sharedVal);
                return FsmExecutionResult.None();
            }

            // Set 模式：控制流执行（portName == null，沿 exec 链路推进）
            if (portName == null && node.Inputs.ContainsKey("input") && paramRid != 0) {
                FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "input");
                ctx.ExposedParameters[paramRid] = val;
                return FsmExecutionResult.None(); // 继续沿 exec 输出推进
            }

            return FsmExecutionResult.None();
        }
    }

    #endregion

    #region 实用节点

    /// <summary>NVectorSplit: 向量分裂 —— 将输入向量的 4 个分量拆分为独立输出</summary>
    public class VectorSplitExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NVectorSplit";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            // 拉取输入端口值（上游 NVector / NVectorCombine 返回 MultiValue）
            Vector4 vec = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsVector4();

            return portName switch {
                "outputX" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = vec.x }),
                "outputY" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = vec.y }),
                "outputZ" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = vec.z }),
                "outputW" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "float", FloatValue = vec.w }),
                _ => FsmExecutionResult.None()
            };
        }
    }

    /// <summary>NVectorCombine: 向量合并 —— 将 4 个浮点合并为 Vector4</summary>
    public class VectorCombineExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NVectorCombine";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float x = ctx.Engine.PullValue(ctx, node.Rid, "valueX").AsFloat();
            float y = ctx.Engine.PullValue(ctx, node.Rid, "valueY").AsFloat();
            float z = ctx.Engine.PullValue(ctx, node.Rid, "valueZ").AsFloat();
            float w = ctx.Engine.PullValue(ctx, node.Rid, "valueW").AsFloat();

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(new Vector4(x, y, z, w)));
        }
    }

    #endregion

    #region 转换节点

    /// <summary>NConvertColorToVector: 颜色 → 向量</summary>
    public class ConvertColorToVectorExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NConvertColorToVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            // 拉取上游 Color 值（MultiValue），根据 isNormalized 缩放
            Color col = ctx.Engine.PullValue(ctx, node.Rid, "col").AsColor();
            bool normalize = node.Properties.TryGetValue("isNormalized", out var n) && n.AsBool();

            Vector4 result = normalize
                ? new Vector4(col.r, col.g, col.b, col.a)
                : new Vector4(col.r * 255f, col.g * 255f, col.b * 255f, col.a * 255f);

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(result));
        }
    }

    /// <summary>NConvertVectorToColor: 向量 → 颜色</summary>
    public class ConvertVectorToColorExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NConvertVectorToColor";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            // 拉取上游 Vector 值（MultiValue），根据 isNormalized 缩放并钳制
            Vector4 vec = ctx.Engine.PullValue(ctx, node.Rid, "vec").AsVector4();
            bool normalize = node.Properties.TryGetValue("isNormalized", out var n) && n.AsBool();

            Color result = normalize
                ? new Color(vec.x, vec.y, vec.z, vec.w)
                : new Color(
                    Mathf.Clamp(vec.x / 255f, 0f, 1f),
                    Mathf.Clamp(vec.y / 255f, 0f, 1f),
                    Mathf.Clamp(vec.z / 255f, 0f, 1f),
                    Mathf.Clamp(vec.w / 255f, 0f, 1f));

            return FsmExecutionResult.Value(FsmTypedValue.FromColor(result));
        }
    }

    /// <summary>NConvertToBool: 任意类型 → 布尔</summary>
    public class ConvertToBoolExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NConvertToBool";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "any");
            bool result = val.TypeName switch {
                "bool" => val.AsBool(),
                "int" or "long" => val.AsInt() == 1,
                "float" or "double" => Mathf.Approximately(val.AsFloat(), 1f),
                "string" => val.AsString() is "true" or "True" or "TRUE" or "1",
                "Color" or "Vector3" or "Vector4" => Mathf.Approximately(val.AsFloat(), 1f),
                _ => false
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = result });
        }
    }

    /// <summary>NConvertToNumber: 任意类型 → 数字（int）</summary>
    public class ConvertToNumberExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NConvertToNumber";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "any");
            int result = val.TypeName switch {
                "int" or "long" => val.AsInt(),
                "float" or "double" => (int)val.AsFloat(),
                "bool" => val.AsBool() ? 1 : 0,
                "string" => float.TryParse(val.AsString(), out float f) ? (int)f : 0,
                "Color" or "Vector3" or "Vector4" => (int)val.AsFloat(),
                _ => 0
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "int", IntValue = result });
        }
    }

    /// <summary>NConvertToString: 任意类型 → 字符串</summary>
    public class ConvertToStringExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NConvertToString";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            int decimals = node.Properties.TryGetValue("decimalPlaces", out var dp) ? dp.AsInt() : 2;
            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "any");

            string result = val.TypeName switch {
                "float" or "double" => val.AsFloat().ToString($"F{decimals}"),
                "int" or "long" => val.AsInt().ToString(),
                "bool" => val.AsBool().ToString(),
                "string" => val.AsString(),
                "Color" or "Vector3" or "Vector4" => val.AsVector4().ToString($"F{decimals}"),
                _ => val.AsInt().ToString()
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "string", StringValue = result });
        }
    }
    #endregion

    #region 计算节点 —— Float

    /// <summary>NClampFloat: 浮点钳制</summary>
    public class ClampFloatExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NClampFloat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float val = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsFloat();
            bool useMin = node.Properties.TryGetValue("useMin", out var um) && um.AsBool();
            bool useMax = node.Properties.TryGetValue("useMax", out var ux) && ux.AsBool();

            if (useMin) val = Mathf.Max(val, ctx.Engine.PullValue(ctx, node.Rid, "min").AsFloat());
            if (useMax) val = Mathf.Min(val, ctx.Engine.PullValue(ctx, node.Rid, "max").AsFloat());

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = val });
        }
    }

    /// <summary>NLerpFloat: 浮点线性插值</summary>
    public class LerpFloatExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NLerpFloat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsFloat();
            float b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsFloat();
            float t = ctx.Engine.PullValue(ctx, node.Rid, "delta").AsFloat();

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = Mathf.Lerp(a, b, t) });
        }
    }

    /// <summary>NMapFloat: 浮点范围映射</summary>
    public class MapFloatExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NMapFloat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float val    = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsFloat();
            float fromMin = ctx.Engine.PullValue(ctx, node.Rid, "fromMin").AsFloat();
            float fromMax = ctx.Engine.PullValue(ctx, node.Rid, "fromMax").AsFloat();
            float toMin  = ctx.Engine.PullValue(ctx, node.Rid, "toMin").AsFloat();
            float toMax  = ctx.Engine.PullValue(ctx, node.Rid, "toMax").AsFloat();

            float range = fromMax - fromMin;
            float result = Mathf.Approximately(range, 0f)
                ? toMin
                : ((val - fromMin) * (toMax - toMin) + toMin * range) / range;

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NSingleMath: 浮点单值计算</summary>
    public class SingleMathExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NSingleMath";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsFloat();
            int method = node.Properties.TryGetValue("method", out var m) ? m.AsInt() : 0;

            // NodeSingleMathMethod: 0=Add_1,1=Sub_1,2=Power_2,3=Sqrt_2,4=Absolute,
            //                      5=Negate,6=Ceil,7=Floor,8=Round,9=Frac,10=Sign
            float result = method switch {
                0 => a + 1f,
                1 => a - 1f,
                2 => a * a,
                3 => Mathf.Sqrt(Mathf.Max(0f, a)),
                4 => Mathf.Abs(a),
                5 => -a,
                6 => Mathf.Ceil(a),
                7 => Mathf.Floor(a),
                8 => Mathf.Round(a),
                9 => a - Mathf.Floor(a),
                10 => a >= 0f ? 1f : -1f,
                _ => a
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NMultiMath: 浮点多值计算</summary>
    public class MultiMathExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NMultiMath";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsFloat();
            float b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsFloat();
            int method = node.Properties.TryGetValue("method", out var m) ? m.AsInt() : 0;

            // NodeMultiMathMethod: 0=Add,1=Subtract,2=Multiply,3=Divide,4=Modulus,5=Power,6=Sqrt,7=Log
            float result = method switch {
                0 => a + b,
                1 => a - b,
                2 => a * b,
                3 => Mathf.Approximately(b, 0f) ? 0f : a / b,
                4 => Mathf.Approximately(b, 0f) ? 0f : a % b,
                5 => Mathf.Pow(a, b),
                6 => Mathf.Approximately(b, 0f) ? 0f : Mathf.Pow(a, 1f / b),
                7 => (Mathf.Approximately(a, 0f) || Mathf.Approximately(a, 1f) || b <= 0f)
                     ? 0f : Mathf.Log(b, a),
                _ => 0f
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NTrigonometry: 三角函数</summary>
    public class TrigonometryExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NTrigonometry";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsFloat();
            int method = node.Properties.TryGetValue("method", out var m) ? m.AsInt() : 0;
            bool isDeg = node.Properties.TryGetValue("isDegrees", out var d) && d.AsBool();

            if (isDeg) a *= Mathf.Deg2Rad;

            // NodeTrigonometryMethod: 0=Sin, 1=Cos, 2=Tan, 3=Cot
            float result = method switch {
                0 => Mathf.Sin(a),
                1 => Mathf.Cos(a),
                2 => Mathf.Tan(a),
                3 => GetCot(a),
                _ => 0f
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }

        /// <summary>【计算余切值】</summary>
        /// <param name="a">【角度值】</param>
        /// <returns>【余切值】</returns>
        private float GetCot(float a) {
            float tan = Mathf.Tan(a);
            return Mathf.Approximately(tan, 0f) ? 0f : 1f / tan;
        }
    }

    /// <summary>NRadianToDegree: 弧度角度互转</summary>
    public class RadianToDegreeExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NRadianToDegree";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float val = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsFloat();
            bool isReverse = node.Properties.TryGetValue("isReverse", out var r) && r.AsBool();

            float result = isReverse
                ? val * Mathf.Deg2Rad
                : val * Mathf.Rad2Deg;

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NMathConstant: 数学常数</summary>
    public class MathConstantExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NMathConstant";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            int type = node.Properties.TryGetValue("type", out var t) ? t.AsInt() : 0;

            // NodeMathConstant: 0=PI, 1=E, 2=Sqrt_2
            float result = type switch {
                0 => Mathf.PI,
                1 => Mathf.Exp(1f),
                2 => Mathf.Sqrt(2f),
                _ => 0f
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    #endregion

    #region 计算节点 —— Vector

    /// <summary>NClampVector: 向量钳制（各通道独立）</summary>
    public class ClampVectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NClampVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputVector") return FsmExecutionResult.None();

            Vector4 vec = ctx.Engine.PullValue(ctx, node.Rid, "vector").AsVector4();
            bool useMin = node.Properties.TryGetValue("useMin", out var um) && um.AsBool();
            bool useMax = node.Properties.TryGetValue("useMax", out var ux) && ux.AsBool();

            if (useMin) {
                Vector4 mn = ctx.Engine.PullValue(ctx, node.Rid, "min").AsVector4();
                vec = new Vector4(Mathf.Max(vec.x, mn.x), Mathf.Max(vec.y, mn.y),
                                  Mathf.Max(vec.z, mn.z), Mathf.Max(vec.w, mn.w));
            }
            if (useMax) {
                Vector4 mx = ctx.Engine.PullValue(ctx, node.Rid, "max").AsVector4();
                vec = new Vector4(Mathf.Min(vec.x, mx.x), Mathf.Min(vec.y, mx.y),
                                  Mathf.Min(vec.z, mx.z), Mathf.Min(vec.w, mx.w));
            }

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(vec));
        }
    }

    /// <summary>NLerpVector: 向量线性插值（各通道独立）</summary>
    public class LerpVectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NLerpVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputVector") return FsmExecutionResult.None();

            Vector4 a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsVector4();
            Vector4 b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsVector4();
            float   t = ctx.Engine.PullValue(ctx, node.Rid, "delta").AsFloat();

            Vector4 result = new(
                Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.z, b.z, t), Mathf.Lerp(a.w, b.w, t));

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(result));
        }
    }

    /// <summary>NMapVector: 向量范围映射（各通道独立）</summary>
    public class MapVectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NMapVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputVector") return FsmExecutionResult.None();

            Vector4 val    = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsVector4();
            Vector4 fromMin = ctx.Engine.PullValue(ctx, node.Rid, "fromMin").AsVector4();
            Vector4 fromMax = ctx.Engine.PullValue(ctx, node.Rid, "fromMax").AsVector4();
            Vector4 toMin   = ctx.Engine.PullValue(ctx, node.Rid, "toMin").AsVector4();
            Vector4 toMax   = ctx.Engine.PullValue(ctx, node.Rid, "toMax").AsVector4();

            Vector4 result = new(
                MapComponent(val.x, fromMin.x, fromMax.x, toMin.x, toMax.x),
                MapComponent(val.y, fromMin.y, fromMax.y, toMin.y, toMax.y),
                MapComponent(val.z, fromMin.z, fromMax.z, toMin.z, toMax.z),
                MapComponent(val.w, fromMin.w, fromMax.w, toMin.w, toMax.w));

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(result));
        }

        private static float MapComponent(float v, float fMin, float fMax, float tMin, float tMax) {
            float range = fMax - fMin;
            return Mathf.Approximately(range, 0f)
                ? tMin
                : ((v - fMin) * (tMax - tMin) + tMin * range) / range;
        }
    }

    /// <summary>NVectorMath: 向量计算</summary>
    public class VectorMathExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NVectorMath";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputVector") return FsmExecutionResult.None();

            Vector4 a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsVector4();
            Vector4 b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsVector4();
            int method = node.Properties.TryGetValue("method", out var m) ? m.AsInt() : 0;

            // NodeVectorMathMethod: 0=Add,1=Subtract,2=Dot_Product,3=Cross_Product,
            //                      4=A_Normalize,5=A_Magnitude,6=Distance
            switch (method) {
                case 0: return FsmExecutionResult.Value(FsmTypedValue.FromVector4(a + b));
                case 1: return FsmExecutionResult.Value(FsmTypedValue.FromVector4(a - b));

                // 标量结果 → 存入 x 分量
                case 2: {
                    float dot = Vector4.Dot(a, b);
                    return FsmExecutionResult.Value(
                        new FsmTypedValue { TypeName = "float", FloatValue = dot });
                }
                case 3: {
                    Vector3 cross = Vector3.Cross((Vector3)a, (Vector3)b);
                    return FsmExecutionResult.Value(
                        FsmTypedValue.FromVector4(new Vector4(cross.x, cross.y, cross.z, 0f)));
                }
                case 4: {
                    float mag = a.magnitude;
                    Vector4 norm = mag > 1e-8f ? a / mag : Vector4.zero;
                    return FsmExecutionResult.Value(FsmTypedValue.FromVector4(norm));
                }
                case 5:
                    return FsmExecutionResult.Value(
                        new FsmTypedValue { TypeName = "float", FloatValue = a.magnitude });

                case 6:
                    return FsmExecutionResult.Value(
                        new FsmTypedValue { TypeName = "float", FloatValue = Vector4.Distance(a, b) });

                default: return FsmExecutionResult.None();
            }
        }
    }

    #endregion

    #region 噪声节点

    /// <summary>NNoiseFloat: 柏林噪声 —— 二维输入，单通道输出</summary>
    public class NoiseFloatExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NNoiseFloat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float x = ctx.Engine.PullValue(ctx, node.Rid, "positionX").AsFloat();
            float y = ctx.Engine.PullValue(ctx, node.Rid, "positionY").AsFloat();

            float result = Mathf.PerlinNoise(x, y);
            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NNoiseVector: 柏林噪声 —— 四通道独立采样</summary>
    public class NoiseVectorExecutor : IFsmNodeExecutor {
        public bool CanExecute(string typeName) => typeName == "NNoiseVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            Vector4 px = ctx.Engine.PullValue(ctx, node.Rid, "positionX").AsVector4();
            Vector4 py = ctx.Engine.PullValue(ctx, node.Rid, "positionY").AsVector4();

            Vector4 result = new(
                Mathf.PerlinNoise(px.x, py.x),
                Mathf.PerlinNoise(px.y, py.y),
                Mathf.PerlinNoise(px.z, py.z),
                Mathf.PerlinNoise(px.w, py.w));

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(result));
        }
    }

    #endregion

    #region 流程控制 / 转换补充

    /// <summary>NIfElse: 执行分流 —— 根据 isA 选择 ExecutesA 或 ExecutesB 端口继续执行</summary>
    public class IfElseExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NIfElse";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != null) return FsmExecutionResult.None();

            bool isA = ctx.Engine.PullValue(ctx, node.Rid, "isA").AsBool();
            return FsmExecutionResult.ExecPort(isA ? "ExecutesA" : "ExecutesB");
        }
    }

    /// <summary>NBranch: 数据分流 —— 根据 isA 选择 a 或 b 端口的数据输出</summary>
    public class BranchExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NBranch";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            bool isA = ctx.Engine.PullValue(ctx, node.Rid, "isA").AsBool();
            string srcPort = isA ? "a" : "b";
            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, srcPort);

            // 保留上游类型透传
            return FsmExecutionResult.Value(val);
        }
    }

    /// <summary>NConvertToVector: 任意类型 → Vector4</summary>
    public class ConvertToVectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NConvertToVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "any");
            Vector4 v = val.TypeName switch {
                "int" or "long"    => new Vector4(val.AsInt(), 0, 0, 0),
                "float" or "double"=> new Vector4(val.AsFloat(), 0, 0, 0),
                "bool"             => new Vector4(val.AsBool() ? 1f : 0f, 0, 0, 0),
                "string"           => float.TryParse(val.AsString(), out float f)
                                       ? new Vector4(f, 0, 0, 0) : Vector4.zero,
                "Vector4" or "Color" => val.AsVector4(),
                "Vector3"          => val.AsVector4(), // w=0 来自 AsVector4 默认
                _                  => new Vector4((float)val.AsFloat(), 0, 0, 0)
            };

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(v));
        }
    }

    /// <summary>NConvertToColor: 任意类型 → Color</summary>
    public class ConvertToColorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NConvertToColor";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "result") return FsmExecutionResult.None();

            FsmTypedValue val = ctx.Engine.PullValue(ctx, node.Rid, "any");
            bool normalize = node.Properties.TryGetValue("isNormalized", out var n) && n.AsBool();

            Color c = val.TypeName switch {
                "int" or "long"    => new Color(val.AsInt(), 0, 0, 0),
                "float" or "double"=> new Color(val.AsFloat(), 0, 0, 0),
                "bool"             => val.AsBool() ? Color.white : Color.black,
                "string"           => float.TryParse(val.AsString(), out float f)
                                       ? new Color(f, 0, 0, 0) : Color.black,
                "Vector4"          => FromVector(val.AsVector4(), normalize),
                "Vector3"          => FromVector(val.AsVector4(), normalize),
                "Color"            => val.AsColor(), // 已是 Color，直接返回
                _                  => new Color((float)val.AsFloat(), 0, 0, 0)
            };

            return FsmExecutionResult.Value(FsmTypedValue.FromColor(c));
        }

        private static Color FromVector(Vector4 v, bool normalize) {
            if (normalize)
                return new Color(v.x, v.y, v.z, v.w);
            return new Color(
                Mathf.Clamp(v.x / 255f, 0f, 1f),
                Mathf.Clamp(v.y / 255f, 0f, 1f),
                Mathf.Clamp(v.z / 255f, 0f, 1f),
                Mathf.Clamp(v.w / 255f, 0f, 1f));
        }
    }

    #endregion

    #region 实用节点

    /// <summary>NRandomFloat: 随机浮点</summary>
    public class RandomFloatExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NRandomFloat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            float min = ctx.Engine.PullValue(ctx, node.Rid, "min").AsFloat();
            float max = ctx.Engine.PullValue(ctx, node.Rid, "max").AsFloat();
            float result = UnityEngine.Random.Range(min, max);

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "float", FloatValue = result });
        }
    }

    /// <summary>NRandomVector: 随机向量</summary>
    public class RandomVectorExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NRandomVector";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            Vector4 min = ctx.Engine.PullValue(ctx, node.Rid, "min").AsVector4();
            Vector4 max = ctx.Engine.PullValue(ctx, node.Rid, "max").AsVector4();
            Vector4 result = new(
                UnityEngine.Random.Range(min.x, max.x),
                UnityEngine.Random.Range(min.y, max.y),
                UnityEngine.Random.Range(min.z, max.z),
                UnityEngine.Random.Range(min.w, max.w));

            return FsmExecutionResult.Value(FsmTypedValue.FromVector4(result));
        }
    }

    /// <summary>NColorFlip: 颜色通道翻转</summary>
    public class ColorFlipExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NColorFlip";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            Color c = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsColor();
            bool flipR = ctx.Engine.PullValue(ctx, node.Rid, "flipR").AsBool();
            bool flipG = ctx.Engine.PullValue(ctx, node.Rid, "flipG").AsBool();
            bool flipB = ctx.Engine.PullValue(ctx, node.Rid, "flipB").AsBool();
            bool flipA = ctx.Engine.PullValue(ctx, node.Rid, "flipA").AsBool();

            Color result = new(
                flipR ? 1f - c.r : c.r,
                flipG ? 1f - c.g : c.g,
                flipB ? 1f - c.b : c.b,
                flipA ? 1f - c.a : c.a);

            return FsmExecutionResult.Value(FsmTypedValue.FromColor(result));
        }
    }

    /// <summary>NColorWrap: 颜色通道置换</summary>
    public class ColorWrapExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NColorWrap";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            Color src = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsColor();
            int chR = node.Properties.TryGetValue("r", out var pr) ? pr.AsInt() : 0;
            int chG = node.Properties.TryGetValue("g", out var pg) ? pg.AsInt() : 1;
            int chB = node.Properties.TryGetValue("b", out var pb) ? pb.AsInt() : 2;
            int chA = node.Properties.TryGetValue("a", out var pa) ? pa.AsInt() : 3;

            // NodeColorChannel: 0=Red,1=Green,2=Blue,3=Alpha
            float Channel(int ch) => ch switch {
                0 => src.r, 1 => src.g, 2 => src.b, 3 => src.a, _ => 0f
            };

            Color result = new(Channel(chR), Channel(chG), Channel(chB), Channel(chA));
            return FsmExecutionResult.Value(FsmTypedValue.FromColor(result));
        }
    }

    /// <summary>NStringConcat: 字符串拼接</summary>
    public class StringConcatExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NStringConcat";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            string a = ctx.Engine.PullValue(ctx, node.Rid, "a").AsString();
            string b = ctx.Engine.PullValue(ctx, node.Rid, "b").AsString();
            string sep = node.Properties.TryGetValue("separator", out var s) ? s.AsString() : "";

            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "string", StringValue = a + sep + b });
        }
    }

    /// <summary>NStringSplit: 字符串分割提取 —— 按分隔符切分，取指定索引的子串</summary>
    public class StringSplitExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NStringSplit";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            string input = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsString();
            int index = ctx.Engine.PullValue(ctx, node.Rid, "resultIndex").AsInt();
            string sep = node.Properties.TryGetValue("separator", out var s) ? s.AsString() : "";

            if (string.IsNullOrEmpty(sep)) {
                return FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "string", StringValue = index == 0 ? input : "" });
            }

            string[] parts = input.Split(new[] { sep }, System.StringSplitOptions.None);
            string result = index >= 0 && index < parts.Length ? parts[index] : "";
            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "string", StringValue = result });
        }
    }

    /// <summary>NToJson: 将 6 种类型值序列化为紧凑 JSON 字符串</summary>
    public class ToJsonExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NToJson";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            // 读取键名（属性，空键名 = 跳过该字段）
            string intKey    = node.Properties.TryGetValue("intKey",    out var ik) ? ik.AsString() : "";
            string floatKey  = node.Properties.TryGetValue("floatKey",  out var fk) ? fk.AsString() : "";
            string boolKey   = node.Properties.TryGetValue("boolKey",   out var bk) ? bk.AsString() : "";
            string stringKey = node.Properties.TryGetValue("stringKey", out var sk) ? sk.AsString() : "";
            string vectorKey = node.Properties.TryGetValue("vectorKey", out var vk) ? vk.AsString() : "";
            string colorKey  = node.Properties.TryGetValue("colorKey",  out var ck) ? ck.AsString() : "";

            // 拉取各端口值
            int    intVal    = ctx.Engine.PullValue(ctx, node.Rid, "intValue").AsInt();
            float  floatVal  = ctx.Engine.PullValue(ctx, node.Rid, "floatValue").AsFloat();
            bool   boolVal   = ctx.Engine.PullValue(ctx, node.Rid, "boolValue").AsBool();
            string stringVal = ctx.Engine.PullValue(ctx, node.Rid, "stringValue").AsString();
            Vector4 vecVal   = ctx.Engine.PullValue(ctx, node.Rid, "vectorValue").AsVector4();
            Color   colVal   = ctx.Engine.PullValue(ctx, node.Rid, "colorValue").AsColor();

            // 构建字典（仅非空键名的字段）
            var dict = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(intKey))    dict[intKey]    = intVal;
            if (!string.IsNullOrEmpty(floatKey))  dict[floatKey]  = floatVal;
            if (!string.IsNullOrEmpty(boolKey))   dict[boolKey]   = boolVal;
            if (!string.IsNullOrEmpty(stringKey)) dict[stringKey] = stringVal;
            if (!string.IsNullOrEmpty(vectorKey)) dict[vectorKey] = new { x = vecVal.x, y = vecVal.y, z = vecVal.z, w = vecVal.w };
            if (!string.IsNullOrEmpty(colorKey))  dict[colorKey]  = new { r = colVal.r, g = colVal.g, b = colVal.b, a = colVal.a };

            string json = JsonConvert.SerializeObject(dict, Formatting.None);
            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "string", StringValue = json });
        }
    }

    /// <summary>NFromJson: 从 JSON 字符串反序列化提取 6 种类型值</summary>
    public class FromJsonExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NFromJson";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            // 读取键名
            string intKey    = node.Properties.TryGetValue("intKey",    out var ik) ? ik.AsString() : "";
            string floatKey  = node.Properties.TryGetValue("floatKey",  out var fk) ? fk.AsString() : "";
            string boolKey   = node.Properties.TryGetValue("boolKey",   out var bk) ? bk.AsString() : "";
            string stringKey = node.Properties.TryGetValue("stringKey", out var sk) ? sk.AsString() : "";
            string vectorKey = node.Properties.TryGetValue("vectorKey", out var vk) ? vk.AsString() : "";
            string colorKey  = node.Properties.TryGetValue("colorKey",  out var ck) ? ck.AsString() : "";

            // 按帧缓存解析 JSON（同帧多端口 PullValue 只解析一次）
            JObject root = null;
            int frame = Time.frameCount;
            if (ctx.FromJsonCache.TryGetValue(node.Rid, out var cached) && cached.frame == frame) {
                root = cached.root;
            } else {
                string json = ctx.Engine.PullValue(ctx, node.Rid, "json").AsString();
                if (!string.IsNullOrEmpty(json)) {
                    try { root = JObject.Parse(json); }
                    catch { root = null; }
                }
                ctx.FromJsonCache[node.Rid] = (frame, root);
            }

            if (root == null)
                return DefaultForPort(portName);

            return portName switch {
                "intValue"    => ExtractInt(root, intKey),
                "floatValue"  => ExtractFloat(root, floatKey),
                "boolValue"   => ExtractBool(root, boolKey),
                "stringValue" => ExtractString(root, stringKey),
                "vectorValue" => ExtractVector(root, vectorKey),
                "colorValue"  => ExtractColor(root, colorKey),
                _             => FsmExecutionResult.None(),
            };
        }

        private static FsmExecutionResult ExtractInt(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] == null) return IntDefault();
            return Value(new FsmTypedValue { TypeName = "int", IntValue = root[key].Value<int>() });
        }
        private static FsmExecutionResult ExtractFloat(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] == null) return FloatDefault();
            return Value(new FsmTypedValue { TypeName = "float", FloatValue = root[key].Value<float>() });
        }
        private static FsmExecutionResult ExtractBool(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] == null) return BoolDefault();
            return Value(new FsmTypedValue { TypeName = "bool", BoolValue = root[key].Value<bool>() });
        }
        private static FsmExecutionResult ExtractString(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] == null) return StringDefault();
            return Value(new FsmTypedValue { TypeName = "string", StringValue = root[key].Value<string>() });
        }
        private static FsmExecutionResult ExtractVector(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] is not JObject jo) return VectorDefault();
            Vector4 v = new(
                jo["x"]?.Value<float>() ?? 0f,
                jo["y"]?.Value<float>() ?? 0f,
                jo["z"]?.Value<float>() ?? 0f,
                jo["w"]?.Value<float>() ?? 0f);
            return Value(FsmTypedValue.FromVector4(v));
        }
        private static FsmExecutionResult ExtractColor(JObject root, string key) {
            if (string.IsNullOrEmpty(key) || root[key] is not JObject jo) return ColorDefault();
            Color c = new(
                jo["r"]?.Value<float>() ?? 0f,
                jo["g"]?.Value<float>() ?? 0f,
                jo["b"]?.Value<float>() ?? 0f,
                jo["a"]?.Value<float>() ?? 0f);
            return Value(FsmTypedValue.FromColor(c));
        }

        private static FsmExecutionResult DefaultForPort(string portName) => portName switch {
            "intValue"    => IntDefault(),
            "floatValue"  => FloatDefault(),
            "boolValue"   => BoolDefault(),
            "stringValue" => StringDefault(),
            "vectorValue" => VectorDefault(),
            "colorValue"  => ColorDefault(),
            _             => FsmExecutionResult.None(),
        };

        private static FsmExecutionResult IntDefault()    => Value(new FsmTypedValue { TypeName = "int",    IntValue    = 0 });
        private static FsmExecutionResult FloatDefault()  => Value(new FsmTypedValue { TypeName = "float",  FloatValue  = 0f });
        private static FsmExecutionResult BoolDefault()   => Value(new FsmTypedValue { TypeName = "bool",   BoolValue   = false });
        private static FsmExecutionResult StringDefault() => Value(new FsmTypedValue { TypeName = "string", StringValue = "" });
        private static FsmExecutionResult VectorDefault() => Value(FsmTypedValue.FromVector4(Vector4.zero));
        private static FsmExecutionResult ColorDefault()  => Value(FsmTypedValue.FromColor(Color.black));
        private static FsmExecutionResult Value(FsmTypedValue tv) =>
            new() { Type = FsmExecutionResult.ResultType.ValueReady, TypedValue = tv };
    }

    /// <summary>NRegularEx: 正则表达式</summary>
    public class RegularExExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NRegularEx";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            string input = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsString();
            string pattern = ctx.Engine.PullValue(ctx, node.Rid, "pattern").AsString();
            int method = node.Properties.TryGetValue("method", out var m) ? m.AsInt() : 0;

            // NodeRegularExpressionMethod: 0=Remove, 1=Replace
            string result;
            if (method == 0) {
                result = string.IsNullOrEmpty(pattern) ? input
                    : Regex.Replace(input, pattern, "");
            }
            else {
                string replace = ctx.Engine.PullValue(ctx, node.Rid, "replace").AsString();
                result = string.IsNullOrEmpty(pattern) ? input
                    : Regex.Replace(input, pattern, replace);
            }

            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "string", StringValue = result });
        }
    }

    /// <summary>NRegularExMatch: 正则匹配判断</summary>
    public class RegularExMatchExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NRegularExMatch";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            string input = ctx.Engine.PullValue(ctx, node.Rid, "inputValue").AsString();
            string pattern = ctx.Engine.PullValue(ctx, node.Rid, "pattern").AsString();
            bool result = !string.IsNullOrEmpty(pattern) && Regex.IsMatch(input, pattern);

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = result });
        }
    }

    /// <summary>NCurrentState: 获取当前状态名</summary>
    public class CurrentStateExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NCurrentState";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "currentState") return FsmExecutionResult.None();

            return FsmExecutionResult.Value(
                new FsmTypedValue { TypeName = "string", StringValue = ctx.CurrentStateName ?? "" });
        }
    }

    /// <summary>NIsEqualTo: 判断两个对象是否相等</summary>
    public class IsEqualToExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NIsEqualTo";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            if (portName != "outputValue") return FsmExecutionResult.None();

            FsmTypedValue a = ctx.Engine.PullValue(ctx, node.Rid, "a");
            FsmTypedValue b = ctx.Engine.PullValue(ctx, node.Rid, "b");

            bool result = a.TypeName == b.TypeName && a.TypeName switch {
                "bool"   => a.AsBool() == b.AsBool(),
                "int" or "long" => a.AsInt() == b.AsInt(),
                "float" or "double" => Mathf.Approximately(a.AsFloat(), b.AsFloat()),
                "string" => a.AsString() == b.AsString(),
                "Color" or "Vector4" or "Vector3" => a.AsVector4() == b.AsVector4(),
                _ => false
            };

            return FsmExecutionResult.Value(new FsmTypedValue { TypeName = "bool", BoolValue = result });
        }
    }

    /// <summary>NLocalTransform: 传递实时 Transform 信息</summary>
    public class LocalTransformExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NLocalTransform";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            Transform t = ctx.Owner?.transform;
            if (t == null) return FsmExecutionResult.None();

            bool isRelative = node.Properties.TryGetValue("isRelative", out var ir) && ir.AsBool();

            // 获取旋转角度
            FsmExecutionResult GetRotationResult() {
                Vector3 euler = isRelative ? t.localEulerAngles : t.eulerAngles;
                return FsmExecutionResult.Value(FsmTypedValue.FromVector4(new Vector4(euler.x, euler.y, euler.z, 0f)));
            }
            // 获取缩放比例
            FsmExecutionResult GetScaleResult() {
                Vector3 scale = isRelative ? t.localScale : t.lossyScale;
                return FsmExecutionResult.Value(FsmTypedValue.FromVector4(new Vector4(scale.x, scale.y, scale.z, 0f)));
            }

            return portName switch {
                "tPosition" => FsmExecutionResult.Value(
                    FsmTypedValue.FromVector4(isRelative ? (Vector4)t.localPosition : (Vector4)t.position)),
                "tRotation" => GetRotationResult(),
                "tScale" => GetScaleResult(),
                _ => FsmExecutionResult.None()
            };
        }
    }

    /// <summary>NLocalGameObject: 传递实时 GameObject 信息</summary>
    public class LocalGameObjectExecutor : IFsmNodeExecutor
    {
        public bool CanExecute(string typeName) => typeName == "NLocalGameObject";

        public FsmExecutionResult Execute(FsmContext ctx, FsmNode node, string portName) {
            GameObject go = ctx.Owner?.gameObject;
            if (go == null) return FsmExecutionResult.None();

            return portName switch {
                "goName" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "string", StringValue = go.name }),
                "goTag" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "string", StringValue = go.tag }),
                "goLayer" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "int", IntValue = go.layer }),
                "goIsStatic" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "bool", BoolValue = go.isStatic }),
                "goSceneName" => FsmExecutionResult.Value(
                    new FsmTypedValue { TypeName = "string", StringValue = go.scene.name }),
                _ => FsmExecutionResult.None()
            };
        }
    }

    #endregion
}