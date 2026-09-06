using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点映射 Map Float")]
    public class NMapFloat : BaseNode
    {
        [Input(name = "Value"), ShowAsDrawer] public float inputValue = 0f;
        [Input(name = "FromMin"), ShowAsDrawer] public float fromMin = 0f;
        [Input(name = "FromMax"), ShowAsDrawer] public float fromMax = 1f;
        [Input(name = "ToMin"), ShowAsDrawer] public float toMin = 0f;
        [Input(name = "ToMax"), ShowAsDrawer] public float toMax = 1f;

        [Output(name = "Result")] public float outputValue = 0f;

        public override string name => "浮点映射 Map Float";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = 0f;
            fromMin = 0f;
            fromMax = 1f;
            toMin = 0f;
            toMax = 1f;
            outputValue = 0f;
        }
    }
}
