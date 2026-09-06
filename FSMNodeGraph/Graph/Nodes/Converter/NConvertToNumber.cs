using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/任意转数字 Any -> Num")]
    public class NConvertToNumber : BaseNode
    {
        [Input(name = "Any")] public object any = null;

        [Output(name = "Number")] public int result = 0;

        public override string name => "任意 -> 数字 Num";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            any = null;
            result = 0;
        }
    }
}
