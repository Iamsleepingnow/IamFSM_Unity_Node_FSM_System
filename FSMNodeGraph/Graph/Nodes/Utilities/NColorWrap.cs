using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/颜色通道置换 Color Wrap")]
    public class NColorWrap : BaseNode
    {
        public NodeColorChannel r = NodeColorChannel.Red;
        public NodeColorChannel g = NodeColorChannel.Green;
        public NodeColorChannel b = NodeColorChannel.Blue;
        public NodeColorChannel a = NodeColorChannel.Alpha;

        [Input(name = "Value"), ShowAsDrawer] public Color inputValue;

        [Output(name = "Result")] public Color outputValue;

        public override string name => "颜色通道置换 Color Wrap";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            r = NodeColorChannel.Red;
            g = NodeColorChannel.Green;
            b = NodeColorChannel.Blue;
            a = NodeColorChannel.Alpha;
            inputValue = Color.black;
            outputValue = Color.black;
        }
    }
}
