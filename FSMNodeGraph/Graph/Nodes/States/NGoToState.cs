using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("状态 States/切换状态 Go to State")]
    public class NGoToState : BaseNode, IConditionalNode
    {
        [Input(name = "Executes", allowMultiple = true)] public ConditionalLink executes;

        [Input(name = "State"), ShowAsDrawer] public string state;

        public override string name => "切换状态 Go to State";
        public override Color color => NodeMiscData.nodeThemeColor_States;
        public override bool isRenamable => false;

        public IEnumerable<ConditionalNode> GetExecutedNodes() {
            return GetInputNodes().Where(n => n is ConditionalNode).Select(n => n as ConditionalNode);
        }

        public override FieldInfo[] GetNodeFields() => base.GetNodeFields();

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            state = "Default";
        }
    }
}
