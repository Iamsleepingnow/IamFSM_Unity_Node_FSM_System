using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("基本类型 Primitives/向量 Vector")]
    public class NVector : BaseNode
    {
        public float valueX = 0f;
        public float valueY = 0f;
        public float valueZ = 0f;
        public float valueW = 0f;

        [Output(name = "Value")] public Vector4 outputValue = Vector4.zero;

        public override string name => "向量 Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Primitives;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            outputValue = Vector4.zero;
        }
    }
}
