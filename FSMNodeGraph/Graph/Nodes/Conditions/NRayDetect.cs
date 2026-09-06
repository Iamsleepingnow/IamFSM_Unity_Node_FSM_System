using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/射线检测 Ray Detect")]
    public class NRayDetect : BaseNode
    {
        public bool showDebug;

        [Input(name = "Detect Refresh Rate"), ShowAsDrawer] public int detectRefreshRate;
        [Input(name = "Origin"), ShowAsDrawer] public Vector4 origin;
        [Input(name = "Direction"), ShowAsDrawer] public Vector4 direction;
        [Input(name = "Distance"), ShowAsDrawer] public float distance;
        [Input(name = "Relative"), ShowAsDrawer] public bool relative;
        [Input(name = "LayerMask"), ShowAsDrawer] public int layerMask;
        [Input(name = "Include Children"), ShowAsDrawer] public bool includeChildren;

        [Output(name = "Did Hit")] public bool didHit;
        [Output(name = "Hit Point")] public Vector4 hitPoint;
        [Output(name = "Hit Normal")] public Vector4 hitNormal;
        [Output(name = "Hit Distance")] public float hitDistance;
        [Output(name = "Hit Object Name")] public string hitObjectName;
        
        public override string name => "射线检测 Ray Detect";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            showDebug = false;
            detectRefreshRate = 8;
            origin = Vector4.zero;
            direction = new Vector4(1, 0, 0, 0);
            distance = 100;
            relative = true;
            layerMask = -1;
            includeChildren = false;
            didHit = false;
            hitPoint = Vector4.zero;
            hitNormal = Vector4.zero;
            hitDistance = 0;
            hitObjectName = "";
        }
    }
}
