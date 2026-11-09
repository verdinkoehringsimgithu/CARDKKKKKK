using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class VoyageTaskView : MonoBehaviour
    {
        [SerializeField] private Text title, timer, inputs;
        [SerializeField] private Button cancel;
        [SerializeField] private GameObject sanVial, hungerVial;
        [SerializeField] private RectTransform fill, sanFill, hungerFill;
        public RectTransform Rect => (RectTransform)transform;

        public void Bind(Action cancelTask) => cancel.onClick.AddListener(() => cancelTask());

        public void Display(string recipe, string heldCards, int remaining, int duration, bool paused, bool canCancel, float san, float hunger)
        {
            title.text = recipe;
            timer.text = (paused ? "暂停 · " : "") + remaining + " 分钟";
            inputs.text = heldCards;
            cancel.interactable = canCancel;
            fill.anchorMax = new Vector2(Mathf.Clamp01(1 - (float)remaining / Mathf.Max(1, duration)), 1);
            DisplayVial(sanVial, sanFill, san);
            DisplayVial(hungerVial, hungerFill, hunger);
        }

        private static void DisplayVial(GameObject vial, RectTransform fill, float level)
        {
            vial.SetActive(level >= 0);
            if (level >= 0) fill.anchorMax = new Vector2(1, Mathf.Clamp01(level));
        }
    }
}
