using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("条件 Conditions/触发检测 Trigger Detect")]
    public class NTriggerDetect : BaseNode
    {
        public float selfRBDSleepThreshold;
        public RigidbodySleepMode2D selfRBG2DSleepMode;

        [Input(name = "Pass Layer Mask"), ShowAsDrawer] public int passLayerMask;

        [Output(name = "On Trigger Enter")] public bool onTriggerEnter;
        [Output(name = "On Trigger Stay")] public bool onTriggerStay;
        [Output(name = "On Trigger Exit")] public bool onTriggerExit;
        [Output(name = "On Trigger Enter 2D")] public bool onTriggerEnter2D;
        [Output(name = "On Trigger Stay 2D")] public bool onTriggerStay2D;
        [Output(name = "On Trigger Exit 2D")] public bool onTriggerExit2D;

        [Output(name = "Enter Object Count")] public int enterObjectCount;
        [Output(name = "Stay Object Count")] public int stayObjectCount;
        [Output(name = "Exit Object Count")] public int exitObjectCount;

        public override string name => "触发检测 Trigger Detect";
        public override Color color => NodeMiscData.nodeThemeColor_Conditions;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            selfRBDSleepThreshold = 0.005f;
            selfRBG2DSleepMode = RigidbodySleepMode2D.StartAwake;
            passLayerMask = -1;
            onTriggerEnter = false;
            onTriggerStay = false;
            onTriggerExit = false;
            onTriggerEnter2D = false;
            onTriggerStay2D = false;
            onTriggerExit2D = false;
            enterObjectCount = 0;
            stayObjectCount = 0;
            exitObjectCount = 0;
        }
    }
}
