using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo4_ForEach : MonoBehaviour
    {
        public FsmObject fsmObject;
        public Text txt_CurrentStateLog;

        private List<MeshRenderer> meshs;

        private Color idleColor = new(0.68f, 0.39f, 0.78f);
        private Color foreachColor = new(0.84f, 0.74f, 0.24f);

        public GameObject ballPrefab;
        public Vector3 instantiatePosition;
        public Vector3 instantiateBorderSize;
        private List<GameObject> balls = new();

        void Start() {
            fsmObject.fsmEnterState.AddListener(st => {
                txt_CurrentStateLog.text = $"当前状态：<color=#AF65C7><b>{st}</b></color>";
                meshs = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
                switch (st) {
                    case "Idle":
                        meshs.ForEach(m => m.material.color = idleColor);
                        break;
                    case "ForEach":
                        meshs.ForEach(m => m.material.color = foreachColor);
                        break;
                }
            });
            fsmObject.fsmEvent.AddListener(ev => {
                if (ev == "ForEachTick") {
                    InstanciateBall();
                }
                else if (ev == "Completed") {
                    balls.ForEach(b => b.GetComponent<MeshRenderer>().material.color = idleColor);
                    balls.Clear();
                }
            });
        }

        private void InstanciateBall() {
            GameObject ball = Instantiate(ballPrefab);
            ball.transform.position = new Vector3(
                Random.Range(-instantiateBorderSize.x, instantiateBorderSize.x),
                Random.Range(-instantiateBorderSize.y, instantiateBorderSize.y),
                Random.Range(-instantiateBorderSize.z, instantiateBorderSize.z)
            ) + instantiatePosition;
            if (ball.GetComponent<MeshRenderer>() != null) {
                ball.GetComponent<MeshRenderer>().material.color = foreachColor;
            }
            balls.Add(ball);
        }
    }
}
