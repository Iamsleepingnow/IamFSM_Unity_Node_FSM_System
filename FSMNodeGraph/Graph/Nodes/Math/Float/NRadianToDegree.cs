using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("计算 Calculate/弧度角度转化 Radian To Degree")]
    public class NRadianToDegree : BaseNode
    {
        public bool isReverse = false;

        [Input(name = "Value"), ShowAsDrawer] public float inputValue = 0f;

        [Output(name = "Result")] public float outputValue = 0f;

        public override string name => "弧度角度转化 Radian To Degree";
        public override Color color => NodeMiscData.nodeThemeColor_Math;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            isReverse = false;
            inputValue = 0f;
            outputValue = 0f;
        }
    }
}
