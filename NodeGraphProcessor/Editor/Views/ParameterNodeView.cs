using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;
using System.Linq;

[NodeCustomEditor(typeof(ParameterNode))]
public class ParameterNodeView : BaseNodeView
{
    ParameterNode parameterNode;
    VisualElement _originalTopParent;

    public override void Enable(bool fromInspector = false)
    {
        parameterNode = nodeTarget as ParameterNode;

        EnumField accessorSelector = new EnumField(parameterNode.accessor);
        accessorSelector.SetValueWithoutNotify(parameterNode.accessor);
        accessorSelector.RegisterValueChangedCallback(evt =>
        {
            parameterNode.accessor = (ParameterAccessor)evt.newValue;
            UpdatePortLayout();
            controlsContainer.MarkDirtyRepaint();
            ForceUpdatePorts();
        });
        
        controlsContainer.Add(accessorSelector);
        
        // 移除展开/折叠按钮
        titleContainer.Remove(titleContainer.Q("title-button-container"));

        // 保存 topContainer 的原始父容器（nodeBorderContainer），供 Set 模式恢复使用
        _originalTopParent = topContainer.parent;

        // 默认 Get 模式：将端口移到标题栏（紧凑内联样式）
        topContainer.parent?.Remove(topContainer);
        titleContainer.Add(topContainer);

        UpdatePortLayout();

        parameterNode.onParameterChanged += UpdateView;
        UpdateView();
    }

    void UpdateView()
    {
        title = parameterNode.parameter?.name;
    }
    
    /// <summary>
    /// Get 模式（单端口）：端口在标题栏内，紧凑内联。
    /// Set 模式（多端口含 exec）：端口回到节点主体，输入左 / 输出右自然排布。
    /// </summary>
    void UpdatePortLayout()
    {
        if (parameterNode.accessor == ParameterAccessor.Set)
        {
            titleContainer.AddToClassList("input");
            // 将端口区域移回原始父容器，使 exec 端口正确显示（输入左，输出右）
            if (topContainer.parent == titleContainer && _originalTopParent != null)
            {
                topContainer.parent.Remove(topContainer);
                _originalTopParent.Add(topContainer);
            }
        }
        else
        {
            titleContainer.RemoveFromClassList("input");
            // 将端口区域移入标题栏，单端口紧凑显示
            if (topContainer.parent != titleContainer)
            {
                topContainer.parent?.Remove(topContainer);
                titleContainer.Add(topContainer);
            }
        }
    }
}
