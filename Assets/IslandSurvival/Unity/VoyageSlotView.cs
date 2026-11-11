using CardKKK.IslandSurvival.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed class VoyageSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Text label;
        private VoyageView owner;
        public int Location { get; private set; }
        public int Index { get; private set; }
        public int CardId { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Display(VoyageView view, VoyageSlotOffer slot)
        {
            owner = view;
            Location = slot.Location;
            Index = slot.Index;
            CardId = slot.CardId;
            label.text = slot.Locked ? "进行中" : slot.CardId == 0 ? "+" : "";
            frame.color = slot.Locked ? new Color(.40f, .43f, .42f, .65f) : new Color(.72f, .76f, .67f, .75f);
        }

        public void Guide(bool acceptable, bool selected)
        {
            if (acceptable) frame.color = new Color(.46f, 1f, .16f, 1f);
            else if (selected) frame.color = new Color(.98f, .81f, .48f, 1f);
        }

        public void OnPointerClick(PointerEventData pointer)
        {
            if (pointer.button == PointerEventData.InputButton.Left) owner?.SelectSlot(Index);
        }
    }
}
