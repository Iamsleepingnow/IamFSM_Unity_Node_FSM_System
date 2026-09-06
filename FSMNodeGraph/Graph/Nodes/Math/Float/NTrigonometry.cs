using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/三角函数 Trigonometry")]
    public class NTrigonometry : BaseNode
    {
        public NodeTrigonometryMethod method;
        public bool isDegrees; // 是否为角度制，否则为弧度制
        
        [Input(name = "A"), ShowAsDrawer] public float a;
        
        [Output(name = "Result")] public float outputValue;
        
        public override string name => "三角函数 Trigonometry";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            method = NodeTrigonometryMethod.Sin;
            a = 0;
            outputValue = 0;
        }
    }
}
