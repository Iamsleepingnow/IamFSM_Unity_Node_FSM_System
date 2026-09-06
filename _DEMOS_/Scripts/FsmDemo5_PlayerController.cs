using UnityEngine;

namespace FSMGraph.Demos
{
    public class FsmDemo5_PlayerController : MonoBehaviour
    {
        public CharacterController cc;
        public string horizontalAxis = "Horizontal"; // 左右
        public string verticalAxis = "Vertical"; // 前后

        private float h, v;

        private void Update() {
            h = Input.GetAxis(horizontalAxis);
            v = Input.GetAxis(verticalAxis);
            cc.Move(new Vector3(h, 0, v) * 5f * Time.deltaTime);
        }
    }
}
