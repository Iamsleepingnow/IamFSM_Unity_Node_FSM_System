using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("基本类型 Primitives/整数 Integer")]
    public class NInteger : BaseNode
    {
        public int value = 0;

        [Output(name = "Value")] public int outputValue = 0;

        public override string name => "整数 Integer";
        public override Color color => NodeMiscData.nodeThemeColor_Primitives;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            value = 0;
            outputValue = 0;
        }
    }
}
