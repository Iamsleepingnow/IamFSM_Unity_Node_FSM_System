using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("基本类型 Primitives/字符串 String")]
    public class NString : BaseNode
    {
        [TextArea(3, 3)] public string value = "";

        [Output(name = "Value")] public string outputValue = "";

        public override string name => "字符串 String";
        public override Color color => NodeMiscData.nodeThemeColor_Primitives;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            value = "";
            outputValue = "";
        }
    }
}
