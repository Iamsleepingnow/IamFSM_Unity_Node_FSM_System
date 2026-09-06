using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("状态 States/进入状态 In State")]
    public class NInState : BaseNode, IConditionalNode
    {
        [Output(name = "Executes")] public ConditionalLink executes;

        public string state = "Default";

        public override string name => "进入状态 In State";
        public override Color color => NodeMiscData.nodeThemeColor_States;
        public override bool isRenamable => false;

        public IEnumerable<ConditionalNode> GetExecutedNodes() {
            return GetOutputNodes().Where(n => n is ConditionalNode).Select(n => n as ConditionalNode);
        }

        public override FieldInfo[] GetNodeFields() => base.GetNodeFields();

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            state = "Default";
        }
    }
}
