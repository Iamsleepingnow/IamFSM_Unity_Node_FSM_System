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