using System.Linq;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace GraphProcessor
{
	[System.Serializable]
	public abstract class BaseGraphWindow : EditorWindow
	{
		protected VisualElement		rootView;
		protected BaseGraphView		graphView;

		[SerializeField]
		public BaseGraph			graph;

		readonly string				graphWindowStyle = "GraphProcessorStyles/BaseGraphView";

		public bool					isGraphLoaded
		{
			get { return graphView != null && graphView.graph != null; }
		}

		bool						reloadWorkaround = false;

		public event Action< BaseGraph >	graphLoaded;
		public event Action< BaseGraph >	graphUnloaded;

		/// <summary>
		/// Called by Unity when the window is enabled / opened
		/// </summary>
		protected virtual void OnEnable()
		{
			InitializeRootView();

			if (graph != null)
				LoadGraph();
			else
				reloadWorkaround = true;
		}

		protected virtual void Update()
		{
			// Workaround for the Refresh option of the editor window:
			// When Refresh is clicked, OnEnable is called before the serialized data in the
			// editor window is deserialized, causing the graph view to not be loaded
			if (reloadWorkaround && graph != null)
			{
				LoadGraph();
				reloadWorkaround = false;
			}

			// 编译(Domain Reload)后的端口裂缝兜底：
			// 修复"节点表窗口开启时更新代码触发编译 → 节点只剩标题头与变量、端口消失、
			// 重开窗口端口恢复但连线丢失"的竞态问题。见 RebuildViewIfPortsMissing。
			RebuildViewIfPortsMissing();
		}

		/// <summary>
		/// 领域重载兜底：图数据端口已就绪、但窗口 GraphView 的视图端口缺失时，强制重建视图。
		/// 触发场景：编译(Domain Reload)后窗口 OnEnable 早于 asset.OnEnable 的端口重建，
		/// 导致已构建的 GraphView 停留在"端口消失"的空态——数据层稍后恢复但视图不刷新。
		/// 仅在异常态重建，正常运行（视图端口齐全）不触发。
		/// </summary>
		void RebuildViewIfPortsMissing()
		{
			if (graph == null || graphView == null || !graph.isEnabled)
				return;

			// 空图无须兜底
			if (graph.edges == null || graph.edges.Count == 0)
				return;

			// 数据层端口：node 上的端口（asset.OnEnable→InitializePorts 重建）
			bool anyDataPort = graph.nodes.Any(n => n != null && (
				(n.inputPorts   != null && n.inputPorts.Count   > 0) ||
				(n.outputPorts  != null && n.outputPorts.Count  > 0)));

			if (!anyDataPort)
				return; // 数据尚未就绪，交给既有 onEnabled/reloadWorkaround 路径

			// 视图层端口：GraphView 里实际渲染的端口
			bool anyViewPort = graphView.nodeViews.Any(nv => nv != null && (
				(nv.inputPortViews  != null && nv.inputPortViews.Count  > 0) ||
				(nv.outputPortViews != null && nv.outputPortViews.Count > 0)));

			if (anyViewPort)
				return; // 正常态，不闪屏

			// 数据已就绪但视图缺端口 → 编译重载裂缝：先持久化再重建，防止重建中丢数据
			graphView.SaveGraphToDisk();
			rootView.Remove(graphView);
			graphView = null;
			reloadWorkaround = true; // 触发 Update 中后续 LoadGraph 重建
		}

		void LoadGraph()
		{
            // We wait for the graph to be initialized
            if (graph.isEnabled)
                InitializeGraph(graph);
            else
                graph.onEnabled += () => InitializeGraph(graph);
		}

		/// <summary>
		/// Called by Unity when the window is disabled (happens on domain reload)
		/// </summary>
		protected virtual void OnDisable()
		{
			if (graph != null && graphView != null)
				graphView.SaveGraphToDisk();
		}
		
		/// <summary>
		/// Called by Unity when the window is closed
		/// </summary>
		protected virtual void OnDestroy() { }

		void InitializeRootView()
		{
			rootView = base.rootVisualElement;

			rootView.name = "graphRootView";

			rootView.styleSheets.Add(Resources.Load<StyleSheet>(graphWindowStyle));
		}

		public void InitializeGraph(BaseGraph graph)
		{
			if (this.graph != null && graph != this.graph)
			{
				// Save the graph to the disk
				EditorUtility.SetDirty(this.graph);
				AssetDatabase.SaveAssets();
				// Unload the graph
				graphUnloaded?.Invoke(this.graph);
			}

			graphLoaded?.Invoke(graph);
			this.graph = graph;

			if (graphView != null)
				rootView.Remove(graphView);

			//Initialize will provide the BaseGraphView
			InitializeWindow(graph);

			graphView = rootView.Children().FirstOrDefault(e => e is BaseGraphView) as BaseGraphView;

			if (graphView == null)
			{
				Debug.LogError("GraphView has not been added to the BaseGraph root view !");
				return ;
			}

			graphView.Initialize(graph);

			InitializeGraphView(graphView);

			// TOOD: onSceneLinked...

			if (graph.IsLinkedToScene())
				LinkGraphWindowToScene(graph.GetLinkedScene());
			else
				graph.onSceneLinked += LinkGraphWindowToScene;
		}

		void LinkGraphWindowToScene(Scene scene)
		{
			EditorSceneManager.sceneClosed += CloseWindowWhenSceneIsClosed;

			void CloseWindowWhenSceneIsClosed(Scene closedScene)
			{
				if (scene == closedScene)
				{
					Close();
					EditorSceneManager.sceneClosed -= CloseWindowWhenSceneIsClosed;
				}
			}
		}

		public virtual void OnGraphDeleted()
		{
			if (graph != null && graphView != null)
				rootView.Remove(graphView);

			graphView = null;
		}

		protected abstract void	InitializeWindow(BaseGraph graph);
		protected virtual void InitializeGraphView(BaseGraphView view) {}
	}
}