using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/数字比较 Compare Number")]
    public class NCompareNumber : BaseNode
    {
        public NodeNumberCompareMethod function = NodeNumberCompareMethod.Equal;

        [Input(name = "A"), ShowAsDrawer] public float a = 0f;
        [Input(name = "B"), ShowAsDrawer] public float b = 0f;

        [Output(name = "Result")] public bool result = false;

        public override string name => "数字比较 Compare Number";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            function = NodeNumberCompareMethod.Equal;
            a = 0f;
            b = 0f;
        }
    }
}