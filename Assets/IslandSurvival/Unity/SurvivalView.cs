using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardKKK.IslandSurvival.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    /// <summary>显示已提交的快照；所有按钮、快捷键、拖放走同一核心命令入口。</summary>
    public sealed class SurvivalView : MonoBehaviour
    {
        [SerializeField] private RectTransform logicalDesktop, tableSurface, naturalSurface, conditionSurface, cardDragLayer, inventoryContent, actionContent, optionContent;
        [SerializeField] private SurvivalCardView cardTemplate;
        [SerializeField] private SurvivalChoiceView choiceTemplate;
        [SerializeField] private Text clockText, bodyText, humanText, capacityText, selectedText, previewText, noticeText, taskText, modalTitle, modalDetail, batchText, confirmationText;
        [SerializeField] private Button pauseButton, speedButton, recipesButton, bodyButton, goalsButton, tidyButton, restButton, menuButton, batchLessButton, batchMoreButton, startButton, modalCloseButton, saveButton, loadButton, restartButton, returnButton, menuCloseButton, confirmYesButton, confirmNoButton;
        [SerializeField] private GameObject modalPanel, menuPanel, confirmationPanel;
        [SerializeField] private InputField searchInput;
        [SerializeField] private GameObject relationsPanel;
        [SerializeField] private RectTransform relationSources, relationUses;
        [SerializeField] private Text relationCenterText;
        [SerializeField] private Button relationCloseButton;
        [SerializeField] private SurvivalTheme theme;
        [SerializeField] private AudioSource soundSource;
        [SerializeField] private Button backupButton, settingsButton, quitButton;
        [SerializeField] private RectTransform usableTableRect;
        public SurvivalTheme Theme => theme;
        private SurvivalEngine engine;
        private SurvivalConfiguration config;
        private SurvivalDefinition rules;
        private SurvivalState state;
        private Action<Func<SurvivalEngine, SurvivalResult>> run;
        private Action save, load, backup, restart, leave, exit, confirm;
        private readonly Dictionary<string, SurvivalCardView> cards = new Dictionary<string, SurvivalCardView>();
        private readonly List<SurvivalChoiceView> inspectorRows = new List<SurvivalChoiceView>(), actionRows = new List<SurvivalChoiceView>(), optionRows = new List<SurvivalChoiceView>();
        private string selectedId = "", pendingRecipe = "", pendingTarget = "", pendingSecondary = "", pendingWound = "", pendingTrait = "";
        private string pendingIngredient = "";
        private Func<SurvivalEngine, SurvivalResult> pendingCommand;
        private int batch = 1;
        private bool bodyPage;
        private bool dragWasPaused;
        private readonly List<string> careSelection = new List<string>();
        private readonly List<SurvivalChoiceView> sourceRows = new List<SurvivalChoiceView>(), useRows = new List<SurvivalChoiceView>();
        private Action<string> searchChanged;
        private string messageOverride = "";
        private bool deathShown;
        private float playerVolume = 1;
        private bool navigationDirty = true;
        private GameObject navigationScope;
        public RectTransform DragLayer => cardDragLayer;
        private CardRule Rule(string id) => rules.Cards.Find(c => c.Id == id);
        private ItemState Item(string id) => state.Items.Find(i => i.Id == id);
        private static void SetButton(Button button, Action action)
        { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => action()); }
        private void Awake()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true)) if (child.name == "PreviewRoot") child.gameObject.SetActive(false);
            cardTemplate.gameObject.SetActive(false); choiceTemplate.gameObject.SetActive(false);
            modalPanel.SetActive(false); menuPanel.SetActive(false); confirmationPanel.SetActive(false);
            relationsPanel.SetActive(false);
        }
        public void Bind(SurvivalEngine value, SurvivalConfiguration configuration, Action<Func<SurvivalEngine, SurvivalResult>> submit, Action saveAction, Action loadAction, Action backupAction, Action restartAction, Action returnAction, Action exitAction)
        {
            engine = value; config = configuration; rules = engine.Rules; run = submit; save = saveAction; load = loadAction; backup = backupAction; restart = restartAction; leave = returnAction;
            exit = exitAction;
            playerVolume = PlayerPrefs.GetFloat("IslandSurvival.Volume", 1); deathShown = false;
            if (PlayerPrefs.HasKey("IslandSurvival.Width")) Screen.SetResolution(PlayerPrefs.GetInt("IslandSurvival.Width",1600), PlayerPrefs.GetInt("IslandSurvival.Height",900), PlayerPrefs.GetInt("IslandSurvival.Fullscreen",Screen.fullScreen?1:0)==1);
            selectedId = ""; pendingCommand = null; pendingRecipe = ""; bodyPage = false;
            modalPanel.SetActive(false); menuPanel.SetActive(false); confirmationPanel.SetActive(false);
            relationsPanel.SetActive(false);
            SetButton(relationCloseButton, () => relationsPanel.SetActive(false));
            searchInput.onValueChanged.RemoveAllListeners(); searchInput.onValueChanged.AddListener(text => searchChanged?.Invoke(text));
            SetButton(pauseButton, () => Do(e => e.SetPaused(!e.Snapshot.Paused)));
            SetButton(speedButton, () => Do(e => e.SetSpeed(e.Snapshot.Speed == 1 ? 3 : 1)));
            SetButton(recipesButton, () => ShowRecipes()); SetButton(bodyButton, ShowBody); SetButton(goalsButton, ShowGoals);
            SetButton(tidyButton, () => { Do(e => e.SetPaused(true)); Do(e => e.Tidy()); }); SetButton(restButton, () => PickRecipe(rules.Recipes.First(r => r.Kind == RecipeKind.Rest).Id));
            SetButton(menuButton, OpenMenu); SetButton(menuCloseButton, () => menuPanel.SetActive(false));
            SetButton(saveButton, () => save()); SetButton(loadButton, RequestLoad);
            SetButton(backupButton, () => Confirm("恢复前一份自动备份？当前未保存的进度会被替换。", () => { backup(); menuPanel.SetActive(false); }));
            SetButton(settingsButton, ShowSettings); SetButton(quitButton, () => Confirm("保存当前自动进度并退出游戏？", () => exit()));
            SetButton(restartButton, () => Confirm("重新开始营地？当前进度将由新营地覆盖，保留上一份有效备份。", () => restart()));
            SetButton(returnButton, () => leave()); SetButton(modalCloseButton, CloseModal);
            SetButton(batchLessButton, () => { batch = Mathf.Max(1, batch - 1); UpdatePreview(); });
            SetButton(batchMoreButton, () => { batch = Mathf.Min(1000, batch + 1); UpdatePreview(); });
            SetButton(startButton, StartPending); SetButton(confirmNoButton, () => confirmationPanel.SetActive(false));
            SetButton(confirmYesButton, () => { Action action = confirm; confirmationPanel.SetActive(false); confirm = null; action?.Invoke(); });
            Render();
        }
        private void Update()
        {
            if (engine == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            { if (confirmationPanel.activeSelf) confirmationPanel.SetActive(false); else if (relationsPanel.activeSelf) relationsPanel.SetActive(false); else if (modalPanel.activeSelf) CloseModal(); else if (menuPanel.activeSelf) menuPanel.SetActive(false); else OpenMenu(); }
            if (modalPanel.activeSelf || menuPanel.activeSelf || confirmationPanel.activeSelf || relationsPanel.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Space)) Do(e => e.SetPaused(!e.Snapshot.Paused));
            if (Input.GetKeyDown(KeyCode.R)) tidyButton.onClick.Invoke();
            if (Input.GetKeyDown(KeyCode.F5)) saveButton.onClick.Invoke();
            if (Input.GetKeyDown(KeyCode.F9)) RequestLoad();
        }
        private void Do(Func<SurvivalEngine, SurvivalResult> command) { run?.Invoke(command); }
        public void Message(string message) { messageOverride = message; if (noticeText != null) noticeText.text = message; }
        public void ClearMessage() { messageOverride = ""; }
        public void Feedback(bool success, bool warning = false)
        { AudioClip clip = warning ? theme.Warning : success ? theme.Success : theme.Denied; if (clip != null && soundSource != null) soundSource.PlayOneShot(clip, theme.Volume * playerVolume); }
        private void Confirm(string message, Action action)
        { confirmationText.text = message; confirm = action; confirmationPanel.SetActive(true); Do(e => e.SetPaused(true)); }
        private void OpenMenu() { Do(e => e.SetPaused(true)); menuPanel.SetActive(true); }
        private void RequestLoad() => Confirm("读取手动存档？当前未保存的进度会被替换。", () =>
        { load(); menuPanel.SetActive(false); if (!engine.Dead) CloseModal(); });
        private void ShowSettings()
        {
            menuPanel.SetActive(false);
            Modal("设置", "音量 " + (playerVolume * 100).ToString("0") + "% · " + (Screen.fullScreen ? "全屏" : "窗口") + " " + Screen.width + "×" + Screen.height);
            Row(optionContent, optionRows, "音量降低", "减少10%。", () => { SetVolume(playerVolume - .1f); ShowSettings(); });
            Row(optionContent, optionRows, "音量提高", "增加10%。", () => { SetVolume(playerVolume + .1f); ShowSettings(); });
            Row(optionContent, optionRows, Screen.fullScreen ? "切换窗口" : "切换全屏", "保留界面比例。", () => { Screen.fullScreen = !Screen.fullScreen; PlayerPrefs.SetInt("IslandSurvival.Fullscreen", Screen.fullScreen ? 1 : 0); PlayerPrefs.Save(); });
            foreach (Vector2Int size in new[] { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1920,1080) })
            { Vector2Int resolution = size; Row(optionContent, optionRows, size.x + "×" + size.y, "设置窗口或显示分辨率。", () => { Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreen); PlayerPrefs.SetInt("IslandSurvival.Width",resolution.x); PlayerPrefs.SetInt("IslandSurvival.Height",resolution.y); PlayerPrefs.Save(); }); }
            Row(optionContent, optionRows, "制作人员", "项目与资源来源。", () => Modal("制作人员", theme.Credits));
        }
        private void SetVolume(float value) { playerVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat("IslandSurvival.Volume", playerVolume); PlayerPrefs.Save(); Feedback(true); }
        private void ShowFailure()
        {
            var changes = state.History.Where(h => h.Minute >= state.Minute - 1440).GroupBy(h => h.Key).Select(g => new { Key=g.Key, Delta=g.Sum(x => x.Delta)*(g.Key.StartsWith("body:",StringComparison.Ordinal)?100:1) }).Where(c => Math.Abs(c.Delta) > .00001).OrderByDescending(c => Math.Abs(c.Delta)).ToList();
            var text = new StringBuilder(state.FailureReason).Append("\n\n最近24小时净变化（包含投入、产出、食用与腐败）：");
            foreach (var change in changes.Take(18)) text.Append("\n").Append(ResourceName(change.Key)).Append(" ").Append(change.Delta > 0 ? "+" : "").Append(change.Delta.ToString("0.#"));
            text.Append("\n\n失败进度与前一自动备份分别保留，手动存档不会被自动进度覆盖。");
            Modal("营地生存结束", text.ToString());
            Row(optionContent, optionRows, "读取手动存档", "恢复你主动保存的营地。", RequestLoad);
            Row(optionContent, optionRows, "恢复自动备份", "读取前一份有效自动进度。", () => Confirm("恢复前一份自动备份？", () => { backup(); if (!engine.Dead) CloseModal(); }));
            Row(optionContent, optionRows, "重新开始", "建立新营地，时间暂停。", () => Confirm("重新开始？", () => restart()));
            Row(optionContent, optionRows, "返回牌桌", "保留独立孤岛进度。", () => leave());
        }
        private string ResourceName(string key)
        {
            switch (key) { case "body:food": return "饱食"; case "body:mind":return "精神"; case "body:vital":return "生命"; case "body:health":return "健康"; default:return Name(key); }
        }
        private void CloseModal() { modalPanel.SetActive(false); pendingCommand = null; pendingRecipe = ""; bodyPage = false; }
        private void ClearRows(List<SurvivalChoiceView> rows) { foreach (SurvivalChoiceView row in rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); } rows.Clear(); navigationDirty = true; }
        private SurvivalChoiceView Row(RectTransform content, List<SurvivalChoiceView> rows, string title, string detail, Action action, bool enabled = true)
        {
            SurvivalChoiceView row = Instantiate(choiceTemplate, content); row.gameObject.SetActive(true);
            ((RectTransform)row.transform).anchorMin = new Vector2(0, 1); ((RectTransform)row.transform).anchorMax = new Vector2(1, 1);
            ((RectTransform)row.transform).pivot = new Vector2(0, 1); ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -rows.Count * 66);
            ((RectTransform)row.transform).sizeDelta = new Vector2(0, 62); row.Bind(title, detail, action, enabled); rows.Add(row);
            content.sizeDelta = new Vector2(content.sizeDelta.x, rows.Count * 66);
            navigationDirty = true;
            return row;
        }
        private void Modal(string title, string detail = "")
        {
            bodyPage = false; modalTitle.text = title; modalDetail.text = detail; previewText.text = "";
            relationsPanel.SetActive(false); searchChanged = null; searchInput.SetTextWithoutNotify(""); searchInput.gameObject.SetActive(false);
            pendingCommand = null; pendingRecipe = ""; startButton.interactable = false; batchLessButton.gameObject.SetActive(false); batchMoreButton.gameObject.SetActive(false); batchText.text = "";
            ClearRows(optionRows); optionContent.sizeDelta = new Vector2(optionContent.sizeDelta.x, 0); optionContent.anchoredPosition = Vector2.zero;
            modalPanel.SetActive(true); Do(e => e.SetPaused(true));
        }
        public void Render()
        {
            if (engine == null) return;
            SurvivalState next = engine.Snapshot;
            if (state != null && next.Revision != state.Revision)
            {
                bool warning = next.Items.Any(i => Rule(i.DefinitionId).Kind == CardKind.Condition && !state.Items.Any(old => old.Id == i.Id));
                bool output = next.Items.Any(i => Rule(i.DefinitionId).Kind != CardKind.Condition && (!state.Items.Any(old => old.Id == i.Id) || i.Quantity > (state.Items.Find(old => old.Id == i.Id)?.Quantity ?? 0)));
                if (warning || output) Feedback(true, warning);
            }
            if (state != null && state.Notice != next.Notice) messageOverride = "";
            state = next;
            usableTableRect.sizeDelta = new Vector2((float)engine.ProductionWidth, (float)engine.ProductionHeight);
            int day = (int)((state.Minute - rules.StartMinute) / 1440) + 1;
            clockText.text = "第" + day + "天  " + ((state.Minute % 1440) / 60).ToString("00") + ":" + (state.Minute % 60).ToString("00") + (state.Paused ? "  暂停" : "  " + state.Speed + "倍");
            bodyText.text = "饱食 " + Percent(state.Body.Food) + "    精神 " + Percent(state.Body.Mind) + "    健康 " + engine.Health.ToString("0") + "    生命 " + Percent(state.Body.Vitality);
            TaskState human = state.Tasks.Find(t => t.Human && !t.Suspended);
            humanText.text = human == null ? "人力空闲" : rules.Recipes.Find(r => r.Id == human.RecipeId).Name + " " + Percent(human.Progress);
            int used = state.Items.Where(i => i.OwnerId == "" && Rule(i.DefinitionId).Kind != CardKind.Condition && Rule(i.DefinitionId).Role != CardRole.NaturalNode).Sum(i => Rule(i.DefinitionId).Footprint);
            int held = state.Reservations.Where(r => r.OwnerId == "" && r.StackId == "").Sum(r => Rule(r.DefinitionId).Footprint);
            capacityText.text = "生产区 " + used + "/" + state.Capacity + (held > 0 ? " 预留" + held : "");
            pauseButton.GetComponentInChildren<Text>().text = state.Paused ? "继续" : "暂停"; speedButton.GetComponentInChildren<Text>().text = state.Speed + "倍";
            RenderCards(); RenderInspector(); RenderTasks();
            if (state.Dead) { noticeText.text = state.FailureReason; }
            else if (state.Completed && !state.ContinueAfterWin && !modalPanel.activeSelf)
            {
                Modal("营地已稳定", "食物、库存、护理与循环生产达到了目标。可以保存这座营地，并继续经营。");
                Row(optionContent, optionRows, "继续经营", "保留营地与全部生产链。", () => { CloseModal(); Do(e => e.Continue()); });
            }
            if (bodyPage && modalPanel.activeSelf) modalDetail.text = BodyDescription();
            if (state.Dead && !deathShown) { deathShown = true; ShowFailure(); }
            if (!state.Dead) deathShown = false;
        }
        private void RenderCards()
        {
            var visible = new HashSet<string>(); int naturalIndex = 0;
            foreach (ItemState item in state.Items.Where(i => i.OwnerId == "" && i.Place != Place.Equipment))
            {
                CardRule rule = Rule(item.DefinitionId);
                RectTransform surface = rule.Kind == CardKind.Condition ? conditionSurface : rule.Role == CardRole.NaturalNode ? naturalSurface : tableSurface;
                SurvivalCardView card = CardView(item, surface); visible.Add(item.Id);
                if (!card.Dragging)
                {
                    RectTransform rect = (RectTransform)card.transform; rect.localScale = Vector3.one;
                    rect.sizeDelta = rule.Footprint == 4 ? new Vector2((float)rules.CellWidth * 2 - 8, (float)rules.CellHeight * 2 - 6) : new Vector2((float)rules.CellWidth - 8, (float)rules.CellHeight - 6);
                    rect.anchoredPosition = rule.Role == CardRole.NaturalNode ? new Vector2(naturalIndex++ * (float)rules.CellWidth, 0) : new Vector2((float)item.X, -(float)item.Y);
                }
            }
            // 容器内仍是独立牌实例；小牌排入所属建筑，点击可查看，拖出仍走Transfer。
            foreach (ItemState item in state.Items.Where(i => i.OwnerId != "" && i.Place != Place.Equipment).OrderBy(i => i.Place).ThenBy(i => i.Slot))
            {
                ItemState root = RootOf(item); if (root == null || !cards.TryGetValue(root.Id, out SurvivalCardView rootView)) continue;
                if (Rule(root.DefinitionId).Footprint != 4) continue;
                var siblings = state.Items.Where(i => i.OwnerId != "" && i.Place != Place.Equipment && RootOf(i)?.Id == root.Id).OrderBy(i => i.Place).ThenBy(i => i.Slot).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
                int index = siblings.FindIndex(i => i.Id == item.Id);
                SurvivalCardView card = CardView(item, (RectTransform)rootView.transform); visible.Add(item.Id);
                if (!card.Dragging)
                {
                    RectTransform rect = (RectTransform)card.transform; rect.localScale = Vector3.one * .42f;
                    rect.sizeDelta = new Vector2((float)rules.CellWidth - 8, (float)rules.CellHeight - 6);
                    int cols = Rule(root.DefinitionId).Footprint == 4 ? 4 : 2;
                    rect.anchoredPosition = new Vector2(4 + index % cols * (float)rules.CellWidth * rect.localScale.x, -64 - index / cols * (float)rules.CellHeight * rect.localScale.y);
                }
            }
            foreach (string id in cards.Keys.Where(id => !visible.Contains(id)).ToArray()) { Destroy(cards[id].gameObject); cards.Remove(id); }
        }
        private ItemState RootOf(ItemState item)
        {
            int budget = state.Items.Count + 1;
            while (item != null && item.OwnerId != "" && item.OwnerId != "player" && budget-- > 0) item = Item(item.OwnerId);
            return item;
        }
        private SurvivalCardView CardView(ItemState item, RectTransform parent)
        {
            if (!cards.TryGetValue(item.Id, out SurvivalCardView card))
            { card = Instantiate(cardTemplate, parent); card.gameObject.SetActive(true); cards.Add(item.Id, card); }
            if (!card.Dragging)
            {
                if (card.transform.parent != parent) card.transform.SetParent(parent, false); var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            }
            card.Render(this, item, Rule(item.DefinitionId), CardDetail(item), config.FindArtwork(item.DefinitionId), item.Id == selectedId);
            return card;
        }
        private string CardDetail(ItemState i)
        {
            CardRule r = Rule(i.DefinitionId);
            if (r.Kind == CardKind.Condition)
            {
                InjuryState wound = state.Body.Injuries.Find(w => w.Id == i.ConditionId);
                if (wound != null) return "损伤 " + Percent(wound.Deficit) + (wound.DressingUntil > state.Minute ? " · 已敷料" : " · 开放") + "\n" + wound.Name;
                AfflictionState a = state.Body.Afflictions.Find(x => x.Id == i.ConditionId);
                return a == null ? "" : "严重度 " + Percent(a.Severity) + "\n" + a.Source;
            }
            TaskState task = state.Tasks.Find(t => t.TargetId == i.Id || t.SecondaryId == i.Id);
            if (task != null) return rules.Recipes.Find(x => x.Id == task.RecipeId).Name + " " + Percent(task.Progress);
            if (r.Role == CardRole.NaturalNode) return string.Join(" · ", i.Stock.Select(a => Name(a.DefinitionId) + a.Count));
            if (r.Kind == CardKind.Tool) return r.InitialDurability > 0 ? "耐久 " + i.Durability + "/" + i.MaxDurability + " " + i.Status : r.Family;
            if (r.Structure > 0) return "结构 " + i.Structure + "/" + r.Structure + (r.FuelCapacity > 0 ? "  火 " + i.Fuel.ToString("0.#") : "");
            if (r.Kind == CardKind.Creature) return i.Status != "" ? i.Status : i.Mature ? "成熟" + (i.Fed ? " · 已喂" : "") : "成长中";
            long expiry = i.Lots.Where(l => l.ExpiresAt > 0).Select(l => l.ExpiresAt).DefaultIfEmpty(0).Min();
            return expiry > 0 ? "余 " + Hours(Math.Max(0, expiry - state.Minute)) : r.Family;
        }
        public void Select(string id) { selectedId = id; Render(); }
        public bool CanDrag(string id) => engine != null && Item(id) != null && Rule(Item(id).DefinitionId).Role != CardRole.NaturalNode && !modalPanel.activeSelf && !menuPanel.activeSelf && !confirmationPanel.activeSelf && !relationsPanel.activeSelf;
        public void BeginCardDrag() { dragWasPaused = state.Paused; Do(e => e.SetPaused(true)); }
        public bool CanSplitDrag(string id) => Item(id) != null && Rule(Item(id).DefinitionId).Kind == CardKind.Material && Item(id).Quantity > 1;
        public void EndCardDrag()
        { if (!dragWasPaused && Application.isFocused && !modalPanel.activeSelf && !menuPanel.activeSelf && !confirmationPanel.activeSelf && !relationsPanel.activeSelf) Do(e => e.SetPaused(false)); }
        public void Drop(string id, PointerEventData data, bool single, Vector3 worldPosition)
        {
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
            {
                SurvivalCardView other = hit.gameObject.GetComponentInParent<SurvivalCardView>();
                if (other != null && other.ItemId != id) { DropOn(id, other.ItemId, single); return; }
            }
            ItemState item = Item(id); if (item == null) return;
            RectTransform surface = Rule(item.DefinitionId).Kind == CardKind.Condition ? conditionSurface : tableSurface;
            if (!RectTransformUtility.RectangleContainsScreenPoint(surface, data.position, data.pressEventCamera)) { Render(); return; }
            if (item.OwnerId != "") { Do(e => e.Transfer(id, count:single ? 1 : 0)); return; }
            Vector3 point = surface.InverseTransformPoint(worldPosition);
            double x = Math.Max(0, point.x);
            double y = Math.Max(0, -point.y);
            Do(e => e.Move(id, x, y, single ? 1 : 0));
        }
        private void DropOn(string sourceId, string targetId, bool single)
        {
            ItemState source = Item(sourceId), target = Item(targetId); if (source == null || target == null) return;
            selectedId = targetId;
            if (source.DefinitionId == target.DefinitionId && Rule(source.DefinitionId).Kind == CardKind.Material) { Do(e => e.Stack(sourceId, targetId, single ? 1 : int.MaxValue)); return; }
            Modal(Name(source.DefinitionId) + " → " + Name(target.DefinitionId));
            CardRule receiver = Rule(target.DefinitionId);
            if (receiver.Kind == CardKind.Facility || receiver.Kind == CardKind.Area)
            {
                if (receiver.InputSlots > 0) Row(optionContent, optionRows, "放入输入槽", (single ? "移动1份" : "整堆移动") + "；保留到期批次。", () => CommitImmediate(e => e.Transfer(sourceId, targetId, Place.Input, single ? 1 : 0)));
                if (receiver.StorageSlots > 0) Row(optionContent, optionRows, "放入储物箱", "占用一个材料堆槽位。", () => CommitImmediate(e => e.Transfer(sourceId, targetId, Place.Storage, single ? 1 : 0)));
                if (receiver.AdultSlots > 0 && Rule(source.DefinitionId).Role == CardRole.AdultSnail) Row(optionContent, optionRows, "放入成年槽", "喂养与生产仍按个体窗口结算。", () => CommitImmediate(e => e.Transfer(sourceId, targetId, Place.Adult)));
                if (receiver.YoungSlots > 0 && Rule(source.DefinitionId).Role == CardRole.YoungSnail) Row(optionContent, optionRows, "放入幼体槽", "在围栏中成长。", () => CommitImmediate(e => e.Transfer(sourceId, targetId, Place.Young)));
                if (receiver.Role == CardRole.Fire && Rule(source.DefinitionId).FuelValue > 0) Row(optionContent, optionRows, "添入1份燃料", "增加燃料" + Rule(source.DefinitionId).FuelValue, () => CommitImmediate(e => e.Refuel(targetId, sourceId)));
            }
            foreach (string rid in engine.KnownRecipeIds(targetId))
            {
                RecipeRule recipe = rules.Recipes.Find(r => r.Id == rid);
                if (!recipe.Inputs.Any(input => input.Alternatives.Contains(source.DefinitionId))) continue;
                string captured = rid;
                Row(optionContent, optionRows, recipe.Name, RecipeSummary(recipe), () => PickRecipe(captured, targetId, sourceId));
            }
            if (optionRows.Count == 0) modalDetail.text = "这两张牌目前没有可用的组合。";
        }
        private void CommitImmediate(Func<SurvivalEngine, SurvivalResult> command) { CloseModal(); Do(command); }
        private void RenderInspector()
        {
            ClearRows(inspectorRows); ClearRows(actionRows); inventoryContent.sizeDelta = new Vector2(inventoryContent.sizeDelta.x, 0); actionContent.sizeDelta = new Vector2(actionContent.sizeDelta.x, 0);
            ItemState selected = Item(selectedId);
            if (selected == null) { selectedText.text = "选择一张牌\n\n拖动材料、工具或状态牌，查看可执行的操作。"; return; }
            CardRule rule = Rule(selected.DefinitionId);
            selectedText.text = rule.Name + "\n" + DescribeItem(selected);
            foreach (ItemState child in state.Items.Where(i => i.OwnerId == selected.Id))
            {
                string id = child.Id;
                Row(inventoryContent, inspectorRows, Name(child.DefinitionId) + " ×" + child.Quantity, PlaceName(child.Place) + " " + CardDetail(child), () => Select(id));
            }
            if (selected.OwnerId != "" && selected.Place != Place.Equipment)
                Row(actionContent, actionRows, "取到桌面", "保留实例和全部批次。", () => Do(e => e.Transfer(selected.Id)));
            if (rule.Nutrition > 0) Row(actionContent, actionRows, "食用1份", "饱食增加" + rule.Nutrition.ToString("0.#") + "，超出上限不保留。", () => Do(e => e.Eat(selected.Id)));
            if (rule.Kind == CardKind.Material && selected.Quantity > 1) Row(actionContent, actionRows, "拆出1份", "保留最早到期批次，需要一个空位置。", () => Do(e => e.Split(selected.Id)));
            if (rule.Role == CardRole.Ornament) Row(actionContent, actionRows, selected.Place == Place.Equipment ? "取下" : "佩戴", "仅一个饰品槽，休息时生效。", () => Do(e => selected.Place == Place.Equipment ? e.Unequip(selected.Id) : e.Equip(selected.Id)));
            if (rule.Role == CardRole.AdultSnail || rule.Role == CardRole.YoungSnail) Row(actionContent, actionRows, "喂养", "手动取桌面饲料；本窗口最多一次。", () => Do(e => e.Feed(selected.Id)));
            foreach (string rid in engine.KnownRecipeIds(selectedId))
            {
                RecipeRule recipe = rules.Recipes.Find(r => r.Id == rid);
                string captured = rid; Row(actionContent, actionRows, recipe.Name, RecipeSummary(recipe), () => PickRecipe(captured, selected.Id));
            }
            TaskState task = state.Tasks.Find(t => t.TargetId == selected.Id || t.SecondaryId == selected.Id || t.LockedItems.Contains(selected.Id));
            if (task != null) Row(actionContent, actionRows, "中止当前工作", "本轮材料、损耗和已付劳动不返还。", () => Confirm("中止“" + rules.Recipes.Find(r => r.Id == task.RecipeId).Name + "”？本轮投入不返还，未开工批次不扣费。", () => Do(e => e.CancelTask(task.Id))));
            if (rule.Kind == CardKind.Area || rule.Kind == CardKind.Facility)
            {
                if (rule.InputSlots > 0) Row(actionContent, actionRows, "补入材料", "选择库存中的一整堆，合入输入槽。", () => ShowSupply(selected.Id));
                Row(actionContent, actionRows, "重复生产", selected.Repeat ? "当前已开启，点击选择或关闭。" : "每轮重新校验输入、保留量和产出槽。", () => ShowRepeat(selected.Id));
                Row(actionContent, actionRows, "材料保留量", "留在输入槽，不被新周期使用。", () => ShowReserves(selected.Id));
            }
            Row(actionContent, actionRows, "查看完整规则", "用途、生命周期与操作条件。", () => { Modal(rule.Name, DescribeItem(selected, true)); });
            Row(actionContent, actionRows, "查看工艺关系", "来源与用途各一层，点击工艺可预览。", () => ShowRelations(selected.DefinitionId));
        }
        private void RenderTasks()
        {
            TaskState human = state.Tasks.Find(t => t.Human && !t.Suspended);
            int automatic = state.Tasks.Count(t => !t.Human);
            taskText.text = human == null ? "人力空闲" : rules.Recipes.Find(r => r.Id == human.RecipeId).Name + " · " + Percent(human.Progress);
            taskText.text += "    自动工作 " + automatic + "项" + (state.Tasks.Any(t => t.Suspended) ? "    一项工作暂放中" : "") + "    全部工作可在身体详情中查看";
            if (messageOverride != "") noticeText.text = messageOverride;
            else if (state.Notice != "") noticeText.text = state.Notice;
        }
        private string DescribeItem(ItemState i, bool full = false)
        {
            CardRule rule = Rule(i.DefinitionId); var text = new StringBuilder(CardDetail(i));
            if (i.TraitId != "") text.Append("\n性状：").Append(rules.Traits.Find(t => t.Id == i.TraitId)?.Name);
            if (rule.Kind == CardKind.Material) text.Append("\n").Append(i.Quantity).Append("份 / 每堆").Append(rule.StackLimit);
            if (rule.Kind == CardKind.Area) text.Append("\n区域条件 ").Append(engine.ProductionFactor(i.Id).ToString("0.00")).Append(" · 输入").Append(rule.InputSlots).Append(" 输出").Append(rule.OutputSlots);
            if (rule.Kind == CardKind.Condition)
            {
                AfflictionState a = state.Body.Afflictions.Find(x => x.Id == i.ConditionId);
                InjuryState w = state.Body.Injuries.Find(x => x.Id == i.ConditionId || a != null && x.Id == a.InjuryId);
                if (w != null) text.Append("\n清洁 ").Append(Percent(w.Cleanliness)).Append(" · 敷料余").Append(Hours(Math.Max(0, w.DressingUntil - state.Minute))).Append("\n护理余").Append(Hours(Math.Max(0, w.CareUntil - state.Minute)));
                if (a != null) text.Append("\n").Append(rules.Afflictions.Find(r => r.Id == a.RuleId)?.Description);
            }
            if (full)
            {
                text.Append("\n\n").Append(rule.Description);
                if (rule.Ability != "") text.Append("\n切割硬度 ").Append(rule.Hardness).Append(" · 维修降低最大耐久 ").Append(rule.RepairLoss);
                if (rule.Nutrition > 0) text.Append("\n每份营养 ").Append(rule.Nutrition).Append("，保质 ").Append(rule.ShelfMinutes > 0 ? Hours(rule.ShelfMinutes) : "无限期");
                foreach (Lot lot in i.Lots) text.Append("\n批次 ").Append(lot.Count).Append("份：").Append(lot.ExpiresAt == 0 ? "无期限" : "余" + Hours(Math.Max(0, lot.ExpiresAt - state.Minute)));
                foreach (string id in engine.KnownRecipeIds(i.Id)) text.Append("\n").Append(rules.Recipes.Find(r => r.Id == id).Name);
            }
            return text.ToString();
        }
        private void ShowRecipes(string targetId = null)
        {
            Modal("已发现工艺", "选择工艺，核对材料、目标、时间和产出后开始。");
            searchInput.gameObject.SetActive(true);
            searchChanged = query => FillRecipeRows(targetId, query); FillRecipeRows(targetId, "");
        }
        private void FillRecipeRows(string targetId, string query)
        {
            ClearRows(optionRows); optionContent.anchoredPosition = Vector2.zero;
            foreach (string id in engine.KnownRecipeIds(targetId))
            {
                RecipeRule recipe = rules.Recipes.Find(r => r.Id == id); string captured = id;
                if (!string.IsNullOrWhiteSpace(query) && (recipe.Name + " " + recipe.Id + " " + recipe.Description + " " + RecipeSummary(recipe)).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                Row(optionContent, optionRows, recipe.Name, RecipeSummary(recipe), () => PickRecipe(captured, targetId ?? ""));
            }
        }
        private void ShowRelations(string definitionId)
        {
            Do(e => e.SetPaused(true)); modalPanel.SetActive(false); relationsPanel.SetActive(true);
            ClearRows(sourceRows); ClearRows(useRows);
            CardRule center = Rule(definitionId); relationCenterText.text = center.Name + "\n\n" + center.Description;
            var known = new HashSet<string>(engine.KnownRecipeIds());
            foreach (RecipeRule recipe in rules.Recipes)
            {
                string id = recipe.Id;
                bool source = recipe.Outputs.Any(a => a.DefinitionId == definitionId);
                bool use = recipe.Inputs.Any(a => a.Alternatives.Contains(definitionId)) || recipe.TargetDefinitions.Contains(definitionId) || recipe.RequiredRole != CardRole.None && recipe.RequiredRole == center.Role || recipe.RequiredAbility != "" && recipe.RequiredAbility == center.Ability;
                if (!source && !use) continue;
                if (!known.Contains(id))
                { Row(source ? relationSources : relationUses, source ? sourceRows : useRows, "尚未发现的工艺", recipe.Kind == RecipeKind.Breed || recipe.Kind == RecipeKind.Harvest ? "生物相关线索" : "制作与加工线索", () => { }, false); continue; }
                Color color = RecipeColor(recipe);
                if (source) Row(relationSources, sourceRows, recipe.Name, RecipeSummary(recipe), () => { relationsPanel.SetActive(false); PickRecipe(id); }).Tint(color);
                if (use) Row(relationUses, useRows, recipe.Name, RecipeSummary(recipe), () => { relationsPanel.SetActive(false); PickRecipe(id, selectedId); }).Tint(color);
            }
            if (sourceRows.Count == 0) Row(relationSources, sourceRows, "尚无已发现的制造工艺", "自然来源、初始物资或尚未发现。", () => { }, false);
            if (useRows.Count == 0) Row(relationUses, useRows, "尚无已发现的工艺用途", "仍可查看这张牌本身的操作。", () => { }, false);
        }
        private Color RecipeColor(RecipeRule recipe)
        {
            List<ItemState> targets = engine.RecipeTargets(recipe.Id);
            bool targetNeeded = recipe.TargetRequired || recipe.RequiredRole != CardRole.None || recipe.TargetDefinitions.Count > 0;
            bool equipmentTarget = rules.Cards.Any(c => (c.Kind == CardKind.Facility || c.Kind == CardKind.Area) && (recipe.RequiredRole != CardRole.None && c.Role == recipe.RequiredRole || recipe.TargetDefinitions.Contains(c.Id)));
            if (targetNeeded && equipmentTarget && targets.Count == 0 || recipe.SecondaryRole != CardRole.None && !state.Items.Any(i => Rule(i.DefinitionId).Role == recipe.SecondaryRole)) return theme.MissingFacility;
            string target = targetNeeded ? targets.FirstOrDefault()?.Id ?? "" : "";
            string secondary = recipe.SecondaryRole == CardRole.None ? "" : state.Items.FirstOrDefault(i => Rule(i.DefinitionId).Role == recipe.SecondaryRole)?.Id ?? "";
            return engine.PreviewRecipe(recipe.Id, target, secondary).Success ? theme.Ready : theme.MissingMaterial;
        }
        private void PickRecipe(string recipeId, string targetId = "", string suppliedId = "")
        {
            RecipeRule recipe = rules.Recipes.Find(r => r.Id == recipeId); if (recipe == null) return;
            pendingRecipe = recipeId; pendingTarget = targetId; pendingSecondary = ""; pendingWound = ""; pendingTrait = ""; batch = 1;
            pendingIngredient = suppliedId;
            bool targetNeeded = recipe.TargetRequired || recipe.RequiredRole != CardRole.None || recipe.TargetDefinitions.Count > 0;
            List<ItemState> targets = targetNeeded ? engine.RecipeTargets(recipeId) : new List<ItemState>();
            if (targetNeeded && !targets.Any(i => i.Id == targetId))
            {
                Modal("选择“" + recipe.Name + "”的目标");
                foreach (ItemState item in targets)
                { string chosen = item.Id; Row(optionContent, optionRows, Name(item.DefinitionId), CardDetail(item), () => PickRecipe(recipeId, chosen, suppliedId)); }
                if (targets.Count == 0) modalDetail.text = "当前没有适用的目标。";
                return;
            }
            PickSecondary(recipe, suppliedId);
        }
        private void PickSecondary(RecipeRule recipe, string suppliedId)
        {
            if (recipe.SecondaryRole != CardRole.None || recipe.Kind == RecipeKind.Breed || recipe.Kind == RecipeKind.Plant)
            {
                List<ItemState> available = state.Items.Where(i => i.Id != pendingTarget && (recipe.Kind == RecipeKind.Breed ? Rule(i.DefinitionId).Role == CardRole.AdultSnail && i.OwnerId == Item(pendingTarget)?.OwnerId : recipe.Kind == RecipeKind.Plant ? recipe.Inputs.First().Alternatives.Contains(i.DefinitionId) : Rule(i.DefinitionId).Role == recipe.SecondaryRole)).ToList();
                if (available.Any(i => i.Id == suppliedId)) { pendingSecondary = suppliedId; PickTrait(recipe); return; }
                string rememberedTarget = pendingTarget;
                Modal("选择第二张牌", recipe.Kind == RecipeKind.Plant ? "选择实际投入的种苗，性状随种苗保留。" : "明确选择本次共同使用的对象。");
                foreach (ItemState item in available)
                { string chosen = item.Id; Row(optionContent, optionRows, Name(item.DefinitionId), CardDetail(item), () => { pendingRecipe = recipe.Id; pendingTarget = rememberedTarget; pendingSecondary = chosen; PickTrait(recipe); }); }
                return;
            }
            PickTrait(recipe);
        }
        private void PickTrait(RecipeRule recipe)
        {
            if (recipe.Kind == RecipeKind.Plant) pendingTrait = Item(pendingSecondary)?.TraitId ?? "";
            if (recipe.Kind != RecipeKind.SelectSeed && recipe.Kind != RecipeKind.Breed) { ShowRecipePreview(recipe); return; }
            string target = pendingTarget, secondary = pendingSecondary;
            Modal("选择育种方式", "普通繁殖沿用已有性状；指定性状的额外成本与人力会列入预览。");
            if (recipe.Kind == RecipeKind.Breed) Row(optionContent, optionRows, "普通繁殖", "不额外指定性状。", () => { pendingTarget = target; pendingSecondary = secondary; pendingTrait = ""; ShowRecipePreview(recipe); });
            foreach (TraitRule trait in rules.Traits.Where(t => t.Species == (recipe.Kind == RecipeKind.SelectSeed ? CardRole.YamPlant : CardRole.AdultSnail)))
            {
                string id = trait.Id;
                Row(optionContent, optionRows, trait.Name, "周期倍率" + trait.DurationMultiplier.ToString("0.##") + " " + Amounts(trait.AddedOutputs), () => { pendingTarget = target; pendingSecondary = secondary; pendingTrait = id; ShowRecipePreview(recipe); });
            }
        }
        private void ShowRecipePreview(RecipeRule recipe)
        {
            string target = pendingTarget, secondary = pendingSecondary, trait = pendingTrait;
            Modal(recipe.Name, RecipeSummary(recipe) + "\n" + recipe.Description);
            pendingRecipe = recipe.Id; pendingTarget = target; pendingSecondary = secondary; pendingTrait = trait;
            batchLessButton.gameObject.SetActive(true); batchMoreButton.gameObject.SetActive(true);
            UpdatePreview();
        }
        private void UpdatePreview()
        {
            if (pendingRecipe == "") return;
            RecipeRule recipe = rules.Recipes.Find(r => r.Id == pendingRecipe);
            SurvivalResult result = engine.PreviewRecipe(pendingRecipe, pendingTarget, pendingSecondary, pendingWound, batch, pendingTrait, pendingIngredient);
            batchText.text = batch + "轮"; startButton.interactable = result.Success;
            string load = "体力负荷 " + recipe.Physical.ToString("0.##") + " · 认知负荷 " + recipe.Cognitive.ToString("0.##") + " · 危险 " + recipe.Danger.ToString("0.##") + (recipe.Exposure > 0 ? "\n处理有机物时累积接触风险。" : "");
            previewText.text = result.Success ? "预计 " + Hours(result.EstimatedMinutes) + "\n按当前身体预计：饱食−" + (result.EstimatedFood * 100).ToString("0.#") + "，精神−" + (result.EstimatedMind * 100).ToString("0.#") + "\n" + load + "\n" + PreviewCost(result.State) + "\n每个周期开工扣料与耐久，中止不退款。" + (result.Warning == "" ? "" : "\n\n" + result.Warning) : result.Error + "\n\n" + load;
            pendingCommand = e => e.StartRecipe(recipe.Id, pendingTarget, pendingSecondary, pendingWound, batch, pendingTrait, pendingIngredient);
        }
        private string PreviewCost(SurvivalState preview)
        {
            TaskState task = preview.Tasks.FirstOrDefault(t => !state.Tasks.Any(old => old.Id == t.Id)); if (task == null) return "";
            string source = task.Consumed.Count > 0 ? "本轮投入 " + Amounts(task.Consumed) : "本轮不消耗材料";
            string output = Amounts(preview.Reservations.Where(r => r.TaskId == task.Id).Select(r => new Amount(r.DefinitionId, r.Count)).ToList());
            return source + (output != "" ? "\n产出 " + output : "") + (task.LockedItems.Count > 0 ? "\n占用 " + string.Join("、", task.LockedItems.Select(id => preview.Items.Find(i => i.Id == id)).Where(i => i != null).Select(i => Name(i.DefinitionId)).Distinct()) : "");
        }
        private void StartPending()
        {
            if (pendingCommand == null) return;
            Func<SurvivalEngine, SurvivalResult> command = pendingCommand;
            RecipeRule recipe = rules.Recipes.Find(r => r.Id == pendingRecipe);
            Action execute = () => { CloseModal(); Do(e => { SurvivalResult result = command(e); if (result.Success) e.SetPaused(false); return result; }); };
            if (recipe != null && recipe.Permanent) Confirm("永久执行“" + recipe.Name + "”？\n一次产出：" + Amounts(recipe.Outputs) + "\n该对象将被移除，不能恢复。", execute);
            else execute();
        }
        private void ShowRepeat(string id)
        {
            ItemState owner = Item(id); Modal("重复生产");
            if (owner.Repeat) Row(optionContent, optionRows, "关闭重复", "已经投入的本轮继续完成。", () => CommitImmediate(e => e.SetRepeat(id, owner.RepeatRecipe, false)));
            foreach (string rid in engine.KnownRecipeIds(id))
            {
                RecipeRule recipe = rules.Recipes.Find(r => r.Id == rid); if (recipe.Human || recipe.AutomaticMinutes > 0) continue;
                string captured = rid; Row(optionContent, optionRows, recipe.Name, RecipeSummary(recipe), () => CommitImmediate(e => e.SetRepeat(id, captured, true)));
            }
        }
        private void ShowReserves(string id)
        {
            ItemState owner = Item(id); Modal("输入保留量", "选择材料增加1；保留量用于新周期取料，清零可恢复使用。");
            foreach (CardRule card in rules.Cards.Where(c => c.Kind == CardKind.Material))
            {
                string material = card.Id; int current = owner.Reserves.Find(a => a.DefinitionId == material)?.Count ?? 0;
                Row(optionContent, optionRows, card.Name + " 保留" + current, "点击增加1" + (current > 0 ? "；在下方可清除。" : "。"), () => { Do(e => e.SetReserve(id, material, current + 1)); ShowReserves(id); });
                if (current > 0) Row(optionContent, optionRows, "清除“" + card.Name + "”保留量", "设为0。", () => { Do(e => e.SetReserve(id, material, 0)); ShowReserves(id); });
            }
        }
        private void ShowSupply(string id)
        {
            Modal("补入材料", "由你选择本次搬入的材料堆；保质批次和性状保留。自动生产只读取本区域的输入槽。");
            foreach (ItemState material in engine.SupplyCandidates(id))
            {
                string sourceId = material.Id;
                Row(optionContent, optionRows, Name(material.DefinitionId) + " ×" + material.Quantity, CardDetail(material), () =>
                {
                    ItemState existing = state.Items.Find(i => i.OwnerId == id && i.Place == Place.Input && i.DefinitionId == material.DefinitionId && i.TraitId == material.TraitId && i.Quantity < Rule(i.DefinitionId).StackLimit);
                    CommitImmediate(e => existing == null ? e.Transfer(sourceId, id, Place.Input) : e.Stack(sourceId, existing.Id));
                });
            }
            if (optionRows.Count == 0) modalDetail.text = "可投入的材料库存不足。可在选中物的工艺关系中查看来源。";
        }
        private void ShowBody()
        {
            Modal("身体与状态牌", BodyDescription()); bodyPage = true;
            foreach (ItemState condition in state.Items.Where(i => Rule(i.DefinitionId).Kind == CardKind.Condition))
            { string id = condition.Id; Row(optionContent, optionRows, Name(condition.DefinitionId), CardDetail(condition), () => { CloseModal(); Select(id); }); }
            foreach (ItemState equipment in state.Items.Where(i => i.Place == Place.Equipment))
            { string id = equipment.Id; Row(optionContent, optionRows, "佩戴：" + Name(equipment.DefinitionId), "点击选中可取下。", () => { CloseModal(); Select(id); }); }
            TaskState working = state.Tasks.Find(t => t.Human && !t.Suspended);
            if (working != null) Row(optionContent, optionRows, "中止人力工作", "当前轮投入不返还。", () => Confirm("中止当前人力工作？", () => { CloseModal(); Do(e => e.CancelTask(working.Id)); }));
            Row(optionContent, optionRows, "查看记录", "最近的完成、预警和异常来源。", () => Modal("营地记录", string.Join("\n", state.Log)));
            Row(optionContent, optionRows, "工作一览", "进度、投入、批量余数与中止。", ShowTasks);
            Row(optionContent, optionRows, "逐处护理", "选择多处伤口；每处分别消耗材料和人力。", () => { careSelection.Clear(); ShowCareSelection(); });
            Row(optionContent, optionRows, "操作帮助", "按需查看完整操作。", ShowHelp);
        }
        private void ShowTasks()
        {
            Modal("全部工作", "中止会取消本轮产出和尚未开始的后续批次；已经投入的材料、耐久与劳动不返还。");
            foreach (TaskState task in state.Tasks)
            {
                string id = task.Id;
                Row(optionContent, optionRows, rules.Recipes.Find(r => r.Id == task.RecipeId).Name + " " + Percent(task.Progress), "预计余" + Hours(engine.RemainingMinutes(id)) + " · " + task.Remaining + "轮 · 点击中止", () => Confirm("中止这项工作？本轮投入不返还。", () => CommitImmediate(e => e.CancelTask(id))));
                foreach (string wound in task.QueueWounds)
                {
                    string woundId = wound;
                    Row(optionContent, optionRows, "未开始的护理目标", state.Body.Injuries.Find(w => w.Id == wound)?.Name ?? wound, () => { Do(e => e.CancelQueuedWound(id, woundId)); ShowTasks(); });
                }
            }
        }
        private void ShowCareSelection()
        {
            Modal("选择护理伤口", "已选" + careSelection.Count + "处。按选择顺序逐处处理；可在工作一览取消未开始的目标。");
            foreach (ItemState card in state.Items.Where(i => Rule(i.DefinitionId).WoundCard))
            {
                string canonical = card.ConditionId;
                Row(optionContent, optionRows, (careSelection.Contains(canonical) ? "✓ " : "") + Name(card.DefinitionId), CardDetail(card), () => { if (!careSelection.Remove(canonical)) careSelection.Add(canonical); ShowCareSelection(); });
            }
            if (careSelection.Count == 0) return;
            foreach (RecipeRule recipe in rules.Recipes.Where(r => (r.Kind == RecipeKind.Care || r.Kind == RecipeKind.Clean) && engine.KnownRecipeIds().Contains(r.Id)))
            {
                string id = recipe.Id;
                Row(optionContent, optionRows, "按顺序“" + recipe.Name + "”", "每处独立扣料，条件不符时停在该处。", () =>
                {
                    ItemState first = state.Items.Find(i => Rule(i.DefinitionId).WoundCard && i.ConditionId == careSelection[0]);
                    if (first == null) { Message("选中的伤口已结束。"); return; }
                    pendingTarget = first.Id; pendingSecondary = pendingTrait = ""; pendingWound = string.Join(",", careSelection);
                    pendingIngredient = "";
                    ShowRecipePreview(rules.Recipes.Find(r => r.Id == id));
                });
            }
        }
        private string BodyDescription()
        {
            return "饱食 " + Percent(state.Body.Food) + "  精神 " + Percent(state.Body.Mind) + "\n健康 " + engine.Health.ToString("0.0") + "  生命 " + Percent(state.Body.Vitality) + "\n结构 " + Percent(state.Body.Integrity) + "  机能 " + Percent(state.Body.Function) + "\n\n状态牌可单独拖动、选择，并作为护理配方的目标。普通等待不消耗饱食和精神；未处理异常仍随时间发展。";
        }
        private void ShowGoals()
        {
            Modal("营地目标", "最早第" + rules.Goal.MinimumDay + "天清晨检查。\n最近" + rules.Goal.WindowDays + "天每天净新增食物≥" + rules.Goal.DailyFood + "，合计至少" + rules.Goal.SourceCount + "种来源；堆肥≥" + rules.Goal.CompostCount + "轮；每日清晨有机废物≤" + rules.Goal.MaxWaste + "。\n曾制成骨刀；储备食物营养≥" + rules.Goal.StoredFood + "，剩余保质至少" + Hours(rules.Goal.MinimumShelfMinutes) + "。\n健康≥" + Percent(rules.Goal.MinimumHealth) + "，生命≥" + Percent(rules.Goal.MinimumVital) + "，精神≥" + Percent(rules.Goal.MinimumMind) + "。\n口粮不计产出，熟制只记新增营养，干燥不重复计数。");
            foreach (DailyRecord day in state.Days.OrderByDescending(d => d.Day).Take(4)) Row(optionContent, optionRows, "第" + day.Day + "天", "食物+" + day.Food.ToString("0.#") + "  堆肥" + day.Compost + "  来源" + day.Sources.Count + "  清晨废物" + day.Waste, () => { }, false);
            foreach (RecipeRule recipe in rules.Recipes.Where(r => r.Kind == RecipeKind.Expand)) Row(optionContent, optionRows, "扩建生产区", "当前容量" + state.Capacity + "；需相应收获/繁殖里程碑。", () => PickRecipe(recipe.Id));
        }
        private void ShowHelp() => Modal("操作帮助", "单击选中牌，在右侧查看状态、内容物和可执行动作。\n材料拖到同类材料可合堆；保质期分批保留。拖到建筑选择投料、收纳或工艺，多个动作不会自动选第一项。\n花圃肥料、围栏饲料、自动加工原料须放入对应输入槽；材料保留量会阻止新周期动用。\n状态牌独立出现在下方桌面；把敷料或护理材料拖到状态牌，选择工艺后核对预览并开始。治疗只针对这张牌关联的具体伤口。\n任务开始扣本轮材料与耐久，并预留产出位置。批量按轮继续；中止不退当前轮，后续未开始的轮次不扣费。\n精神不足会暂停劳动；选择休息可暂放当前工作，休息结束继续。\n卡牌可以拖动，区域整体移动不打断任务；移动正在工作的生物须先中止。\n空格暂停/继续，Esc关闭窗口或打开菜单。保存与返回在菜单；读取后时间暂停。\n健康、生命由伤口、异常和事件影响。危险采集与接触作业在预览和状态记录中提示来源。\n自动加工缺料、结构损坏或出口满会等待，已经开工的产出位会保留。清理内容物可恢复区域条件。");
        private string RecipeSummary(RecipeRule recipe)
        {
            string input = string.Join("、", recipe.Inputs.Select(a => string.Join("/", a.Alternatives.Select(Name)) + "×" + a.Count + (a.Consumed ? "" : "（不消耗）")));
            return (input == "" ? "无材料" : input) + (recipe.Outputs.Count > 0 ? " → " + Amounts(recipe.Outputs) : "") + " · " + Hours(recipe.Minutes) + (recipe.AutomaticMinutes > 0 ? "+" + Hours(recipe.AutomaticMinutes) + "自动" : recipe.Human ? "人力" : "自动");
        }
        private string Amounts(List<Amount> amounts) => string.Join("、", amounts.GroupBy(a => a.DefinitionId).Select(g => Name(g.Key) + "×" + g.Sum(a => a.Count)));
        private string Name(string id) => Rule(id)?.Name ?? id;
        private static string Percent(double value) => (value * 100).ToString("0") + "%";
        private static string Hours(double minutes) => minutes >= 60 ? (minutes / 60).ToString("0.#") + "小时" : Math.Ceiling(minutes).ToString("0") + "分钟";
        private static string PlaceName(Place place)
        {
            switch (place) { case Place.Input: return "输入"; case Place.Output: return "产出"; case Place.Plant: return "种植槽"; case Place.Adult: return "成年槽"; case Place.Young: return "幼体槽"; case Place.Storage: return "储物"; case Place.Residue: return "腐败残余"; default: return ""; }
        }
        private void LateUpdate()
        {
            if (engine == null || EventSystem.current == null) return;
            GameObject scope = confirmationPanel.activeSelf ? confirmationPanel : menuPanel.activeSelf ? menuPanel : relationsPanel.activeSelf ? relationsPanel : modalPanel.activeSelf ? modalPanel : logicalDesktop.gameObject;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            bool invalid = selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(scope.transform);
            if (scope == navigationScope && !navigationDirty && !invalid) return;
            navigationScope = scope; navigationDirty = false;
            foreach (Selectable control in GetComponentsInChildren<Selectable>(true))
            {
                Navigation navigation = control.navigation;
                navigation.mode = control.isActiveAndEnabled && control.transform.IsChildOf(scope.transform) ? Navigation.Mode.Automatic : Navigation.Mode.None;
                control.navigation = navigation;
            }
            if (invalid)
            {
                Selectable first = scope.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.IsInteractable() && s.isActiveAndEnabled);
                EventSystem.current.SetSelectedGameObject(first == null ? null : first.gameObject);
            }
        }
    }
}
