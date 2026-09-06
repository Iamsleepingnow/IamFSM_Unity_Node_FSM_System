using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Linq;

namespace GraphProcessor
{
	public class ExposedParameterFieldView : BlackboardField
	{
		protected BaseGraphView	graphView;

		public ExposedParameter	parameter { get; private set; }

		public ExposedParameterFieldView(BaseGraphView graphView, ExposedParameter param) : base(null, param.name, param.shortType)
		{
			this.graphView = graphView;
			parameter = param;
			this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
			this.Q("icon").AddToClassList("parameter-" + param.shortType);
			this.Q("icon").visible = true;

			(this.Q("textField") as TextField).RegisterValueChangedCallback((e) => {
				param.name = e.newValue;
				text = e.newValue;
				graphView.graph.UpdateExposedParameterName(param, e.newValue);
			});
        }

		void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), DropdownMenuAction.AlwaysEnabled);
            // 延迟到菜单收起后再删除，避免删除导致被右键元素脱离 panel、菜单仍尝试定位而报错
            evt.menu.AppendAction("Delete", (a) =>
                graphView.schedule.Execute(() => graphView.graph.RemoveExposedParameter(parameter)).ExecuteLater(0),
                DropdownMenuAction.AlwaysEnabled);

            evt.StopPropagation();
        }
	}
}