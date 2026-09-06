using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/任意转颜色 Any -> Color")]
    public class NConvertToColor : BaseNode
    {
        public bool isNormalized = true; // 是否归一化

        [Input(name = "Any")] public object any = null;

        [Output(name = "Color")] public Color result = Color.white;

        public override string name => "任意 -> 颜色 Color";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            any = null;
            result = Color.white;
        }
    }
}
