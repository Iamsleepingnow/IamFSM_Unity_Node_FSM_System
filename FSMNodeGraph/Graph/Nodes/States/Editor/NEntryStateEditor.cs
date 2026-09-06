using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;

namespace FSMGraph.Editor
{
    [NodeCustomEditor(typeof(NEntryState))]
    public class NEntryStateEditor : BaseNodeView
    {
        NEntryState node;

        public override void Enable() {
            base.Enable();
            node = nodeTarget as NEntryState;
            node.InvokeOnProcessed();
        }
    }
}
