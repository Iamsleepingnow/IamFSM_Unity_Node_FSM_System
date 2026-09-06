using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("转换 Converter/任意转布尔 Any -> Bool")]
    public class NConvertToBool : BaseNode
    {
        [Input(name = "Any")] public object any = null;

        [Output(name = "Boolean")] public bool result = false;

        public override string name => "任意 -> 布尔 Bool";
        public override Color color => NodeMiscData.nodeThemeColor_Converters;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            any = null;
            result = false;
        }
    }
}
