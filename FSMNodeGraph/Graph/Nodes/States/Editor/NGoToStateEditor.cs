using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;

namespace FSMGraph.Editor
{
    [NodeCustomEditor(typeof(NGoToState))]
    public class NGoToStateEditor : BaseNodeView
    {
        NGoToState node;

        public override void Enable() {
            base.Enable();
            node = nodeTarget as NGoToState;
            node.InvokeOnProcessed();
        }
    }
}
