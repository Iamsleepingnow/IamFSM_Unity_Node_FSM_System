using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/向量映射 Map Vector")]
    public class NMapVector : BaseNode
    {
        [Input(name = "Value"), ShowAsDrawer] public Vector4 inputValue;
        [Input(name = "FromMin"), ShowAsDrawer] public Vector4 fromMin;
        [Input(name = "FromMax"), ShowAsDrawer] public Vector4 fromMax;
        [Input(name = "ToMin"), ShowAsDrawer] public Vector4 toMin;
        [Input(name = "ToMax"), ShowAsDrawer] public Vector4 toMax;

        [Output(name = "Result")] public Vector4 outputVector;

        public override string name => "向量映射 Map Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = Vector4.zero;
            fromMin = Vector4.zero;
            fromMax = Vector4.one;
            toMin = Vector4.zero;
            toMax = Vector4.one;
            outputVector = Vector4.zero;
        }
    }
}
