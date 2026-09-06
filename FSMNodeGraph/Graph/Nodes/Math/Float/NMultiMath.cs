using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点多值计算 Multi Math")]
    public class NMultiMath : BaseNode
    {
        public NodeMultiMathMethod method;
        
        [Input(name = "A"), ShowAsDrawer] public float a;
        [Input(name = "B"), ShowAsDrawer] public float b;
        
        [Output(name = "Result")] public float outputValue;
        
        public override string name => "浮点多值计算 Multi Math";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;
        
        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            method = NodeMultiMathMethod.Add;
            a = 0f;
            b = 0f;
            outputValue = 0f;
        }
    }
}
