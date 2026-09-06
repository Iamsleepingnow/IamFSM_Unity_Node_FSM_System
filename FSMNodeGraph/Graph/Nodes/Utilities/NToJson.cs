using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;

namespace FSMGraph
{
    [System.Serializable, NodeMenuItem("实用 Utilities/数据 -> JSON")]
    public class NToJson : BaseNode
    {
        public string intKey = "INT";
        public string floatKey = "FLOAT";
        public string boolKey = "BOOL";
        public string stringKey = "STRING";
        public string vectorKey = "VECTOR";
        public string colorKey = "COLOR";

        [Input(name = "Int"), ShowAsDrawer] public int intValue = 0;
        [Input(name = "Float"), ShowAsDrawer] public float floatValue = 0.0f;
        [Input(name = "Bool"), ShowAsDrawer] public bool boolValue = false;
        [Input(name = "String"), ShowAsDrawer] public string stringValue = "";
        [Input(name = "Vector"), ShowAsDrawer] public Vector4 vectorValue = Vector4.zero;
        [Input(name = "Color"), ShowAsDrawer] public Color colorValue = Color.black;

        [Output(name = "JSON")] public string outputValue = "";
        
        public override string name => "数据 -> JSON";
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
            intValue = 0;
            floatValue = 0.0f;
            boolValue = false;
            stringValue = "";
            vectorValue = Vector4.zero;
            colorValue = Color.black;
            outputValue = "";
        }
    }
}
