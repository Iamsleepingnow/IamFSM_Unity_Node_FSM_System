using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GraphProcessor;

namespace GraphProcessor
{
	[System.Serializable]
	public class ParameterNode : BaseNode
	{
		// ====== 数据端口 ======
		[Input]
		public object input;

		[Output]
		public object output;

		// ====== 执行端口（仅在 Set 模式生效）======
		[Input(name = "Executed", allowMultiple = true)]
		public ConditionalLink executed;

		// [Output(name = "Executes")]
		// public ConditionalLink executes;

		public override string name => "Parameter";

		// We serialize the GUID of the exposed parameter in the graph so we can retrieve the true ExposedParameter from the graph
		[SerializeField, HideInInspector]
		public string parameterGUID;

		public ExposedParameter parameter { get; private set; }

		public event Action onParameterChanged;

		public ParameterAccessor accessor;

		// 将 "executed" 端口排到最顶部（模仿 ConditionalNode 的行为）
		public override FieldInfo[] GetNodeFields() {
			var fields = base.GetNodeFields();
			Array.Sort(fields, (f1, f2) => f1.Name == nameof(executed) ? -1 : 1);
			return fields;
		}

		protected override void Enable()
		{
			// load the parameter
			LoadExposedParameter();

			graph.onExposedParameterModified += OnParamChanged;
			graph.onExposedParameterValueChanged += OnParamValueChanged;
			if (onParameterChanged != null)
				onParameterChanged?.Invoke();
		}

		protected override void Disable()
		{
			graph.onExposedParameterModified -= OnParamChanged;
			graph.onExposedParameterValueChanged -= OnParamValueChanged;
		}

		void LoadExposedParameter()
		{
			parameter = graph.GetExposedParameterFromGUID(parameterGUID);

			if (parameter == null)
			{
				Debug.Log("Property \"" + parameterGUID + "\" Can't be found !");

				// Delete this node as the property can't be found
				graph.RemoveNode(this);
				return;
			}

			output = parameter.value;
		}

		void OnParamChanged(ExposedParameter modifiedParam)
		{
			if (parameter == modifiedParam)
			{
				onParameterChanged?.Invoke();
			}
		}

		/// <summary>
		/// Inspector 面板修改参数值时触发，立即同步 output 并推送数据到下游节点
		/// </summary>
		void OnParamValueChanged(ExposedParameter modifiedParam)
		{
			if (parameter == modifiedParam && accessor == ParameterAccessor.Get)
			{
				output = parameter.value;
				outputPorts.PushDatas();
			}
		}

		[CustomPortBehavior(nameof(output))]
		IEnumerable<PortData> GetOutputPort(List<SerializableEdge> edges)
		{
			if (accessor == ParameterAccessor.Get)
			{
				yield return new PortData
				{
					identifier = "output",
					displayName = "Value",
					displayType = (parameter == null) ? typeof(object) : parameter.GetValueType(),
					acceptMultipleEdges = true
				};
			}
		}

		// [CustomPortBehavior(nameof(executes))]
		// IEnumerable<PortData> GetExecOutputPort(List<SerializableEdge> edges)
		// {
		// 	if (accessor == ParameterAccessor.Set)
		// 	{
		// 		yield return new PortData
		// 		{
		// 			identifier = "executes",
		// 			displayName = "Executes",
		// 			displayType = typeof(ConditionalLink),
		// 			acceptMultipleEdges = false
		// 		};
		// 	}
		// }

		[CustomPortBehavior(nameof(input))]
		IEnumerable<PortData> GetInputPort(List<SerializableEdge> edges)
		{
			if (accessor == ParameterAccessor.Set)
			{
				yield return new PortData
				{
					identifier = "input",
					displayName = "Value",
					displayType = (parameter == null) ? typeof(object) : parameter.GetValueType(),
				};
			}
		}

		[CustomPortBehavior(nameof(executed))]
		IEnumerable<PortData> GetExecInputPort(List<SerializableEdge> edges)
		{
			if (accessor == ParameterAccessor.Set)
			{
				yield return new PortData
				{
					identifier = "executed",
					displayName = "Executed",
					displayType = typeof(ConditionalLink),
					acceptMultipleEdges = true
				};
			}
		}

		public override void Process()
		{
#if UNITY_EDITOR // In the editor, an undo/redo can change the parameter instance in the graph, in this case the field in this class will point to the wrong parameter
			parameter = graph.GetExposedParameterFromGUID(parameterGUID);
#endif

			if (accessor == ParameterAccessor.Get)
				output = parameter.value;
			else
				graph.UpdateExposedParameter(parameter.guid, input);
		}
	}

	public enum ParameterAccessor
	{
		Get,
		Set
	}
}
