using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("调试 Debug/日志输出 Debug Log")]
    public class NDebugLog : LinearConditionalNode
    {
        [Input(name = "Object")] public object obj = null;

        [TextArea(4, 4)]
        public string text = "";

        public LogType logType = LogType.Log;

        public override string name => "日志输出 Debug Log";
        public override Color color => NodeMiscData.nodeThemeColor_Debug;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            obj = null;
            text = "";
        }
    }
}
