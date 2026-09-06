using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/向量插值 Lerp Vector")]
    public class NLerpVector : BaseNode
    {
        [Input(name = "A"), ShowAsDrawer] public Vector4 a;
        [Input(name = "B"), ShowAsDrawer] public Vector4 b;
        [Input(name = "Delta"), ShowAsDrawer] public float delta = 0f;

        [Output(name = "Result")] public Vector4 outputVector;
        
        public override string name => "向量插值 Lerp Vector";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;
        
        public override void Process() { }
        
        public override void OnNodeCreated() {
            base.OnNodeCreated();
            a = Vector4.zero;
            b = Vector4.one;
            delta = 0f;
            outputVector = Vector4.zero;
        }
    }
}
