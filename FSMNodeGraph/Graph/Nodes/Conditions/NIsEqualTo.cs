using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/等于 Is Equal To")]
    public class NIsEqualTo : BaseNode
    {
        [Input(name = "A")] public object a = null;
        [Input(name = "B")] public object b = null;

        [Output(name = "Result")] public bool outputValue = false;

        public override string name => "等于 Is Equal To";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = null;
            b = null;
            outputValue = false;
        }
    }
}
