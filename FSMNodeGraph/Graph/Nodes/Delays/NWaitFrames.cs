using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("延时 Delays/等待帧数 Wait Frames")]
    public class NWaitFrames : BaseNode
    {
        [Input(name = "Executed", allowMultiple = false)] public ConditionalLink executed;

        [Output(name = "Executes")] public ConditionalLink executes;
        [Output(name = "Delay Update")] public ConditionalLink delayUpdate;
        [Output(name = "Break")] public ConditionalLink breakLink;

        [Output(name = "Progress")] public float progress;
        
        [Input(name = "Frames"), ShowAsDrawer] public int frames = 0;

        [Input(name = "Is Break"), ShowAsDrawer] public bool isBreak = false;
        [Input(name = "Is Background"), ShowAsDrawer] public bool isBackground = false;

        public override string name => "等待帧数 Wait Frames";
        public override Color color => NodeMiscData.nodeThemeColor_Delay;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            frames = 0;
            isBreak = false;
            isBackground = false;
        }
    }
}
