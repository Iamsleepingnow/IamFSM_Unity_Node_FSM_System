using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/向量计算 Vector Math")]
    public class NVectorMath : BaseNode
    {
        public NodeVectorMathMethod method;

        [Input(name = "A"), ShowAsDrawer] public Vector4 a;
        [Input(name = "B"), ShowAsDrawer] public Vector4 b;

        [Output(name = "Result")] public Vector4 outputVector;

        public override string name => "向量计算 Vector Math";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            method = NodeVectorMathMethod.Add;
            a = Vector4.zero;
            b = Vector4.zero;
            outputVector = Vector4.zero;
        }
    }
}
