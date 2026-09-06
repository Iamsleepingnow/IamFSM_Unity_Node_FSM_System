using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/碰撞检测 Collision Detect")]
    public class NCollisionDetect : BaseNode
    {
        public float selfRBDSleepThreshold;
        public RigidbodySleepMode2D selfRBG2DSleepMode;

        [Input(name = "Pass Layer Mask"), ShowAsDrawer] public int passLayerMask;

        [Output(name = "On Collision Enter")] public bool onCollisionEnter;
        [Output(name = "On Collision Stay")] public bool onCollisionStay;
        [Output(name = "On Collision Exit")] public bool onCollisionExit;
        [Output(name = "On Collision Enter 2D")] public bool onCollisionEnter2D;
        [Output(name = "On Collision Stay 2D")] public bool onCollisionStay2D;
        [Output(name = "On Collision Exit 2D")] public bool onCollisionExit2D;

        [Output(name = "Enter Object Count")] public int enterObjectCount;
        [Output(name = "Stay Object Count")] public int stayObjectCount;
        [Output(name = "Exit Object Count")] public int exitObjectCount;
        
        public override string name => "碰撞检测 Collision Detect";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            selfRBDSleepThreshold = 0.005f;
            selfRBG2DSleepMode = RigidbodySleepMode2D.StartAwake;
            passLayerMask = -1;
            onCollisionEnter = false;
            onCollisionStay = false;
            onCollisionExit = false;
            onCollisionEnter2D = false;
            onCollisionStay2D = false;
            onCollisionExit2D = false;
            enterObjectCount = 0;
            stayObjectCount = 0;
            exitObjectCount = 0;
        }
    }
}
