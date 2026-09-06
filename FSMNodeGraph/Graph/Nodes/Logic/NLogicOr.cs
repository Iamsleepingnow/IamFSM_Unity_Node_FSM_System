using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("逻辑 Logic/或 OR")]
    public class NLogicOr : BaseNode
    {
        [Input(name = "A"), ShowAsDrawer] public bool a = false;
        [Input(name = "B"), ShowAsDrawer] public bool b = false;

        [Output(name = "Result")] public bool result = false;

        public override string name => "或 OR";
        public override Color color => NodeMiscData.nodeThemeColor_Logic;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = false;
            b = false;
            result = false;
        }
    }
}
