using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点噪声 Noise Float")]
    public class NNoiseFloat : BaseNode
    {
        [Input(name = "Position X"), ShowAsDrawer] public float positionX;
        [Input(name = "Position Y"), ShowAsDrawer] public float positionY;

        [Output(name = "Result")] public float outputValue;

        public override string name => "浮点噪声 Noise Float";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            positionX = 0f;
            positionY = 0f;
            outputValue = 0f;
        }
    }
}
