using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("信息 Info/游戏时间 Game Time")]
    public class NGameTime : BaseNode
    {
        [Output(name = "Game Time")] public float gameTime = 0f;
        [Output(name = "Game Frames")] public int gameFrames = 0;
        [Output(name = "Game Time Scale")] public float gameTimeScale = 0f;
        [Output(name = "Delta Time")] public float deltaTime = 0f;

        public override string name => "游戏时间 Game Time";
        public override Color color => NodeMiscData.nodeThemeColor_Info;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            gameTime = 0f;
            gameFrames = 0;
            gameTimeScale = 0f;
        }
    }
}
