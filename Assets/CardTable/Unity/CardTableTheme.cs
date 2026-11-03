using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    [CreateAssetMenu(menuName = "卡牌桌面/美术主题", fileName = "CardTableTheme")]
    public sealed class CardTableTheme : ScriptableObject
    {
        [Serializable]
        public sealed class CardFace
        {
            [InspectorName("卡牌定义 ID")] public string DefinitionId;
            [InspectorName("卡面插画")] public Sprite Sprite;
        }

        [Header("字体与图像")]
        [InspectorName("界面字体")] public Font UiFont;
        [InspectorName("桌面背景")] public Sprite TableSurface;
        [InspectorName("通用卡框")] public Sprite CardFrame;
        [InspectorName("卡牌背面")] public Sprite CardBack;
        [InspectorName("操作图标（预留）")] public Sprite ActionIcon;
        [InspectorName("卡面插画映射")] public List<CardFace> CardFaces = new List<CardFace>();

        [Header("占位颜色与过渡")]
        [InspectorName("桌面颜色")] public Color TableColor = new Color(0.055f, 0.13f, 0.17f);
        [InspectorName("卡面颜色")] public Color CardColor = new Color(0.88f, 0.89f, 0.83f);
        [InspectorName("卡背颜色")] public Color BackColor = new Color(0.18f, 0.27f, 0.35f);
        [InspectorName("过渡时长（秒）"), Range(0.05f, 0.6f)] public float TransitionSeconds = 0.18f;

        [Header("音效与音乐")]
        [InspectorName("拾起音效")] public AudioClip PickUpSound;
        [InspectorName("落牌音效")] public AudioClip DropSound;
        [InspectorName("拒绝落点音效")] public AudioClip RejectedSound;
        [InspectorName("翻面音效")] public AudioClip FlipSound;
        [InspectorName("背景音乐")] public AudioClip Music;

        public Sprite FindFace(string definitionId)
        {
            foreach (CardFace face in CardFaces)
            {
                if (face != null && face.DefinitionId == definitionId)
                    return face.Sprite;
            }
            return null;
        }
    }
}
