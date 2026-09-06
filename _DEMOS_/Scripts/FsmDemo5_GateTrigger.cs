using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo5_GateTrigger : MonoBehaviour
    {
        public FsmObject fsmObject;
        public TextMesh txt_TriggerCounter;

        private int triggerCounter = 0;

        void Start() {
            fsmObject.fsmEvent.AddListener(ev => {
                if (ev == "Triggered") {
                    triggerCounter++;
                    txt_TriggerCounter.text = $"进门次数：{triggerCounter}";
                }
            });
        }
    }
}
