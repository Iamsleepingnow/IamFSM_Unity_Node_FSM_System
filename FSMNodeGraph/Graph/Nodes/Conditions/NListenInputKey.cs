using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/按键监听 Listen Input Key")]
    public class NListenInputKey : BaseNode
    {
        public KeyCode keyCode = KeyCode.Space;

        [Output(name = "OnKeyDown")] public bool onKeyDown = false;
        [Output(name = "OnKeyUp")] public bool onKeyUp = false;
        [Output(name = "OnKey")] public bool onKey = false;

        public override string name => "按键监听 Listen Input Key";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            keyCode = KeyCode.Space;
            onKeyDown = false;
            onKeyUp = false;
            onKey = false;
        }
    }
}
