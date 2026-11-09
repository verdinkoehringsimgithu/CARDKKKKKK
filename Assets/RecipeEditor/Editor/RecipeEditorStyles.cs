using System;
using UnityEditor;
using UnityEngine;

namespace CardKKK.RecipeEditor.Editor
{
    /// <summary>
    /// 配方编辑器专用的 UI 样式与绘制辅助库。
    /// </summary>
    public static class RecipeEditorStyles
    {
        private static GUIStyle _cardBox;
        public static GUIStyle CardBox
        {
            get
            {
                if (_cardBox == null)
                {
                    _cardBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(10, 10, 8, 8),
                        margin = new RectOffset(4, 4, 4, 6)
                    };
                }
                return _cardBox;
            }
        }

        private static GUIStyle _headerLabel;
        public static GUIStyle HeaderLabel
        {
            get
            {
                if (_headerLabel == null)
                {
                    _headerLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 13,
                        margin = new RectOffset(0, 0, 2, 4)
                    };
                }
                return _headerLabel;
            }
        }

        private static GUIStyle _formulaBox;
        public static GUIStyle FormulaBox
        {
            get
            {
                if (_formulaBox == null)
                {
                    _formulaBox = new GUIStyle(EditorStyles.textArea)
                    {
                        fontSize = 12,
                        wordWrap = true,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(8, 8, 8, 8)
                    };
                }
                return _formulaBox;
            }
        }

        private static GUIStyle _tagPill;
        public static GUIStyle TagPill
        {
            get
            {
                if (_tagPill == null)
                {
                    _tagPill = new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(6, 6, 2, 2)
                    };
                }
                return _tagPill;
            }
        }

        public static void DrawSectionHeader(string title, string subtitle = "")
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, HeaderLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
            }
            DrawSeparator();
        }

        public static void DrawSeparator(int height = 1)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height + 4);
            rect.height = height;
            rect.y += 2;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.25f));
        }

        public static bool DrawTagBadge(string tagName, string colorHex, bool removable, out bool removeClicked)
        {
            removeClicked = false;
            Color baseColor;
            if (!ColorUtility.TryParseHtmlString(colorHex, out baseColor))
            {
                baseColor = new Color(0.2f, 0.6f, 0.9f);
            }

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = baseColor;

            string labelText = removable ? $"{tagName} ×" : tagName;
            bool clicked = GUILayout.Button(labelText, TagPill, GUILayout.ExpandWidth(false));
            if (clicked && removable)
            {
                removeClicked = true;
            }

            GUI.backgroundColor = prevBg;
            return clicked;
        }

        public static void DrawBadge(string text, Color color)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, EditorStyles.miniButton, GUILayout.ExpandWidth(false));
            GUI.backgroundColor = prevBg;
        }
    }
}
