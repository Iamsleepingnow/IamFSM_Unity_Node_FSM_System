using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo3_Chaser : MonoBehaviour
    {
        public FsmObject fsmObject;
        public Text txt_CurrentStateLog;
        public GameObject playerObject;
        public float chaseRadius = 5f;
        public float attackRadius = 1.5f;

        private List<MeshRenderer> meshs;

        private Color idleColor = new(0.68f, 0.39f, 0.78f);
        private Color chasingColor = new(0.24f, 0.64f, 0.84f);
        private Color attackingColor = new(0.88f, 0.26f, 0.24f);

        public Bounds moveBounds = new(new Vector3(0, 0, 0), new Vector3(20f, 20f, 0f)); // 世界空间 AABB
        private bool _dragging;
        private Vector2 _startMouse;   // 开始拖拽时的鼠标屏幕坐标
        private Vector3 _startPos;     // 开始拖拽时的物体世界坐标

        void Start() {
            fsmObject.fsmEnterState.AddListener(st => {
                txt_CurrentStateLog.text = $"当前状态：<color=#AF65C7><b>{st}</b></color>";
                meshs = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
                switch (st) {
                    case "Idle":
                        meshs.ForEach(m => m.material.color = idleColor);
                        break;
                    case "Chase":
                        meshs.ForEach(m => m.material.color = chasingColor);
                        break;
                    case "Attack":
                        meshs.ForEach(m => m.material.color = attackingColor);
                        break;
                }
                Debug.Log($"进入状态: {st}");
            });
            fsmObject.fsmEvent.AddListener(ev => {
               Debug.Log($"事件触发: {ev}");
            });
        }

        void Update() {
            if (playerObject == null) return;
            fsmObject.SetFloat("SphereDetectRadius", chaseRadius);
            fsmObject.SetFloat("AttackDistance", attackRadius);
            if (fsmObject.CurrentStateName == "Idle" || fsmObject.CurrentStateName == "Chase") {
                fsmObject.SetVector4("PlayerPosition", playerObject.transform.position);
            }
            HandleDrag();
        }

        void OnDrawGizmos() {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, chaseRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }

        void HandleDrag()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                _startMouse = Input.mousePosition;
                _startPos = transform.position;
            }
            if (Input.GetMouseButtonUp(0))
                _dragging = false;
            if (!_dragging) return;

            Vector3 startWorld = MouseToXYPlane(_startMouse, _startPos.z);
            Vector3 curWorld   = MouseToXYPlane(Input.mousePosition, _startPos.z);
            Vector3 target = _startPos + (curWorld - startWorld);

            target.x = Mathf.Clamp(target.x, moveBounds.min.x, moveBounds.max.x); // AABB 约束
            target.y = Mathf.Clamp(target.y, moveBounds.min.y, moveBounds.max.y);
            // transform.position = target; 
            transform.position = Vector3.Lerp(transform.position, target, 0.03f); // z 不变
        }

        Vector3 MouseToXYPlane(Vector3 screenPos, float planeZ)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.forward, new Vector3(0, 0, planeZ)); // XY 平面，z = planeZ
            if (plane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return transform.position; // 平行兜底
        }
    }
}
