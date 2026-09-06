using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/正则匹配 Regular Ex Match")]
    public class NRegularExMatch : BaseNode
    {
        [Input(name = "Value"), ShowAsDrawer] public string inputValue = "";
        [Input(name = "Pattern"), ShowAsDrawer] public string pattern = "";

        [Output(name = "Result")] public bool outputValue = false;

        public override string name => "正则匹配 Regular Ex Match";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = "";
            pattern = "";
            outputValue = false;
        }
    }
}
