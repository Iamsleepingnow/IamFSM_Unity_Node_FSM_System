using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/向量合并 Vector Combine")]
    public class NVectorCombine : BaseNode
    {
        [Input(name = "X"), ShowAsDrawer] public float valueX = 0f;
        [Input(name = "Y"), ShowAsDrawer] public float valueY = 0f;
        [Input(name = "Z"), ShowAsDrawer] public float valueZ = 0f;
        [Input(name = "W"), ShowAsDrawer] public float valueW = 0f;

        [Output(name = "Value")] public Vector4 outputValue = Vector4.zero;

        public override string name => "向量合并 Vector Combine";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            valueX = 0f;
            valueY = 0f;
            valueZ = 0f;
            valueW = 0f;
            outputValue = Vector4.zero;
        }
    }
}
