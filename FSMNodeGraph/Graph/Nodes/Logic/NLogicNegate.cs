using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("逻辑 Logic/取反 NOT")]
    public class NLogicNegate : BaseNode
    {
        [Input(name = "A"), ShowAsDrawer] public bool a = false;

        [Output(name = "Result")] public bool result = false;

        public override string name => "取反 NOT";
        public override Color color => NodeMiscData.nodeThemeColor_Logic;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = false;
            result = false;
        }
    }
}
