using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/任意转向量 Any -> Vector")]
    public class NConvertToVector : BaseNode
    {
        [Input(name = "Any")] public object any = null;

        [Output(name = "Vector")] public Vector4 result = Vector4.zero;

        public override string name => "任意 -> 向量 Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            any = null;
            result = Vector4.zero;
        }
    }
}
