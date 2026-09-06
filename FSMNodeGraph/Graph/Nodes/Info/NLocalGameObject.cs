using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("信息 Info/本地游戏对象 Local Game Object")]
    public class NLocalGameObject : BaseNode
    {
        [Output(name = "Name")] public string goName;
        [Output(name = "Tag")] public string goTag;
        [Output(name = "Layer")] public int goLayer;
        [Output(name = "Is Static")] public bool goIsStatic;
        [Output(name = "Scene Name")] public string goSceneName;

        public override string name => "本地游戏对象 Local Game Object";
        public override Color color => NodeMiscData.nodeThemeColor_Info;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            goName = "";
            goTag = "";
            goLayer = 0;
            goIsStatic = false;
            goSceneName = "";
        }
    }
}
