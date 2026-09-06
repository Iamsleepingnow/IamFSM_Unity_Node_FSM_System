using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/随机向量 Random Vector")]
    public class NRandomVector : BaseNode
    {
        [Input(name = "Min"), ShowAsDrawer] public Vector4 min = Vector4.zero;
        [Input(name = "Max"), ShowAsDrawer] public Vector4 max = Vector4.one;

        [Output(name = "Result")] public Vector4 outputValue = Vector4.zero;

        public override string name => "随机向量 Random Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            min = Vector4.zero;
            max = Vector4.one;
            outputValue = Vector4.zero;
        }
    }
}
