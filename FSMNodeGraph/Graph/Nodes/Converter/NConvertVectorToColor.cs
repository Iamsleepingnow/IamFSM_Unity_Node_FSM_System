using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/向量转颜色 Vec -> Color")]
    public class NConvertVectorToColor : BaseNode
    {
        public bool isNormalized = true; // 是否归一化

        [Input(name = "Vector"), ShowAsDrawer] public Vector4 vec;
        [Output(name = "Color")] public Color result;

        public override string name => "向量转颜色 Color";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            vec = Vector4.zero;
            result = Color.black;
        }
    }
}
