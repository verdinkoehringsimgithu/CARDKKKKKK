using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class SurvivalChoiceView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text titleText, detailText;
        public void Bind(string title, string detail, Action selected, bool enabled = true)
        {
            titleText.text = title; detailText.text = detail;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => selected?.Invoke());
            button.interactable = enabled;
        }
        public void Tint(Color color) { if (button.targetGraphic != null) button.targetGraphic.color = color; }
    }
}
