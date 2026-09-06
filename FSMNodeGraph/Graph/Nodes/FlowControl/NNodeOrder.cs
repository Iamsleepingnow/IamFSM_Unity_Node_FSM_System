using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    /// <summary>
    /// 分支排序节点 Node Order：作为纯透传的执行节点，用于显式控制平行分支的执行先后顺序。
    /// 引擎在推进平行分支时按其 Order 值升序执行（数值越小越先），无 NNodeOrder 的分支默认 0。
    /// </summary>
    [System.Serializable, NodeMenuItem("流程控制 Flow Control/分支排序 Node Order")]
    public class NNodeOrder : LinearConditionalNode
    {
        [Input(name = "Order"), ShowAsDrawer] public int order = 0;

        public override string name => "分支排序 Node Order";
        public override Color color => NodeMiscData.nodeThemeColor_FlowControl;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            order = 0;
        }
    }
}