using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("流程控制 Flow Control/Branch 分支")]
    public class NBranch : BaseNode
    {
        [Input(name = "A")] public object a = null;
        [Input(name = "B")] public object b = null;

        [Input(name = "Is A"), ShowAsDrawer] public bool isA = true;

        [Output(name = "Result")] public object result = null;
        
        public override string name => "Branch 分支";
        public override Color color => NodeMiscData.nodeThemeColor_FlowControl;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = null;
            b = null;
            isA = true;
            result = null;
        }
    }
}
