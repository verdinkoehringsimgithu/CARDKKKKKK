using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>运行与编辑预览共用贴图设置，带边框的 Sprite 按九宫格显示。</summary>
    public static class CardTableArt
    {
        public static void ApplySprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero
                ? Image.Type.Sliced : Image.Type.Simple;
        }

        public static void ApplySurface(Image image, CardTableTheme theme)
        {
            if (theme == null) return;
            ApplySprite(image, theme.TableSurface);
            image.color = theme.TableColor;
        }
    }
}
