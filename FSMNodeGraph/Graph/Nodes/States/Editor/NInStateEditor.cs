using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;

namespace FSMGraph.Editor
{
    [NodeCustomEditor(typeof(NInState))]
    public class NInStateEditor : BaseNodeView
    {
        NInState node;

        public override void Enable() {
            base.Enable();
            node = nodeTarget as NInState;
            node.InvokeOnProcessed();
        }
    }
}
