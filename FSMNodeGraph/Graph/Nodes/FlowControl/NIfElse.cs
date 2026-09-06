using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("流程控制 Flow Control/条件判断 If Else")]
    public class NIfElse : BaseNode
    {
        [Input(name = "Executed", allowMultiple = false)] public ConditionalLink executed;

        [Input(name = "Is A"), ShowAsDrawer] public bool isA = true;

        [Output(name = "ExecutesA")] public ConditionalLink executesA;
        [Output(name = "ExecutesB")] public ConditionalLink executesB;

        public override string name => "If Else 条件判断";
        public override Color color => NodeMiscData.nodeThemeColor_FlowControl;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
        }
    }
}
