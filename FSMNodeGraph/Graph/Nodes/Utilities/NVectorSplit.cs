using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/向量分裂 Vector Split")]
    public class NVectorSplit : BaseNode
    {
        [Input(name = "Value"), ShowAsDrawer] public Vector4 inputValue = Vector4.zero;

        [Output(name = "X")] public float outputX = 0f;
        [Output(name = "Y")] public float outputY = 0f;
        [Output(name = "Z")] public float outputZ = 0f;
        [Output(name = "W")] public float outputW = 0f;

        public override string name => "向量分裂 Vector Split";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = Vector4.zero;
            outputX = 0f;
            outputY = 0f;
            outputZ = 0f;
            outputW = 0f;
        }
    }
}
