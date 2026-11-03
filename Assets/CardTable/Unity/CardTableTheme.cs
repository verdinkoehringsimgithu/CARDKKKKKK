using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardKKK.Tabletop.Unity
{
    [CreateAssetMenu(menuName = "CardKKK/Card Table Theme", fileName = "CardTableTheme")]
    public sealed class CardTableTheme : ScriptableObject
    {
        [Serializable]
        public sealed class CardFace
        {
            public string DefinitionId;
            public Sprite Sprite;
        }

        public Sprite TableSurface;
        public Sprite CardFrame;
        public Sprite CardBack;
        public Sprite ActionIcon;
        public List<CardFace> CardFaces = new List<CardFace>();
        public Color TableColor = new Color(0.055f, 0.13f, 0.17f);
        public Color CardColor = new Color(0.88f, 0.89f, 0.83f);
        public Color BackColor = new Color(0.18f, 0.27f, 0.35f);
        [Range(0.05f, 0.6f)] public float TransitionSeconds = 0.18f;
        public AudioClip PickUpSound;
        public AudioClip DropSound;
        public AudioClip RejectedSound;
        public AudioClip FlipSound;
        public AudioClip Music;

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
