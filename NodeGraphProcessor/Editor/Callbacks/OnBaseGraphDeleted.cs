using UnityEngine;
using UnityEditor;

namespace GraphProcessor
{
	[ExecuteAlways]
	public class DeleteCallback : UnityEditor.AssetModificationProcessor
	{
		static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
		{
			var objects = AssetDatabase.LoadAllAssetsAtPath(path);

			foreach (var obj in objects)
			{
				if (obj is BaseGraph b)
				{
					// 仅对被删除图对应的窗口清除视图。
					// 旧实现会遍历所有 BaseGraphWindow 无差别调用 OnGraphDeleted()，
					// 导致"删除一张图 → 所有打开的节点图窗口全部清空（节点/分组/便签/参数面板/顶部按钮行消失）"。
					foreach (var graphWindow in Resources.FindObjectsOfTypeAll< BaseGraphWindow >())
					{
						if (graphWindow.graph == b)
							graphWindow.OnGraphDeleted();
					}

					b.OnAssetDeleted();
				}
			}

			return AssetDeleteResult.DidNotDelete;
		}
	}
}
