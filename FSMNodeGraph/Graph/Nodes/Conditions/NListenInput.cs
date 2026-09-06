using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/输入监听 Listen Input")]
    public class NListenInput : BaseNode
    {
        [Input(name = "Action Map"), ShowAsDrawer] public string actionMap = "";
        [Input(name = "Action Name"), ShowAsDrawer] public string actionName = "";

        [Output(name = "OnStarted")] public bool onStarted = false;
        [Output(name = "OnPerformed")] public bool onPerformed = false;
        [Output(name = "OnCanceled")] public bool onCanceled = false;

        public override string name => "输入监听 Listen Input";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            actionMap = "";
            actionName = "";
            onStarted = false;
            onPerformed = false;
            onCanceled = false;
        }
    }
}
