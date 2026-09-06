using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo1_Helloworld : MonoBehaviour
    {
        public FsmObject fsmObject;
        public Text txt_CurrentStateLog;
        
        private List<MeshRenderer> meshs;

        private Color idleColor = new(0.68f, 0.39f, 0.78f);
        private Color triggeredColor = new(0.84f, 0.74f, 0.24f);

        void Start() {
            fsmObject.fsmEnterState.AddListener(st => {
                txt_CurrentStateLog.text = $"当前状态：<color=#AF65C7><b>{st}</b></color>";
                meshs = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
                Debug.Log($"进入状态: {st}");
                switch (st) {
                    case "Idle":
                        meshs.ForEach(m => m.material.color = idleColor);
                        break;
                    case "Triggered":
                        meshs.ForEach(m => m.material.color = triggeredColor);
                        break;
                }
            });
            fsmObject.fsmExitState.AddListener(st => {
                Debug.Log($"退出状态: {st}");
            });
            fsmObject.fsmEvent.AddListener(ev => {
               Debug.Log($"事件触发: {ev}");
            });
        }
    }
}
