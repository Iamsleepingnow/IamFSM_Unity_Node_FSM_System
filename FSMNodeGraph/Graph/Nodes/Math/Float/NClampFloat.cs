using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/浮点钳制 Clamp Float")]
    public class NClampFloat : BaseNode
    {
        [Input(name = "Value"), ShowAsDrawer] public float inputValue = 0f;

        public bool useMin = true;
        [Input(name = "Min"), ShowAsDrawer] public float min = 0f;

        public bool useMax = true;
        [Input(name = "Max"), ShowAsDrawer] public float max = 1f;

        [Output(name = "Result")] public float outputValue = 0f;

        public override string name => "浮点钳制 Clamp Float";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            inputValue = 0f;
            useMin = true;
            min = 0f;
            useMax = true;
            max = 1f;
            outputValue = 0f;
        }
    }
}
