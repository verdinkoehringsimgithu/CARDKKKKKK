using System;
using System.Collections.Generic;
using System.Text;
using CardKKK.TestGameplay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.TestGameplay.Unity
{
    /// <summary>独立工坊界面。数据从核心快照读取，所有制作意图交回场景入口。</summary>
    public sealed class WorkshopView : MonoBehaviour, IPointerClickHandler
    {
        [Header("桌面")]
        [SerializeField, InspectorName("画布")] private Canvas canvas;
        [SerializeField, InspectorName("背景")] private Image background;
        [SerializeField, InspectorName("对象区域")] private RectTransform boardArea;
        [SerializeField, InspectorName("地点卡")] private WorkshopCardView[] siteCards;
        [SerializeField, InspectorName("工位卡")] private WorkshopCardView[] facilityCards;
        [SerializeField, InspectorName("库存容器")] private RectTransform inventoryContent;
        [SerializeField, InspectorName("库存网格")] private GridLayoutGroup inventoryGrid;
        [SerializeField, InspectorName("物品牌模板")] private WorkshopCardView itemTemplate;
        [Header("详情")]
        [SerializeField, InspectorName("详情面板")] private GameObject detailsPanel;
        [SerializeField, InspectorName("详情标题")] private Text detailsTitle;
        [SerializeField, InspectorName("详情状态")] private Text detailsStatus;
        [SerializeField, InspectorName("工艺容器")] private RectTransform recipeContent;
        [SerializeField, InspectorName("工艺模板")] private WorkshopRecipeView recipeTemplate;
        [SerializeField, InspectorName("关闭详情")] private Button closeDetails;
        [Header("操作与状态")]
        [SerializeField, InspectorName("时间")] private Text clockText;
        [SerializeField, InspectorName("目标")] private Text goalText;
        [SerializeField, InspectorName("反馈")] private Text messageText;
        [SerializeField, InspectorName("反馈面板")] private GameObject messagePanel;
        [SerializeField, InspectorName("返回原牌桌")] private Button returnButton;
        [SerializeField, InspectorName("保存")] private Button saveButton;
        [SerializeField, InspectorName("读取")] private Button loadButton;
        [SerializeField, InspectorName("重新开始")] private Button restartButton;
        [SerializeField, InspectorName("等待加工")] private Button waitButton;
        [SerializeField, InspectorName("拖动层")] private RectTransform dragLayer;
        private WorkshopEngine engine;
        private WorkshopConfiguration configuration;
        private WorkshopDefinition definition;
        private WorkshopSnapshot snapshot;
        private readonly Dictionary<string, string> itemNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorkshopCardView> inventoryCards = new Dictionary<string, WorkshopCardView>(StringComparer.Ordinal);
        private readonly List<WorkshopRecipeView> recipeRows = new List<WorkshopRecipeView>();
        private readonly Dictionary<string, RecipeDefinitionData> recipes = new Dictionary<string, RecipeDefinitionData>(StringComparer.Ordinal);
        private Font font, ownedFont;
        private string selectedId, filterItemId, dragItemId, renderedScenarioId;
        private WorkshopCardKind selectedKind;
        private WorkshopCardView dragGhost, dragOrigin;
        private bool dropAccepted, returningGhost, buttonsBound;
        private Vector2 ghostReturnTarget;
        private float messageUntil;

        public event Action ReturnRequested, SaveRequested, LoadRequested, RestartRequested, WaitRequested;
        public event Action<string> RecipeRequested, GatherRequested;
        public bool IsDraggingItem => !string.IsNullOrEmpty(dragItemId);

        private void Awake() => BindButtons();

        public void Initialize(WorkshopEngine value, WorkshopConfiguration settings)
        {
            Unbind();
            engine = value ?? throw new ArgumentNullException(nameof(value));
            configuration = settings ?? throw new ArgumentNullException(nameof(settings));
            definition = engine.Definition;
            snapshot = engine.Snapshot;
            itemNames.Clear(); recipes.Clear();
            foreach (ItemDefinitionData item in definition.Items) itemNames[item.Id] = item.Name;
            foreach (RecipeDefinitionData recipe in definition.Recipes) recipes[recipe.Id] = recipe;
            font = settings.UiFont;
            if (font == null)
            {
                if (ownedFont == null)
                    ownedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "SimSun" }, 22);
                font = ownedFont;
            }
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = font;
            detailsStatus.color = settings.MutedTextColor;
            background.sprite = settings.TableSurface;
            background.type = background.sprite != null && background.sprite.border.sqrMagnitude > 0
                ? Image.Type.Sliced : Image.Type.Simple;
            background.color = background.sprite != null ? Color.white : settings.BackgroundColor;
            itemTemplate.gameObject.SetActive(false);
            recipeTemplate.gameObject.SetActive(false);
            foreach (WorkshopCardView card in siteCards) card.Initialize(this, settings, font);
            foreach (WorkshopCardView card in facilityCards) card.Initialize(this, settings, font);
            inventoryCards.Clear();
            foreach (WorkshopCardView card in inventoryContent.GetComponentsInChildren<WorkshopCardView>(true))
            {
                if (string.IsNullOrEmpty(card.ObjectId)) continue;
                if (inventoryCards.ContainsKey(card.ObjectId))
                    throw new InvalidOperationException("库存界面重复绑定同一物品。");
                inventoryCards.Add(card.ObjectId, card);
                card.Initialize(this, settings, font);
                card.gameObject.SetActive(false);
            }
            recipeRows.Clear();
            foreach (WorkshopRecipeView row in recipeContent.GetComponentsInChildren<WorkshopRecipeView>(true))
            {
                row.Initialize(font);
                row.gameObject.SetActive(false);
                recipeRows.Add(row);
            }
            if (dragGhost == null)
            {
                dragGhost = Instantiate(itemTemplate, dragLayer);
                dragGhost.name = "拖动预览";
                CanvasGroup group = dragGhost.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false; group.interactable = false;
            }
            dragGhost.Initialize(this, settings, font);
            dragGhost.gameObject.SetActive(false);
            BindButtons();
            engine.Changed += StateChanged;
            renderedScenarioId = snapshot.ScenarioId;
            selectedId = "kiln"; selectedKind = WorkshopCardKind.Facility; filterItemId = null;
            ShowMessage("");
            Render();
        }

        private void BindButtons()
        {
            if (buttonsBound) return;
            buttonsBound = true;
            returnButton.onClick.AddListener(RequestReturn);
            saveButton.onClick.AddListener(RequestSave);
            loadButton.onClick.AddListener(RequestLoad);
            restartButton.onClick.AddListener(RequestRestart);
            waitButton.onClick.AddListener(RequestWait);
            closeDetails.onClick.AddListener(Deselect);
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void RequestReturn() { CancelDrag(); ReturnRequested?.Invoke(); }
        private void RequestSave() { CancelDrag(); SaveRequested?.Invoke(); }
        private void RequestLoad() { CancelDrag(); LoadRequested?.Invoke(); }
        private void RequestRestart() { CancelDrag(); RestartRequested?.Invoke(); }
        private void RequestWait() { CancelDrag(); WaitRequested?.Invoke(); }

        public void Unbind()
        {
            if (engine != null) engine.Changed -= StateChanged;
            CancelDrag();
            engine = null;
        }

        private void StateChanged(WorkshopSnapshot state)
        {
            CancelDrag();
            Render();
        }

        public void Render()
        {
            if (engine == null) return;
            snapshot = engine.Snapshot;
            bool immediate = renderedScenarioId != snapshot.ScenarioId || snapshot.Revision == 0;
            if (renderedScenarioId != snapshot.ScenarioId)
            {
                selectedId = "kiln"; selectedKind = WorkshopCardKind.Facility; filterItemId = null;
                renderedScenarioId = snapshot.ScenarioId;
            }
            clockText.text = "用时 " + FormatMinutes(snapshot.Minutes);
            ScenarioData scenario = CurrentScenario();
            goalText.text = snapshot.GoalComplete
                ? "已完成 · 用时 " + FormatMinutes(snapshot.CompletionMinutes) + " · 用木 " + snapshot.CompletionWoodSpent
                : "目标：" + Name(scenario.GoalItemId) + " " + scenario.GoalQuantity
                    + (scenario.RequireAxeGather ? " · 持斧取木一次" : "");
            waitButton.interactable = snapshot.Jobs.Count > 0;
            RenderFixedCards(immediate);
            foreach (ItemDefinitionData item in definition.Items)
            {
                int count = Count(item.Id);
                if (!inventoryCards.TryGetValue(item.Id, out WorkshopCardView card) && count > 0)
                {
                    card = Instantiate(itemTemplate, inventoryContent);
                    card.name = item.Name;
                    card.Initialize(this, configuration, font);
                    inventoryCards.Add(item.Id, card);
                }
                if (card == null) continue;
                card.gameObject.SetActive(count > 0);
                card.Bind(item.Id, WorkshopCardKind.Item, item.Name, count, "", -1,
                    selectedKind == WorkshopCardKind.Item && selectedId == item.Id, !immediate);
            }
            RenderDetails();
            FitInventory();
        }

        private void RenderFixedCards(bool immediate)
        {
            string[] sites = { "forest", "riverbank", "mine" };
            for (int i = 0; i < siteCards.Length && i < sites.Length; i++)
            {
                string id = sites[i];
                string status = "";
                foreach (GatherDefinitionData gather in definition.Gathers)
                    if (gather.SiteId == id) { status = id == "riverbank" ? "黏土 / 纤维" : Amounts(gather.Outputs) + " · " + GatherMinutes(gather) + "分"; break; }
                siteCards[i].Bind(id, WorkshopCardKind.Site, configuration.DisplayNameFor(id), 0,
                    status, -1, selectedKind == WorkshopCardKind.Site && selectedId == id, false);
            }
            string[] facilities = { "workbench", "kiln" };
            for (int i = 0; i < facilityCards.Length && i < facilities.Length; i++)
            {
                string id = facilities[i], status = id == "kiln" && snapshot.KilnLined ? "空闲 · 已装炉衬" : "空闲";
                float progress = -1;
                WorkshopJob job = JobAt(id);
                if (job != null && recipes.TryGetValue(job.RecipeId, out RecipeDefinitionData recipe))
                {
                    status = recipe.Name + "\n还需 " + (job.FinishMinute - snapshot.Minutes) + " 分钟";
                    progress = (snapshot.Minutes - job.StartedMinute) / (float)Math.Max(1, job.FinishMinute - job.StartedMinute);
                }
                facilityCards[i].Bind(id, WorkshopCardKind.Facility, configuration.DisplayNameFor(id), 0,
                    status, progress, selectedKind == WorkshopCardKind.Facility && selectedId == id, !immediate);
            }
        }

        public void SelectObject(string id, WorkshopCardKind kind)
        {
            selectedId = id; selectedKind = kind; filterItemId = null;
            Render();
        }

        private void RenderDetails()
        {
            bool visible = !string.IsNullOrEmpty(selectedId);
            detailsPanel.SetActive(visible);
            boardArea.offsetMax = new Vector2(visible ? -560 : -40, boardArea.offsetMax.y);
            if (!visible) { HideRows(0); return; }
            detailsTitle.text = configuration.DisplayNameFor(selectedId);
            detailsStatus.text = selectedKind == WorkshopCardKind.Item ? "库存 " + Count(selectedId)
                : selectedKind == WorkshopCardKind.Site ? "采集"
                : JobAt(selectedId) != null ? "正在加工 · 还需 " + (JobAt(selectedId).FinishMinute - snapshot.Minutes) + " 分钟"
                : selectedId == "kiln" && snapshot.KilnLined ? "空闲 · 已装炉衬" : "空闲";
            if (!string.IsNullOrEmpty(filterItemId)) detailsStatus.text += " · " + Name(filterItemId);
            int used = 0;
            if (selectedKind == WorkshopCardKind.Site)
            {
                foreach (GatherDefinitionData gather in definition.Gathers)
                {
                    if (gather.SiteId != selectedId) continue;
                    string capturedId = gather.Id;
                    GetRow(used++).Bind(gather.Name, "得到 " + Amounts(gather.Outputs),
                        "人物 " + GatherMinutes(gather) + " 分钟", "", gather.Name, true,
                        FirstArtwork(gather.Outputs), () => GatherRequested?.Invoke(capturedId));
                }
            }
            else
            {
                foreach (RecipeDefinitionData recipe in definition.Recipes)
                {
                    if (!Available(recipe.Id)) continue;
                    bool source = false;
                    if (selectedKind == WorkshopCardKind.Facility)
                    {
                        if (recipe.FacilityId != selectedId) continue;
                        if (!string.IsNullOrEmpty(filterItemId) && !Has(recipe.Inputs, filterItemId) && recipe.RequiredToolId != filterItemId) continue;
                    }
                    else
                    {
                        bool use = Has(recipe.Inputs, selectedId) || recipe.RequiredToolId == selectedId;
                        source = Has(recipe.Outputs, selectedId) || Has(recipe.Byproducts, selectedId);
                        if (!use && !source) continue;
                    }
                    RecipeOffer offer = engine.GetOffer(recipe.Id);
                    string details = "投入 " + Amounts(offer.EffectiveInputs) + "\n";
                    details += recipe.InstallLining ? "改善土窑，普通熔铜更省炭"
                        : "得到 " + Amounts(recipe.Outputs);
                    if (recipe.Byproducts.Count > 0) details += "\n副产 " + Amounts(recipe.Byproducts);
                    if (!string.IsNullOrEmpty(recipe.RequiredToolId)) details += "\n工具 " + Name(recipe.RequiredToolId) + "（保留）";
                    string timing = "准备 " + offer.PreparationMinutes + " 分钟";
                    timing += offer.ProcessMinutes > 0 ? " · 后台 " + offer.ProcessMinutes + " 分钟" : " · 完成即得";
                    string capturedId = recipe.Id;
                    GetRow(used++).Bind((selectedKind == WorkshopCardKind.Item ? (source ? "来源 · " : "用途 · ") : "") + recipe.Name,
                        details, timing, offer.CanStart ? "" : offer.Reason,
                        offer.CanStart ? "开始 · " + offer.PreparationMinutes + "分" : offer.IsKnown ? "暂不能开始" : "尚未认识",
                        offer.CanStart, recipe.InstallLining ? configuration.ArtworkFor("kiln") : FirstArtwork(recipe.Outputs),
                        () => RecipeRequested?.Invoke(capturedId));
                }
            }
            HideRows(used);
        }

        private WorkshopRecipeView GetRow(int index)
        {
            while (recipeRows.Count <= index)
            {
                WorkshopRecipeView row = Instantiate(recipeTemplate, recipeContent);
                row.name = "工艺选项";
                row.Initialize(font);
                recipeRows.Add(row);
            }
            recipeRows[index].gameObject.SetActive(true);
            return recipeRows[index];
        }
        private void HideRows(int used)
        { for (int i = used; i < recipeRows.Count; i++) recipeRows[i].gameObject.SetActive(false); }

        public void ShowMessage(string message, bool persistent = false)
        {
            messageText.text = message ?? "";
            messagePanel.SetActive(!string.IsNullOrEmpty(message));
            messageUntil = persistent ? float.PositiveInfinity : Time.unscaledTime + 4f;
        }

        public void BeginItemDrag(WorkshopCardView origin, PointerEventData data)
        {
            if (engine == null || origin.Kind != WorkshopCardKind.Item) return;
            CancelDrag();
            dragOrigin = origin; dragItemId = origin.ObjectId; dropAccepted = false;
            dragGhost.Bind(dragItemId, WorkshopCardKind.Item, Name(dragItemId), Count(dragItemId), "", -1, false, false);
            RectTransform rect = dragGhost.Rect;
            // 鼠标与归位坐标都相对拖动层pivot，锚点必须使用同一原点。
            rect.anchorMin = rect.anchorMax = dragLayer.pivot;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = origin.Rect.rect.size;
            dragGhost.gameObject.SetActive(true);
            rect.SetAsLastSibling();
            MoveItemDrag(data);
        }
        public void MoveItemDrag(PointerEventData data)
        {
            if (!IsDraggingItem) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, data.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 point))
                dragGhost.Rect.anchoredPosition = point;
        }
        public void DropOnFacility(string facilityId)
        {
            if (!IsDraggingItem) return;
            bool related = false;
            foreach (RecipeDefinitionData recipe in definition.Recipes)
                if (Available(recipe.Id) && recipe.FacilityId == facilityId
                    && (Has(recipe.Inputs, dragItemId) || recipe.RequiredToolId == dragItemId)) { related = true; break; }
            if (!related) { ShowMessage(Name(dragItemId) + "不能用于这里的工艺"); return; }
            selectedId = facilityId; selectedKind = WorkshopCardKind.Facility; filterItemId = dragItemId;
            dropAccepted = true; Render();
        }
        public void EndItemDrag(PointerEventData data)
        {
            if (!IsDraggingItem) return;
            if (dropAccepted) dragGhost.gameObject.SetActive(false);
            else
            {
                ghostReturnTarget = (Vector2)dragLayer.InverseTransformPoint(dragOrigin.Rect.TransformPoint(dragOrigin.Rect.rect.center));
                returningGhost = true;
            }
            dragItemId = null;
        }
        public void CancelDrag()
        {
            dragItemId = null; dragOrigin = null; returningGhost = false; dropAccepted = false;
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }
        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            foreach (WorkshopCardView card in siteCards) card.Animate(delta);
            foreach (WorkshopCardView card in facilityCards) card.Animate(delta);
            foreach (WorkshopCardView card in inventoryCards.Values) if (card.gameObject.activeSelf) card.Animate(delta);
            if (returningGhost && dragGhost != null)
            {
                RectTransform rect = dragGhost.Rect;
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, ghostReturnTarget, 1 - Mathf.Exp(-20 * delta));
                if ((rect.anchoredPosition - ghostReturnTarget).sqrMagnitude < 1)
                { returningGhost = false; dragGhost.gameObject.SetActive(false); }
            }
            if (messagePanel.activeSelf && Time.unscaledTime >= messageUntil) messagePanel.SetActive(false);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsDraggingItem || returningGhost) CancelDrag();
                else if (!string.IsNullOrEmpty(filterItemId)) { filterItemId = null; Render(); }
                else if (!string.IsNullOrEmpty(selectedId)) Deselect();
                else ReturnRequested?.Invoke();
            }
        }
        private void OnRectTransformDimensionsChange() { if (engine != null) FitInventory(); }
        private void FitInventory()
        {
            float width = inventoryContent.rect.width;
            int columns = Mathf.Max(1, Mathf.FloorToInt((width - inventoryGrid.padding.horizontal + inventoryGrid.spacing.x)
                / (inventoryGrid.cellSize.x + inventoryGrid.spacing.x)));
            if (inventoryGrid.constraintCount != columns) inventoryGrid.constraintCount = columns;
        }
        private void Deselect()
        {
            selectedId = filterItemId = null;
            if (engine != null) Render();
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (!data.dragging && data.button == PointerEventData.InputButton.Left
                && data.pointerCurrentRaycast.gameObject == background.gameObject) Deselect();
        }
        private void OnDisable() => CancelDrag();
        private void OnDestroy()
        {
            Unbind();
            if (ownedFont != null) Destroy(ownedFont);
        }

        private ScenarioData CurrentScenario()
        {
            foreach (ScenarioData scenario in definition.Scenarios) if (scenario.Id == snapshot.ScenarioId) return scenario;
            throw new InvalidOperationException("找不到当前开局");
        }
        private bool Available(string recipeId)
        { List<string> ids = CurrentScenario().AvailableRecipeIds; return ids.Count == 0 || ids.Contains(recipeId); }
        private int Count(string id)
        { foreach (ItemAmount amount in snapshot.Inventory) if (amount.ItemId == id) return amount.Quantity; return 0; }
        private int GatherMinutes(GatherDefinitionData gather)
        { return Count("copper_axe") > 0 && gather.MinutesWithAxe > 0 ? gather.MinutesWithAxe : gather.Minutes; }
        private WorkshopJob JobAt(string facility)
        { foreach (WorkshopJob job in snapshot.Jobs) if (job.FacilityId == facility) return job; return null; }
        private string Name(string id)
        { return itemNames.TryGetValue(id, out string value) ? value : configuration.DisplayNameFor(id); }
        private static bool Has(List<ItemAmount> amounts, string id)
        { foreach (ItemAmount amount in amounts) if (amount.ItemId == id) return true; return false; }
        private string Amounts(List<ItemAmount> amounts)
        {
            var value = new StringBuilder();
            foreach (ItemAmount amount in amounts)
            {
                if (value.Length > 0) value.Append("、");
                value.Append(Name(amount.ItemId)).Append(" ").Append(amount.Quantity);
            }
            return value.Length > 0 ? value.ToString() : "无";
        }
        private Sprite FirstArtwork(List<ItemAmount> amounts)
        { return amounts.Count > 0 ? configuration.ArtworkFor(amounts[0].ItemId) : null; }
        public static string FormatMinutes(int minutes)
        { return minutes >= 60 ? (minutes / 60) + "小时" + (minutes % 60 == 0 ? "" : (minutes % 60) + "分") : minutes + "分钟"; }
    }
}
