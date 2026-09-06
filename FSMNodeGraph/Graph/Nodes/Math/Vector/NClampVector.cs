using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/向量钳制 Clamp Vector")]
    public class NClampVector : BaseNode
    {
        [Input(name = "Vector"), ShowAsDrawer] public Vector4 vector;

        public bool useMin = true;
        [Input(name = "Min"), ShowAsDrawer] public Vector4 min;

        public bool useMax = true;
        [Input(name = "Max"), ShowAsDrawer] public Vector4 max;

        [Output(name = "Result")] public Vector4 outputVector;

        public override string name => "向量钳制 Clamp Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            vector = Vector4.zero;
            useMin = true;
            min = Vector4.zero;
            useMax = true;
            max = Vector4.one;
            outputVector = Vector4.zero;
        }
    }
}
