using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/随机浮点 Random Float")]
    public class NRandomFloat : BaseNode
    {
        [Input(name = "Min"), ShowAsDrawer] public float min = 0f;
        [Input(name = "Max"), ShowAsDrawer] public float max = 1f;

        [Output(name = "Result")] public float outputValue = 0f;

        public override string name => "随机浮点 Random Float";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            min = 0f;
            max = 1f;
            outputValue = 0f;
        }
    }
}
