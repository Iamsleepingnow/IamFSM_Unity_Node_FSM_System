using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo2_Properties : MonoBehaviour
    {
        public FsmObject fsmObject;
        public Text txt_CurrentCountLog;
        public Slider slider_ReloadProgress;

        private List<MeshRenderer> meshs;
        private int clickCount = 0; // 点击计数
        private float reloadProgress = 0f; // 重载进度

        private Color idleColor = new(0.68f, 0.39f, 0.78f);
        private Color reloadColor = new(0.84f, 0.74f, 0.24f);

        void Start() {
            fsmObject.fsmEnterState.AddListener(st => {
                meshs = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
            });
            fsmObject.fsmEvent.AddListener(eventName => {
                if (eventName == "Click" || eventName == "Reload") {
                    fsmObject?.TryGetInt("Count", out clickCount);
                    txt_CurrentCountLog.text = $"点击计数：<color=#AF65C7><b>{clickCount}/10</b></color>";
                }
            });
        }

        void Update() {
            if (fsmObject?.TryGetFloat("ReloadProgress", out reloadProgress) ?? false) {
                slider_ReloadProgress.value = reloadProgress;
            }
        }
    }
}
