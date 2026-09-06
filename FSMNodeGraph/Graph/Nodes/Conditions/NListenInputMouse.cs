using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/鼠标监听 Listen Input Mouse")]
    public class NListenInputMouse : BaseNode
    {
        public int mouseButton = 0;

        [Output(name = "OnMouseDown")] public bool onMouseDown = false;
        [Output(name = "OnMouseUp")] public bool onMouseUp = false;
        [Output(name = "OnMouse")] public bool onMouse = false;

        public override string name => "鼠标监听 Listen Input Mouse";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            mouseButton = 0;
            onMouseDown = false;
            onMouseUp = false;
            onMouse = false;
        }
    }
}
