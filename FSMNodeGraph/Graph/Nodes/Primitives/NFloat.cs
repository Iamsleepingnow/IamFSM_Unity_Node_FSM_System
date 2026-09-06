using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("基本类型 Primitives/浮点数 Float")]
    public class NFloat : BaseNode
    {
        public float value = 0.0f;

        [Output(name = "Value")] public float outputValue = 0.0f;

        public override string name => "浮点数 Float";
        public override Color color => NodeMiscData.nodeThemeColor_Primitives;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            value = 0.0f;
            outputValue = 0.0f;
        }
    }
}
