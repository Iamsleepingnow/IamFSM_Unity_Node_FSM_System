using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/颜色翻转 Color Flip")]
    public class NColorFlip : BaseNode
    {
        [Input(name = "Color"), ShowAsDrawer] public Color inputValue;

        [Input(name = "FlipR"), ShowAsDrawer] public bool flipR = false;
        [Input(name = "FlipG"), ShowAsDrawer] public bool flipG = false;
        [Input(name = "FlipB"), ShowAsDrawer] public bool flipB = false;
        [Input(name = "FlipA"), ShowAsDrawer] public bool flipA = false;

        [Output(name = "Result")] public Color outputValue;

        public override string name => "颜色翻转 Color Flip";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = Color.black;
            flipR = false;
            flipG = false;
            flipB = false;
            flipA = false;
            outputValue = Color.black;
        }
    }
}
