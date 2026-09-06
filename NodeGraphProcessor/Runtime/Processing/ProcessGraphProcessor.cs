using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;
// using Unity.Entities;

namespace GraphProcessor
{
	/// <summary>
	/// 仅执行从 "流程发起者" （如 NStartFlow）沿 ConditionalLink 边可达的节点的图处理器
	/// </summary>
	public class ProcessGraphProcessor : BaseGraphProcessor
	{
		List<BaseNode> processList;

		/// <summary>
		/// 从起始节点出发，通过条件执行边可达的节点集合
		/// </summary>
		HashSet<BaseNode> conditionalReachable = new HashSet<BaseNode>();

		public ProcessGraphProcessor(BaseGraph graph) : base(graph) { }

		public override void UpdateComputeOrder() {
			// 按 computeOrder 排序所有节点
			processList = graph.nodes.OrderBy(n => n.computeOrder).ToList();

			// 重新收集条件执行流可达节点
			CollectConditionalReachableNodes();
		}

		/// <summary>
		/// 先同步所有 ParameterNode 的最新值，再执行条件执行流中可达的节点
		/// </summary>
		public override void Run() {
			// 预同步：将所有 ParameterNode 的 Inspector 最新值推送到图中
			SyncParameterNodes();

			int count = processList.Count;

			for (int i = 0; i < count; i++) {
				var node = processList[i];
				// 跳过不在条件执行流中的节点
				if (!conditionalReachable.Contains(node))
					continue;

				node.OnProcess();
			}
		}

		void SyncParameterNodes() {
			foreach (var node in graph.nodes) {
				if (node is ParameterNode paramNode)
					paramNode.OnProcess();
			}
		}

		/// <summary>
		/// 从图中的"流程发起者"节点出发，沿 ConditionalLink 边做 BFS，
		/// 收集所有可达节点到 conditionalReachable 集合中。
		/// </summary>
		void CollectConditionalReachableNodes() {
			conditionalReachable.Clear();

			var queue = new Queue<BaseNode>();

			// 找到所有"流程发起者"：有 ConditionalLink 输出端口但没有 ConditionalLink 输入端口的节点
			foreach (var node in graph.nodes) {
				bool hasConditionalOutput = node.outputPorts.Any(p => p.portData.displayType == typeof(ConditionalLink));
				bool hasConditionalInput = node.inputPorts.Any(p => p.portData.displayType == typeof(ConditionalLink));

				if (hasConditionalOutput && !hasConditionalInput) {
					conditionalReachable.Add(node);
					queue.Enqueue(node);
				}
			}

			// BFS 沿 conditional 边遍历
			while (queue.Count > 0) {
				var current = queue.Dequeue();

				foreach (var outputPort in current.outputPorts) {
					// 仅沿 ConditionalLink 类型的输出端口传播
					if (outputPort.portData.displayType != typeof(ConditionalLink))
						continue;

					foreach (var edge in outputPort.GetEdges()) {
						var nextNode = edge.inputNode;
						if (nextNode == null)
							continue;

						if (conditionalReachable.Add(nextNode))
							queue.Enqueue(nextNode);
					}
				}
			}
		}
	}
}