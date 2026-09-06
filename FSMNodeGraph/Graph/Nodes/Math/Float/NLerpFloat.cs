using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点插值 Lerp Float")]
    public class NLerpFloat : BaseNode
    {
        [Input(name = "A"), ShowAsDrawer] public float a = 0f;
        [Input(name = "B"), ShowAsDrawer] public float b = 1f;
        [Input(name = "Delta"), ShowAsDrawer] public float delta = 0f;

        [Output(name = "Result")] public float outputValue = 0f;
        
        public override string name => "浮点插值 Lerp Float";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;
        
        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = 0f;
            b = 1f;
            delta = 0f;
            outputValue = 0f;
        }
    }
}
