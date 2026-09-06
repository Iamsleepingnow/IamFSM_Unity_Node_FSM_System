using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/字符串拼接 String Concat")]
    public class NStringConcat : BaseNode
    {
        public string separator = "";

        [Input(name = "A"), ShowAsDrawer] public string a;
        [Input(name = "B"), ShowAsDrawer] public string b;

        [Output(name = "Result")] public string outputValue = "";

        public override string name => "字符串拼接 String Concat";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;
        
        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            separator = "";
            a = "";
            b = "";
            outputValue = "";
        }
    }
}
