using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("基本类型 Primitives/颜色 Color")]
    public class NColor : BaseNode
    {
        public Color value = Color.black;

        [Output(name = "Value")] public Color outputValue = Color.black;
        
        public override string name => "颜色 Color";
        public override Color color => NodeMiscData.nodeThemeColor_Primitives;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            outputValue = Color.black;
        }
    }
}
