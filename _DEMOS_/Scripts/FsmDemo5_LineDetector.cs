using UnityEngine;
using FSMGraph;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FSMGraph.Demos
{
    public class FsmDemo5_LineDetector : MonoBehaviour
    {
        public FsmObject fsmObject;
        public Transform rayOrigin;
        public Light lightObject; // 光源对象

        [SerializeField] public Vector4 rayDirection;
        [SerializeField] public float rayDistance;

        private bool isLightOn = false; // 光源是否开启
        
        void Start() {
            fsmObject.fsmInitialized.AddListener(() => {
                fsmObject.SetVector4("RayOrigin", new(rayOrigin.position.x, rayOrigin.position.y, rayOrigin.position.z, 0f));
                fsmObject.SetVector4("RayDirection", rayDirection);
                fsmObject.SetFloat("RayDistance", rayDistance);
            });
            fsmObject.fsmEvent.AddListener(ev => {
                if (ev == "RayDetect") {
                    isLightOn = !isLightOn;
                    lightObject.gameObject.SetActive(isLightOn);
                }
            });
            fsmObject.Play();
        }
    }
}
