using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;

namespace FSMGraph.Editor
{
    [NodeCustomEditor(typeof(NColor))]
    public class NColorEditor : BaseNodeView
    {
        NColor color;

        public override void Enable() {
            base.Enable();
            color = (NColor)nodeTarget;
            color.InvokeOnProcessed();
            controlsContainer.Add(new Label("<color=#ffffff00>----------------------------------</color>"));
        }
    }
}
