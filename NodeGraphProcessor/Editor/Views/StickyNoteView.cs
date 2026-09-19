#if UNITY_2020_1_OR_NEWER
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GraphProcessor
{
    public class StickyNoteView : UnityEditor.Experimental.GraphView.StickyNote
	{
		public BaseGraphView	owner;
		public StickyNote		note;

        Label                   titleLabel;
        ColorField              colorField;

        public StickyNoteView()
        {
            fontSize = StickyNoteFontSize.Small;
            theme = StickyNoteTheme.Classic;
		}

		public void Initialize(BaseGraphView graphView, StickyNote note)
		{
			this.note = note;
			owner = graphView;

            // 读取存量时先清洗，避免已有中文双引号的便签在进入编辑时触发 UIToolkit 字体回退死循环
            note.title = RemoveCjkQuotes(note.title);
            note.content = RemoveCjkQuotes(note.content);
#if UNITY_6000_3_OR_NEWER
            // 6.3 起需先修正输入框层级，否则双击无法进入内容编辑
            FixContentsFieldHierarchy();
#endif
            this.Q<TextField>("title-field").RegisterCallback<ChangeEvent<string>>(e => {
                note.title = RemoveCjkQuotes(e.newValue);
            });
            this.Q<TextField>("contents-field").RegisterCallback<ChangeEvent<string>>(e => {
                note.content = RemoveCjkQuotes(e.newValue);
            });
        
            title = note.title;
            contents = note.content;
            SetPosition(note.position);
		}

#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// 规避 Unity 6.3 的便签内容编辑回归（UUM-133754，官方 6000.5.0a7 才修复）：
        /// 6.3 的双击逻辑会把 Label#contents 置为 display:none，而内置 UXML 中
        /// contents-field 是该 Label 的子节点，会随父级一起被隐藏，导致输入框刚获焦
        /// 就失焦，表现为“双击便签无法进入文本编辑”。把输入框提升为 Label 的兄弟
        /// 节点，使其不再受父级 display 影响。若版本已改为扁平结构则自动跳过。
        /// </summary>
        void FixContentsFieldHierarchy()
        {
            var label = this.Q<Label>("contents");
            var field = this.Q<TextField>("contents-field");

            if (label == null || field == null || label.parent == null || field.parent != label)
                return;

            var parent = label.parent;
            int index = parent.IndexOf(label);

            field.RemoveFromHierarchy();
            parent.Insert(index + 1, field);

            field.style.flexGrow = 1;    // 脱离 Label 后需自行填满内容区
            field.style.flexShrink = 1;
        }
#endif

        /// <summary>
        /// 移除中文全角双引号（“”）。全角引号并非默认字体原生 glyph，双击编辑时
        /// 触发 UIToolkit 文本整形器的字体回退路径，会导致编辑器 Main 线程卡死（Hold on）。
        /// 便签文本不要求准确性，直接移除即可规避。
        /// </summary>
        static string RemoveCjkQuotes(string s)
		{
			if (string.IsNullOrEmpty(s)) return s;
			return s.Replace("\u201C", "").Replace("\u201D", ""); // “ →
        }

		public override void SetPosition(Rect newPos)
		{
			base.SetPosition(newPos);

            if (note != null)
                note.position = newPos;
		}

        public override void OnResized()
        {
            note.position = layout;
        }
	}
}
#endif
