using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;

namespace GraphProcessor
{
	public class ExposedParameterPropertyView : VisualElement
	{
		protected BaseGraphView baseGraphView;

		public ExposedParameter parameter { get; private set; }

		public Toggle     hideInInspector { get; private set; }

		public ExposedParameterPropertyView(BaseGraphView graphView, ExposedParameter param)
		{
			baseGraphView = graphView;
			parameter      = param;

			// 防御：窗口关闭/切换在图释放工厂后仍触发列表刷新时，静态工厂已为 null
			if (graphView?.exposedParameterFactory == null)
			{
				Debug.LogWarning("[ExposedParameterPropertyView] exposedParameterFactory 尚未初始化或被释放，跳过参数设置字段创建");
				return;
			}

			var field = graphView.exposedParameterFactory.GetParameterSettingsField(param, (newValue) => {
				param.settings = newValue as ExposedParameter.Settings;
			});

			Add(field);
		}
	}
} 