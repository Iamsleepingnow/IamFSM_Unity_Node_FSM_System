using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/任意转字符串 Any -> Str")]
    public class NConvertToString : BaseNode
    {
        public int decimalPlaces = 2; // 小数位数

        [Input(name = "Any")] public object any = null;

        [Output(name = "String")] public string result = "";

        public override string name => "任意 -> 字符串 Str";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            any = null;
            result = "";
        }
    }
}
