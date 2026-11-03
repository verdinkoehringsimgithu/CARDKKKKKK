using System.Collections.Generic;
using System.Text;
using CardKKK.Tabletop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>已序列化的桌面布局、池化牌面和过渡动画；不制定游戏规则。</summary>
    public sealed class CardTableView : MonoBehaviour
    {
        [Header("桌面与牌面")]
        [SerializeField, InspectorName("桌面可用区域")] private RectTransform boardArea;
        [SerializeField, InspectorName("逻辑桌面")] private RectTransform board;
        [SerializeField, InspectorName("桌面图像")] private Image surface;
        [SerializeField, InspectorName("牌面层")] private RectTransform cardLayer;
        [SerializeField, InspectorName("拖动层")] private RectTransform dragLayer;
        [SerializeField, InspectorName("落点范围")] private Image preview;
        [SerializeField, InspectorName("插槽标记")] private Image insertion;
        [SerializeField, InspectorName("卡牌预制体")] private TableCardView cardPrefab;
        [Header("操作与提示")]
        [SerializeField, InspectorName("发牌按钮容器")] private RectTransform spawnButtons;
        [SerializeField, InspectorName("发牌按钮模板")] private TableActionButton spawnTemplate;
        [SerializeField, InspectorName("状态文字")] private Text status;
        [SerializeField, InspectorName("选中信息")] private Text selected;
        [SerializeField, InspectorName("操作帮助")] private GameObject helpPanel;
        [SerializeField, InspectorName("帮助文字")] private Text helpText;
        [SerializeField, InspectorName("音效播放器")] private AudioSource effects;
        [SerializeField, InspectorName("音乐播放器")] private AudioSource music;
        private readonly Dictionary<string, TableCardView> views = new Dictionary<string, TableCardView>();
        private readonly Dictionary<string, CardDefinition> definitions = new Dictionary<string, CardDefinition>();
        private readonly Queue<TableCardView> pool = new Queue<TableCardView>();
        private readonly List<string> retired = new List<string>();
        private readonly List<TableActionButton> bindings = new List<TableActionButton>();
        private readonly List<TableCardView> moving = new List<TableCardView>();
        private CardTableController controller;
        private BoardRules rules;
        private Font font;
        private Font ownedFont;
        private Vector2 pointerOffset;
        private bool dragVisuals;
        private float messageUntil;

        public bool IsAnimating
        {
            get { foreach (TableCardView card in views.Values) if (card.IsAnimating) return true; return false; }
        }

        public void Initialize(CardTableController value)
        {
            controller = value; rules = controller.Engine.Rules;
            CardTableTheme theme = controller.Config.Theme;
            font = theme != null ? theme.UiFont : null;
            if (font == null)
            {
                ownedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "SimSun" }, 16);
                font = ownedFont;
            }
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = font;
            foreach (CardDefinition definition in controller.Config.Cards) definitions.Add(definition.Id, definition);
            foreach (TableActionButton button in GetComponentsInChildren<TableActionButton>(true))
            {
                if (button == spawnTemplate) continue;
                button.Bind(controller); bindings.Add(button);
            }
            spawnTemplate.gameObject.SetActive(false);
            // 每个配置定义一个按钮；列表使用预制体中的纵向布局和滚动区域。
            foreach (CardDefinition definition in controller.Config.Cards)
            {
                TableActionButton button = Instantiate(spawnTemplate, spawnButtons);
                button.name = "生成_" + definition.DisplayName;
                button.Bind(controller, definition.Id, definition.DisplayName);
                bindings.Add(button);
            }
            if (theme != null)
            {
                surface.sprite = theme.TableSurface; surface.color = theme.TableColor;
                if (theme.Music != null) { music.clip = theme.Music; music.Play(); }
            }
            board.sizeDelta = new Vector2(rules.Width, rules.Height);
            FitBoard();
            controller.StateChanged += Render;
            controller.SelectionChanged += RefreshSelection;
            controller.DragCancelled += FinishDragVisuals;
            controller.Message += ShowMessage;
            controller.Sound += PlaySound;
            Render(true);
            if (helpText != null) helpText.text = BuildHelp();
            if (helpPanel != null) helpPanel.SetActive(false);
            ShowMessage("");
        }

        public void Unbind()
        {
            if (controller == null) return;
            controller.StateChanged -= Render;
            controller.SelectionChanged -= RefreshSelection;
            controller.DragCancelled -= FinishDragVisuals;
            controller.Message -= ShowMessage;
            controller.Sound -= PlaySound;
            foreach (TableActionButton button in bindings) if (button != null) button.Unbind();
            controller = null;
        }

        public void ShowMessage(string message)
        {
            if (status == null) return;
            status.text = message;
            status.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            messageUntil = Time.unscaledTime + 4;
        }
        public void ShowError(string message)
        {
            ShowMessage(message);
            messageUntil = float.PositiveInfinity;
        }
        public void ToggleHelp()
        {
            if (helpPanel == null) return;
            controller?.CancelDrag();
            helpPanel.SetActive(!helpPanel.activeSelf);
        }
        private string BuildHelp()
        {
            var text = new StringBuilder();
            if (rules.AllowWholePileDrag) text.AppendLine("移动整堆　拖动");
            if (rules.AllowSingleCardDrag) text.AppendLine("拆出一张　Shift + 拖动");
            if (rules.AllowWholePileDrag || rules.AllowSingleCardDrag) text.AppendLine("取消拖动　Esc");
            AppendHelp(text, TableAction.Flip, "翻面　空格");
            AppendHelp(text, TableAction.Sort, "排序　S");
            AppendHelp(text, TableAction.Shuffle, "洗牌　H");
            AppendHelp(text, TableAction.Pop, "抽牌　P");
            AppendHelp(text, TableAction.ReorderEarlier, "前后移动　左 / 右方向键");
            AppendHelp(text, TableAction.Tidy, "整理桌面　R");
            AppendHelp(text, TableAction.Save, "保存　F5");
            AppendHelp(text, TableAction.Load, "读取　F9");
            return text.ToString().TrimEnd();
        }
        private void AppendHelp(StringBuilder text, TableAction action, string line)
        { if (controller.IsAvailable(action)) text.AppendLine(line); }
        private void Update()
        {
            foreach (TableCardView card in views.Values) card.Animate(Time.unscaledDeltaTime);
            if (status != null && status.gameObject.activeSelf && Time.unscaledTime >= messageUntil)
                status.gameObject.SetActive(false);
            if (helpPanel != null && helpPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                helpPanel.SetActive(false);
        }
        private void OnRectTransformDimensionsChange() { if (rules != null) FitBoard(); }
        private void OnDestroy() { Unbind(); if (ownedFont != null) Destroy(ownedFont); }
        private void FitBoard()
        {
            float scale = Mathf.Min(boardArea.rect.width / rules.Width, boardArea.rect.height / rules.Height);
            board.localScale = new Vector3(scale, scale, 1);
            board.anchoredPosition = (boardArea.rect.size - new Vector2(rules.Width, rules.Height) * scale) * 0.5f;
        }
        private void Render() => Render(false);
        private void Render(bool immediate)
        {
            retired.Clear();
            foreach (string id in views.Keys) if (controller.FindCard(id) == null) retired.Add(id);
            foreach (string id in retired)
            {
                TableCardView card = views[id]; views.Remove(id); card.gameObject.SetActive(false); pool.Enqueue(card);
            }
            foreach (PileData pile in controller.Snapshot.Piles)
                for (int index = 0; index < pile.CardIds.Count; index++)
                {
                    CardData data = controller.FindCard(pile.CardIds[index]);
                    bool created = !views.TryGetValue(data.Id, out TableCardView card);
                    if (created)
                    {
                        card = pool.Count > 0 ? pool.Dequeue() : Instantiate(cardPrefab, cardLayer);
                        card.Initialize(this, new Vector2(rules.CardWidth, rules.CardHeight), font, controller.Config.Theme);
                        card.gameObject.SetActive(true); card.name = data.Label; views.Add(data.Id, card);
                    }
                    card.SetLayer(cardLayer);
                    Sprite artwork = definitions.TryGetValue(data.DefinitionId, out CardDefinition definition) ? definition.Artwork : null;
                    card.Bind(data, pile, new Vector2(pile.Position.X, pile.Position.Y + index * rules.PileOffset),
                        artwork, data.Id == controller.SelectedCardId, immediate);
                    if (created && !immediate) card.EnterFrom(new Vector2(rules.Width * 0.5f, rules.Height - rules.CardHeight));
                }
            dragVisuals = false; moving.Clear(); HidePreview(); RefreshSelection();
        }
        private void RefreshSelection()
        {
            foreach (TableCardView card in views.Values) card.SetSelected(card.CardId == controller.SelectedCardId);
            CardData data = controller.FindCard(controller.SelectedCardId);
            PileData pile = controller.FindPile(controller.SelectedPileId);
            selected.text = data == null ? "" : data.Label + "\n" + (pile != null ? pile.CardIds.Count : 0) + " 张";
        }
        public void Select(TableCardView card) => controller?.Select(card.CardId, card.PileId);
        public void BeginCardDrag(TableCardView card, PointerEventData data)
        {
            if (controller == null || !PointerPosition(data, out Vector2 pointer)) return;
            bool single = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            PileData pile = controller.FindPile(card.PileId);
            Vector2 origin = single ? card.Position : new Vector2(pile.Position.X, pile.Position.Y);
            if (!controller.BeginDrag(card.CardId, card.PileId, single)) return;
            pointerOffset = pointer - origin; moving.Clear();
            foreach (string id in controller.Engine.CurrentDrag.CardIds)
            { TableCardView view = views[id]; view.SetLayer(dragLayer); moving.Add(view); }
            dragVisuals = true; UpdateCardDrag(data);
        }
        public void UpdateCardDrag(PointerEventData data)
        {
            if (!dragVisuals || !PointerPosition(data, out Vector2 pointer)) return;
            Vector2 position = pointer - pointerOffset;
            for (int i = 0; i < moving.Count; i++) moving[i].SetPosition(position + Vector2.up * (rules.PileOffset * i), dragLayer);
            DropPreview plan = controller.Preview(position);
            PileData target = controller.FindPile(plan.TargetPileId);
            int targetCount = target != null ? target.CardIds.Count : 0;
            if (target != null) foreach (TableCardView card in moving) if (target.CardIds.Contains(card.CardId)) targetCount--;
            int total = targetCount + moving.Count;
            preview.rectTransform.anchoredPosition = new Vector2(plan.Position.X, plan.Position.Y);
            preview.rectTransform.sizeDelta = new Vector2(rules.CardWidth, rules.CardHeight + Mathf.Max(0, total - 1) * rules.PileOffset);
            preview.color = plan.IsValid ? new Color(0.2f, 0.9f, 0.65f, 0.3f) : new Color(1, 0.25f, 0.2f, 0.35f);
            preview.gameObject.SetActive(true);
            insertion.gameObject.SetActive(plan.IsValid && target != null);
            insertion.rectTransform.anchoredPosition = new Vector2(plan.Position.X - 4, plan.Position.Y + plan.InsertionIndex * rules.PileOffset);
            insertion.rectTransform.sizeDelta = new Vector2(rules.CardWidth + 8, 4);
            ShowMessage(plan.IsValid ? "" : plan.Reason);
        }
        public void EndCardDrag(PointerEventData data)
        {
            if (!dragVisuals) return;
            if (!PointerPosition(data, out Vector2 pointer)) { controller.CancelDrag(); return; }
            controller.CommitDrag(pointer - pointerOffset);
        }
        private void FinishDragVisuals()
        {
            // 成功提交由 StateChanged 唯一刷新；取消或非法落点没有提交事件，才归位。
            if (dragVisuals) Render();
            HidePreview();
        }
        private void HidePreview() { preview.gameObject.SetActive(false); insertion.gameObject.SetActive(false); }
        private bool PointerPosition(PointerEventData data, out Vector2 position)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(board, data.position, data.pressEventCamera, out position);
        private void PlaySound(TableSound sound)
        {
            CardTableTheme theme = controller.Config.Theme;
            if (theme == null || effects == null) return;
            AudioClip clip = null;
            switch (sound)
            {
                case TableSound.PickUp: clip = theme.PickUpSound; break;
                case TableSound.Drop: clip = theme.DropSound; break;
                case TableSound.Rejected: clip = theme.RejectedSound; break;
#if !CARDTABLE_DISABLE_FLIP
                case TableSound.Flip: clip = theme.FlipSound; break;
#endif
            }
            if (clip != null) effects.PlayOneShot(clip);
        }
    }
}
