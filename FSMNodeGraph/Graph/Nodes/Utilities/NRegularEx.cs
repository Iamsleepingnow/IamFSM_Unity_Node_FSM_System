using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/正则表达式 Regular Ex")]
    public class NRegularEx : BaseNode
    {
        public NodeRegularExpressionMethod method = NodeRegularExpressionMethod.Remove;

        [Input(name = "Value"), ShowAsDrawer] public string inputValue = "";
        [Input(name = "Pattern"), ShowAsDrawer] public string pattern = "";
        [Input(name = "Replace"), ShowAsDrawer] public string replace = "";

        [Output(name = "Result")] public string outputValue = "";

        public override string name => "正则表达式 Regular Ex";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            method = NodeRegularExpressionMethod.Remove;
            inputValue = "";
            pattern = "";
            replace = "";
            outputValue = "";
        }
    }
}
