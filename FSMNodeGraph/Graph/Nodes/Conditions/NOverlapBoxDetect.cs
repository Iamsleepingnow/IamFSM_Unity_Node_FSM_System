using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/箱型重叠检测 Overlap Box Detect")]
    public class NOverlapBoxDetect : BaseNode
    {
        public bool showDebug;

        [Input(name = "Detect Refresh Rate"), ShowAsDrawer] public int detectRefreshRate;
        [Input(name = "Origin"), ShowAsDrawer] public Vector4 origin;
        [Input(name = "Size"), ShowAsDrawer] public Vector4 size;
        [Input(name = "Relative"), ShowAsDrawer] public bool relative;
        [Input(name = "LayerMask"), ShowAsDrawer] public int layerMask;
        [Input(name = "Include Children"), ShowAsDrawer] public bool includeChildren;

        [Output(name = "Did Overlap")] public bool didOverlap;
        [Output(name = "Overlap Object Count")] public int overlapObjectCount;
        
        public override string name => "箱型重叠检测 Overlap Box Detect";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            showDebug = false;
            detectRefreshRate = 8;
            origin = Vector4.zero;
            size = Vector4.one;
            relative = true;
            layerMask = -1;
            includeChildren = false;
            didOverlap = false;
            overlapObjectCount = 0;
        }
    }
}
