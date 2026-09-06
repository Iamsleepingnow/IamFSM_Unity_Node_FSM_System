using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("信息 Info/当前状态 Current State")]
    public class NCurrentState : BaseNode
    {
        [Output(name = "Current State")] public string currentState = "";

        public override string name => "当前状态 Current State";
        public override Color color => NodeMiscData.nodeThemeColor_Info;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            currentState = "";
        }
    }
}
