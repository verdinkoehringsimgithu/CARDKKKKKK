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

        private Text cancelLabel;
        private Action onCancel, onCollect;
        private bool completed;

        private void EnsureLabel()
        {
            if (cancelLabel == null && cancel != null) cancelLabel = cancel.GetComponentInChildren<Text>();
        }

        public void Bind(Action cancelTask, Action collectTask = null)
        {
            EnsureLabel();
            onCancel = cancelTask;
            onCollect = collectTask ?? cancelTask;
            cancel.onClick.RemoveAllListeners();
            cancel.onClick.AddListener(() =>
            {
                if (completed) onCollect?.Invoke();
                else onCancel?.Invoke();
            });
        }

        public void Display(string recipe, string heldCards, int remaining, int duration, bool paused, bool canCancel, float san, float hunger, bool isCompleted = false, int stage = 1, int totalStages = 1)
        {
            EnsureLabel();
            completed = isCompleted || remaining <= 0;
            string stageSuffix = totalStages > 1 ? $" (阶段 {stage}/{totalStages})" : "";
            title.text = recipe + stageSuffix + (completed ? " · [完成]" : "");
            timer.text = completed ? "已完成 · 点击收取" : (paused ? "暂停 · " : "") + remaining + " 分钟";
            inputs.text = heldCards;
            if (cancelLabel != null) cancelLabel.text = completed ? "收取" : "取消";
            cancel.interactable = completed || canCancel;
            fill.anchorMax = new Vector2(completed ? 1f : Mathf.Clamp01(1 - (float)remaining / Mathf.Max(1, duration)), 1);
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
