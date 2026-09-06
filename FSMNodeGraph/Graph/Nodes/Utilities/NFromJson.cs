using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/数据 <- JSON")]
    public class NFromJson : BaseNode
    {
        public string intKey = "INT";
        public string floatKey = "FLOAT";
        public string boolKey = "BOOL";
        public string stringKey = "STRING";
        public string vectorKey = "VECTOR";
        public string colorKey = "COLOR";

        [Input(name = "JSON"), ShowAsDrawer] public string json = "";

        [Output(name = "Int")] public int intValue = 0;
        [Output(name = "Float")] public float floatValue = 0;
        [Output(name = "Bool")] public bool boolValue = false;
        [Output(name = "String")] public string stringValue = "";
        [Output(name = "Vector")] public Vector4 vectorValue = Vector4.zero;
        [Output(name = "Color")] public Color colorValue = Color.white;
        
        public override string name => "数据 <- JSON";
        public override Color color => NodeMiscData.nodeThemeColor_Utilities;
        public override bool isRenamable => true;

        public override void Process() { }

        public override void OnNodeCreated() {
            base.OnNodeCreated();
            intKey = "INT";
            floatKey = "FLOAT";
            boolKey = "BOOL";
            stringKey = "STRING";
            vectorKey = "VECTOR";
            colorKey = "COLOR";
            json = "";
            intValue = 0;
            floatValue = 0;
            boolValue = false;
            stringValue = "";
            vectorValue = Vector4.zero;
            colorValue = Color.white;
        }
    }
}
