using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("事件 Events/事件触发 Event Invoke")]
    public class NEventInvoke : LinearConditionalNode
    {
        [Input(name = "Message"), ShowAsDrawer] public string message = "";

        public override string name => "事件触发 Event Invoke";
        public override Color color => NodeMiscData.nodeThemeColor_Events;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            message = "";
        }
    }
}
