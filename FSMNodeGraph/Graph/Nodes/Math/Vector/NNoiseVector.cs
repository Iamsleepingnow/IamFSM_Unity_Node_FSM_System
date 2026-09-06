using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/向量噪声 Noise Vector")]
    public class NNoiseVector : BaseNode
    {
        [Input(name = "PositionX"), ShowAsDrawer] public Vector4 positionX;
        [Input(name = "PositionY"), ShowAsDrawer] public Vector4 positionY;

        [Output(name = "Result")] public Vector4 outputValue;
        
        public override string name => "向量噪声 Noise Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            positionX = Vector4.zero;
            positionY = Vector4.zero;
            outputValue = Vector4.zero;
        }
    }
}
