using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/数学常量 Math Constant")]
    public class NMathConstant : BaseNode
    {
        public NodeMathConstant type;

        [Output(name = "Result")] public float outputValue = 0f;

        public override string name => "数学常量 Math Constant";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            type = NodeMathConstant.PI;
            outputValue = 0f;
        }
    }
}
