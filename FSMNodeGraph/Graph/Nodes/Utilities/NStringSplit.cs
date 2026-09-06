using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/字符串分割提取 String Split")]
    public class NStringSplit : BaseNode
    {
        public string separator = "";

        [Input(name = "Value"), ShowAsDrawer] public string inputValue = "";
        [Input(name = "Result Index"), ShowAsDrawer] public int resultIndex = 0;

        [Output(name = "Result")] public string outputValue = "";

        public override string name => "字符串分割提取 String Split";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            separator = "";
            inputValue = "";
            resultIndex = 0;
            outputValue = "";
        }
    }
}
