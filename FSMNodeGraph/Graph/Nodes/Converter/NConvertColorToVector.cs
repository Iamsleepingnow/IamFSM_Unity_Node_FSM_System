using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/颜色转向量 Color -> Vec")]
    public class NConvertColorToVector : BaseNode
    {
        public bool isNormalized = true; // 是否归一化

        [Input(name = "Color"), ShowAsDrawer] public Color col;
        [Output(name = "Vector")] public Vector4 result;

        public override string name => "颜色 ->向量 Vec";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            col = Color.black;
            result = Vector4.zero;
        }
    }
}
