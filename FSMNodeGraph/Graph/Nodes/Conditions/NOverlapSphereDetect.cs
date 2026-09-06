using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/球型重叠检测 Overlap Sphere Detect")]
    public class NOverlapSphereDetect : BaseNode
    {
        public bool showDebug;

        [Input(name = "Detect Refresh Rate"), ShowAsDrawer] public int detectRefreshRate;
        [Input(name = "Origin"), ShowAsDrawer] public Vector4 origin;
        [Input(name = "Radius"), ShowAsDrawer] public float radius;
        [Input(name = "Relative"), ShowAsDrawer] public bool relative;
        [Input(name = "LayerMask"), ShowAsDrawer] public int layerMask;
        [Input(name = "Include Children"), ShowAsDrawer] public bool includeChildren;

        [Output(name = "Did Overlap")] public bool didOverlap;
        [Output(name = "Overlap Object Count")] public int overlapObjectCount;

        public override string name => "球型重叠检测 Overlap Sphere Detect";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            showDebug = false;
            detectRefreshRate = 8;
            origin = Vector4.zero;
            radius = 1;
            relative = true;
            layerMask = -1;
            includeChildren = false;
            didOverlap = false;
            overlapObjectCount = 0;
        }
    }
}
