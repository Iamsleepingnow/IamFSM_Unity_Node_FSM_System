using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【远程物理检测代理】—— 装载在任意碰撞箱物体上，收集该物体本机的碰撞/触发事件，
/// 供 FSM 节点（NCollisionProxyDetect / NTriggerProxyDetect）通过 proxyId 远程读取。
/// 设计要点：
///   - 不依赖任何 FsmObjectBase，可独立存在、被多个 FSM 复用；
///   - 通过全局静态注册表 + proxyId 松耦合解析；
///   - 缓冲与 FsmContext 同构，仅在 LateUpdate 清理更早帧，每帧只保留当前帧事件。
/// 前置约束（Unity 物理硬要求）：
///   - 本物体必须有 Collider/Collider2D；若作碰撞（非触发）处理，两物体至少一方有 Rigidbody；
///   - 触发（isTrigger）场景无需 Rigidbody。
/// </summary>
public class FsmCollisionProxy : MonoBehaviour
{
    /// <summary>唯一标识，供 FSM 节点 proxyId 端口匹配</summary>
    [Tooltip("唯一标识，供 FSM 节点 proxyId 端口匹配")]
    public string proxyId;

    #region 事件缓冲（与 FsmContext 同构，frame 为该事件发生时的 Time.frameCount）

    public readonly List<(Collision col, int frame)> CollisionEnters  = new();
    public readonly List<(Collision col, int frame)> CollisionStays   = new();
    public readonly List<(Collision col, int frame)> CollisionExits   = new();
    public readonly List<(Collision2D col, int frame)> CollisionEnters2D = new();
    public readonly List<(Collision2D col, int frame)> CollisionStays2D  = new();
    public readonly List<(Collision2D col, int frame)> CollisionExits2D  = new();
    public readonly List<(Collider other, int frame)> TriggerEnters     = new();
    public readonly List<(Collider other, int frame)> TriggerStays      = new();
    public readonly List<(Collider other, int frame)> TriggerExits      = new();
    public readonly List<(Collider2D other, int frame)> TriggerEnters2D = new();
    public readonly List<(Collider2D other, int frame)> TriggerStays2D  = new();
    public readonly List<(Collider2D other, int frame)> TriggerExits2D  = new();

    #endregion

    // ==================== 全局注册表 ====================

    /// <summary>全局静态注册表：proxyId → FsmCollisionProxy 实例。（注意：需在 OnDestroy 严格注销，防泄漏）</summary>
    static readonly Dictionary<string, FsmCollisionProxy> _registry = new();

    /// <summary>按 proxyId 查找代理实例；找不到返回 null</summary>
    public static FsmCollisionProxy Find(string id) {
        if (string.IsNullOrEmpty(id)) return null;
        _registry.TryGetValue(id, out var proxy);
        return proxy;
    }

    void Awake() {
        if (string.IsNullOrEmpty(proxyId)) {
            Debug.LogWarning($"[FsmCollisionProxy] {name} 未设置 proxyId，无法被 FSM 节点引用。");
            return;
        }
        if (_registry.ContainsKey(proxyId)) {
            Debug.LogError($"[FsmCollisionProxy] proxyId=[{proxyId}] 冲突：{_registry[proxyId].name} 与 {name}。本实例注册失败，请保证 proxyId 全局唯一。");
            return;
        }
        _registry[proxyId] = this;
    }

    void OnDestroy() {
        if (!string.IsNullOrEmpty(proxyId) && _registry.TryGetValue(proxyId, out var self) && self == this)
            _registry.Remove(proxyId);
    }

    // ==================== 缓冲清理（每帧保留当前帧事件） ====================

    void LateUpdate() {
        int frame = Time.frameCount;
        CollisionEnters.RemoveAll(x => x.frame < frame);
        CollisionStays.RemoveAll(x => x.frame < frame);
        CollisionExits.RemoveAll(x => x.frame < frame);
        CollisionEnters2D.RemoveAll(x => x.frame < frame);
        CollisionStays2D.RemoveAll(x => x.frame < frame);
        CollisionExits2D.RemoveAll(x => x.frame < frame);
        TriggerEnters.RemoveAll(x => x.frame < frame);
        TriggerStays.RemoveAll(x => x.frame < frame);
        TriggerExits.RemoveAll(x => x.frame < frame);
        TriggerEnters2D.RemoveAll(x => x.frame < frame);
        TriggerStays2D.RemoveAll(x => x.frame < frame);
        TriggerExits2D.RemoveAll(x => x.frame < frame);
    }

    // ==================== 物理事件收集（Unity 消息回调 → 缓冲） ====================

    void OnCollisionEnter(Collision other)        => CollisionEnters.Add((other, Time.frameCount));
    void OnCollisionStay(Collision other)         => CollisionStays.Add((other, Time.frameCount));
    void OnCollisionExit(Collision other)         => CollisionExits.Add((other, Time.frameCount));
    void OnCollisionEnter2D(Collision2D other)    => CollisionEnters2D.Add((other, Time.frameCount));
    void OnCollisionStay2D(Collision2D other)     => CollisionStays2D.Add((other, Time.frameCount));
    void OnCollisionExit2D(Collision2D other)     => CollisionExits2D.Add((other, Time.frameCount));
    void OnTriggerEnter(Collider other)           => TriggerEnters.Add((other, Time.frameCount));
    void OnTriggerStay(Collider other)            => TriggerStays.Add((other, Time.frameCount));
    void OnTriggerExit(Collider other)            => TriggerExits.Add((other, Time.frameCount));
    void OnTriggerEnter2D(Collider2D other)       => TriggerEnters2D.Add((other, Time.frameCount));
    void OnTriggerStay2D(Collider2D other)        => TriggerStays2D.Add((other, Time.frameCount));
    void OnTriggerExit2D(Collider2D other)        => TriggerExits2D.Add((other, Time.frameCount));
}