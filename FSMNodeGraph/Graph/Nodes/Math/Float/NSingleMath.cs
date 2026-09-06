using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点单值计算 Single Math")]
    public class NSingleMath : BaseNode
    {
        public NodeSingleMathMethod method;

        [Input(name = "A"), ShowAsDrawer] public float a;

        [Output(name = "Result")] public float outputValue;

        public override string name => "浮点单值计算 Single Math";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            method = NodeSingleMathMethod.Add_1;
            a = 0f;
            outputValue = 0f;
        }
    }
}
