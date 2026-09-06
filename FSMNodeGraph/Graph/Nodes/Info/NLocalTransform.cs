using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("信息 Info/本地方位变换 Local Transform")]
    public class NLocalTransform : BaseNode
    {
        public bool isRelative = false; // 是否相对变换

        [Output(name = "Position")] public Vector4 tPosition;
        [Output(name = "Rotation")] public Vector4 tRotation;
        [Output(name = "Scale")] public Vector4 tScale;

        public override string name => "本地方位变换 Local Transform";
        public override Color color => NodeMiscData.nodeThemeColor_Info;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            isRelative = false;
            tPosition = Vector4.zero;
            tRotation = Vector4.zero;
            tScale = Vector4.zero;
        }
    }
}
