using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/条件满足 Performed")]
    public class NPerformed : LinearConditionalNode
    {
        [Input(name = "Condition", allowMultiple = true), ShowAsDrawer] public bool condition = false;

        /// <summary>【复合条件逻辑模式】true=与(AND), false=或(OR)</summary>
        public bool isAnd = true;

        public override string name => "条件满足 Performed";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            condition = false;
            isAnd = true;
        }
    }
}
