using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardKKK.IslandSurvival.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.IslandSurvival.Unity
{
    public sealed partial class VoyageView : MonoBehaviour
    {
        [SerializeField] private RectTransform tabletop, tabletopViewport, stageCards, stageDrop, taskRows, dragLayer;
        [SerializeField] private ScrollRect tableScroll, stageScroll, taskScroll, readScroll;
        [SerializeField] private GameObject editorPreview, emptyTasks, menuPanel, readPanel;
        [SerializeField] private Text clockText, stageCount, stageStatus, taskCount, cardTitle, cardDetail, noticeText;
        [SerializeField] private Text readTitle, readText, menuTitle, menuNotice;
        [SerializeField] private Button pauseButton, menuButton, clearButton, startButton, readClose;
        [SerializeField] private Button menuClose, saveButton, loadButton, restartButton, returnButton;
        [SerializeField] private Image tableTone;
        [SerializeField] private VoyageCardView cardTemplate;
        [SerializeField] private VoyageTaskView taskTemplate;
        [SerializeField] private SurvivalTheme theme;
        [SerializeField] private Shader desaturateShader;
        [SerializeField] private AudioSource feedback;
        [SerializeField] private AudioClip dragPaper;
        [SerializeField] private CanvasGroup playInteraction;

        [SerializeField] private Text preparationTitle;
        [SerializeField] private Button closePreparation, generalPreparation, tasksButton;
        [SerializeField] private GameObject tasksPanel;
        [SerializeField] private GameObject preparationPanel;
        private bool preparationVisible;
        private bool InOtherPreparation(int id) => state.Workspaces.Any(workspace => workspace.Location != state.PreparationLocation &&
            workspace.Slots.Any(slot => slot.CardId == id));

        private void OpenPreparation(int id)
        {
            preparationVisible = true;
            guidanceSlot = -1;
            tasksPanel.SetActive(false);
            Command(value => value.OpenPreparation(id));
            Render();
            PositionPreparation(id);
        }

        private void PositionPreparation(int id)
        {
            RectTransform panel = (RectTransform)preparationPanel.transform;
            RectTransform parent = (RectTransform)panel.parent;
            Vector2 point = new Vector2(780, 160);
            if (cardViews.TryGetValue(id, out VoyageCardView card))
            {
                Vector3 local = parent.InverseTransformPoint(card.Rect.TransformPoint(card.Rect.rect.center));
                point = new Vector2(local.x - parent.rect.xMin + 76, parent.rect.yMax - local.y - 70);
            }
            panel.anchoredPosition = new Vector2(
                Mathf.Clamp(point.x, 18, 1600 - panel.rect.width - 18),
                -Mathf.Clamp(point.y, 88, 810 - panel.rect.height));
        }

        private static readonly Color Selection = new Color(.98f, .81f, .48f);
        private static readonly Color Related = new Color(.46f, .78f, .61f);
        private static readonly Color LinkedLimb = new Color(.90f, .62f, .43f);
        private static readonly Color StageBorder = new Color(.42f, .59f, .54f);
        private static readonly Color NormalText = new Color(.66f, .76f, .69f);
        private static readonly Color InJobBorder = new Color(.96f, .72f, .30f);
        private static readonly Color[] CardColors =
        {
            new Color(.20f,.33f,.35f), new Color(.24f,.36f,.29f), new Color(.28f,.32f,.28f),
            new Color(.32f,.36f,.24f), new Color(.27f,.30f,.32f), new Color(.38f,.27f,.21f),
            new Color(.27f,.30f,.35f), new Color(.43f,.23f,.24f), new Color(.35f,.25f,.40f),
            new Color(.37f,.32f,.23f), new Color(.29f,.36f,.35f), new Color(.31f,.40f,.30f)
        };
        private static readonly string[] CategoryMarks = { "身", "野", "设", "生", "材", "食", "具", "伤", "念", "记", "船", "航" };

        private readonly Dictionary<int, VoyageCardView> cardViews = new Dictionary<int, VoyageCardView>();
        private readonly Dictionary<int, VoyageTaskView> taskViews = new Dictionary<int, VoyageTaskView>();
        private readonly Dictionary<string, Sprite> artwork = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private Dictionary<int, VoyageCardState> cardById = new Dictionary<int, VoyageCardState>();
        private readonly HashSet<int> preparedFollowers = new HashSet<int>();
        private Dictionary<string, VoyageCardRule> cards;
        private Dictionary<string, VoyageRecipeRule> recipes;
        private Dictionary<string, VoyageAuxiliaryRule> auxiliaries;
        private VoyageEngine engine;
        private VoyageState state;
        private Action<Func<VoyageEngine, VoyageResult>> run;
        private Action save, load, restart, returnToTable;
        private Text pauseLabel, startLabel;
        private VoyageCardView dragging;
        private Material subduedArtwork;
        private int selected, hovered;
        private bool resumeAfterMenu, confirmRestart;
        private Color boardColor;
        private string notice = "";
        public RectTransform DragLayer => dragLayer;

        private void Awake()
        {
            pauseLabel = pauseButton.GetComponentInChildren<Text>();
            startLabel = startButton.GetComponentInChildren<Text>();
            boardColor = tableTone.color;
            tableScroll.onValueChanged.AddListener(_ => { if (dragging != null && dragPointer != null) PreviewDrop(dragging, dragPointer); });
            stageScroll.onValueChanged.AddListener(_ => { if (dragging != null && dragPointer != null) PreviewDrop(dragging, dragPointer); });
            if (desaturateShader != null) subduedArtwork = new Material(desaturateShader) { hideFlags = HideFlags.HideAndDontSave };
            pauseButton.onClick.AddListener(TogglePause);
            menuButton.onClick.AddListener(OpenMenu);
            tasksButton.onClick.AddListener(() =>
            {
                bool show = !tasksPanel.activeSelf;
                tasksPanel.SetActive(show);
                if (show) { preparationVisible = false; preparationPanel.SetActive(false); guidanceSlot = -1; RefreshGuidance(); }
            });
            generalPreparation.onClick.AddListener(() => OpenPreparation(0));
            closePreparation.onClick.AddListener(() => { preparationVisible = false; guidanceSlot = -1; Render(); });
            clearButton.onClick.AddListener(() => Command(value => value.ClearStage()));
            startButton.onClick.AddListener(() => Command(value => value.Start()));
            readClose.onClick.AddListener(CloseRead);
            menuClose.onClick.AddListener(CloseMenu);
            saveButton.onClick.AddListener(() => save?.Invoke());
            loadButton.onClick.AddListener(() => { resumeAfterMenu = false; load?.Invoke(); });
            restartButton.onClick.AddListener(Restart);
            returnButton.onClick.AddListener(() => returnToTable?.Invoke());
        }

        public void Bind(VoyageEngine value, VoyageConfiguration configuration,
            Action<Func<VoyageEngine, VoyageResult>> execute, Action saveGame, Action loadGame, Action restartGame, Action leave)
        {
            AbortDrag();
            StopAllFlightAnimations();
            engine = value;
            run = execute;
            save = saveGame;
            load = loadGame;
            restart = restartGame;
            returnToTable = leave;
            VoyageDefinition rules = configuration.BuildDefinition();
            cards = rules.Cards.ToDictionary(card => card.Id, StringComparer.Ordinal);
            recipes = rules.Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
            auxiliaries = rules.AuxiliaryLines.ToDictionary(line => line.Id, StringComparer.Ordinal);
            deskRules = rules.Desk;
            artwork.Clear();
            foreach (VoyageCardRule rule in rules.Cards) artwork.Add(rule.Id, configuration.FindArtwork(rule.Id));
            foreach (VoyageCardView card in cardViews.Values) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            foreach (VoyageTaskView job in taskViews.Values) { job.gameObject.SetActive(false); Destroy(job.gameObject); }
            cardViews.Clear();
            taskViews.Clear();
            foreach (VoyageSlotView slot in slotViews.Values) Destroy(slot.gameObject);
            slotViews.Clear();
            guidanceSlot = -1;
            panelTime = 0;
            if (preparationFade != null) preparationFade.alpha = 0;
            preparationPanel.SetActive(false);
            state = null;
            selected = hovered = 0;
            preparationVisible = false;
            tasksPanel.SetActive(false);
            resumeAfterMenu = false;
            editorPreview.SetActive(false);
            readPanel.SetActive(false);
            menuPanel.SetActive(false);
            RefreshInteraction();
            tableScroll.verticalNormalizedPosition = 1;
            stageScroll.verticalNormalizedPosition = 1;
            taskScroll.verticalNormalizedPosition = 1;
            Message("");
            Render();
        }

        public void Render()
        {
            if (engine == null) return;
            VoyageState previous = state;
            state = engine.Snapshot;
            if (dragOffsets.Count > 0 && dragging == null) FinishDragSession();
            cardById = state.Cards.ToDictionary(card => card.Id);
            IReadOnlyList<int> followers = engine.PreparedFollowers;
            preparedFollowers.Clear();
            preparedFollowers.UnionWith(followers);
            if (!cardById.ContainsKey(selected)) selected = 0;
            if (!cardById.ContainsKey(hovered)) hovered = 0;
            if (dragging != null && (!cardById.TryGetValue(dragging.CardId, out VoyageCardState dragged)
                || dragged.JobId != 0 || preparedFollowers.Contains(dragging.CardId) || state.Dead || state.Won))
                AbortDrag();

            ShowPreparation();
            preparationTitle.text = state.PreparationLocation == 0 ? "桌面组合" : CardName(state.PreparationLocation);
            bool ended = state.Dead || state.Won;
            int minuteOfDay = state.Minute % 1440;
            clockText.text = $"第 {state.Minute / 1440 + 1} 天  {minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
            pauseLabel.text = state.Paused ? "继续" : "暂停";
            pauseButton.interactable = !ended;
            bool hasInputs = engine.Slots(state.PreparationLocation).Any(slot => slot.CardId != 0);
            clearButton.interactable = !ended && hasInputs;
            startButton.interactable = !ended && hasInputs;
            stageCount.text = (state.Staged.Count(id => id != state.PreparationLocation) + followers.Count) + " 张";
            taskCount.text = state.Jobs.Count.ToString();
            emptyTasks.SetActive(state.Jobs.Count == 0);

            VoyageOffer staged = engine.Preview();
            VoyageOffer hint = state.Staged.Count == 0 && IsAvailable(selected) ? engine.Preview(selected) : staged;
            startLabel.text = staged.Match == VoyageMatch.Ready || state.Staged.Count == 0 ? "开始" : "试一试";
            DisplayStage(hint);
            DisplaySlots();
            DisplayCards(new HashSet<int>(hint.RelatedCardIds), followers, previous);
            RefreshGuidance();
            DisplayJobs();
            DisplayDetails();

            if (ended)
            {
                stageStatus.text = state.EndReason;
                stageStatus.color = state.Won ? Selection : new Color(.96f, .54f, .42f);
                if (previous != null && !previous.Dead && !previous.Won)
                {
                    PlayFeedback(state.Won ? theme?.Success : theme?.Warning);
                    if (state.Won)
                    {
                        VoyageCardState ending = state.Cards.FirstOrDefault(card => cards[card.CardId].Kind == VoyageCardKind.Ending);
                        if (ending != null) Reveal(ending);
                    }
                }
            }
            else if (previous != null && state.Minute > previous.Minute
                && previous.Jobs.Any(old => !state.Jobs.Any(current => current.Id == old.Id)))
            {
                PlayFeedback(theme?.Success);
                TriggerFlyoutAnimations(previous, state);
            }
        }

        public void Message(string message)
        {
            notice = message ?? "";
            noticeText.text = notice;
            if (menuPanel.activeSelf && !confirmRestart) menuNotice.text = notice;
        }

        public void Reject() => PlayFeedback(theme?.Denied);

        internal void Select(int id, int clicks)
        {
            if (state == null || menuPanel.activeSelf || readPanel.activeSelf || !cardById.TryGetValue(id, out VoyageCardState card)) return;
            selected = id;
            VoyageCardKind selectedKind = cards[card.CardId].Kind;
            if (engine.IsLocation(id) && (selectedKind != VoyageCardKind.Information || clicks < 2 && !engine.IsInSlot(id)))
            {
                OpenPreparation(id);
                return;
            }
            if (preparedFollowers.Contains(id))
            {
                Command(value => value.Stage(id));
                return;
            }
            if (cardViews.TryGetValue(id, out VoyageCardView view) && !state.Staged.Contains(id)) view.transform.SetAsLastSibling();
            VoyageCardRule rule = cards[card.CardId];
            if (rule.Kind == VoyageCardKind.Information || rule.Kind == VoyageCardKind.Ending || clicks > 1 && !string.IsNullOrEmpty(rule.Description))
                OpenRead(card, rule);
            Render();
        }

        internal void Hover(int id, bool inside)
        {
            if (state == null) return;
            if (inside) hovered = id;
            else if (hovered == id) hovered = 0;
            DisplayDetails();
        }

        internal void QuickAction(int id)
        {
            if (state == null || !cardById.TryGetValue(id, out VoyageCardState card) || card.JobId != 0) return;
            preparationVisible = true;
            if (engine.IsInSlot(id)) Command(value => value.Unstage(id));
            else if (IsAvailable(id)) Command(value => value.Stage(id));
        }

        internal void RemoveFromStage(int id) => QuickAction(id);

        private void DisplayStage(VoyageOffer offer)
        {
            int busyJob = cardById.TryGetValue(state.PreparationLocation, out VoyageCardState place) ? place.JobId : 0;
            VoyageJobState running = busyJob == 0 ? null : state.Jobs.FirstOrDefault(job => job.Id == busyJob);
            if (running != null)
            {
                stageStatus.color = InJobBorder;
                stageStatus.text = recipes[running.RecipeId].Name + " · 剩余 " + running.Remaining + " 分钟";
                return;
            }
            stageStatus.color = NormalText;
            if (offer.Match == VoyageMatch.Ready)
            {
                stageStatus.text = offer.Name + " · 条件齐备";
                stageStatus.color = Related;
            }
            else if (offer.Match == VoyageMatch.Incomplete)
            {
                stageStatus.text = offer.Name + " · " + offer.Detail;
                stageStatus.color = Selection;
            }
            else if (state.Staged.Count == 0 && !IsAvailable(selected)) stageStatus.text = "将卡牌拖入组合，或将卡牌拖到设施上。";
            else stageStatus.text = "未发现已知组合";
        }

        private void DisplayCards(HashSet<int> related, IReadOnlyList<int> followers, VoyageState previous)
        {
            float dread = Mathf.InverseLerp(75, 5, (float)state.San);
            tableTone.color = Fade(boardColor, dread);
            if (subduedArtwork != null) subduedArtwork.SetFloat("_Desaturation", dread * .9f);
            var stagedIndex = new Dictionary<int, int>();
            foreach (VoyageSlotOffer slot in engine.Slots(state.PreparationLocation))
                if (slot.CardId != 0) stagedIndex.Add(slot.CardId, slot.Index);
            int followerIndex = engine.Slots(state.PreparationLocation).Count;
            foreach (int id in followers) stagedIndex.Add(id, followerIndex++);
            int linkedLimb = cardById.TryGetValue(selected, out VoyageCardState selectedCard) ? selectedCard.Limb : 0;
            SetHeight(stageCards, Mathf.Max(190, Mathf.CeilToInt((engine.Slots(state.PreparationLocation).Count + followers.Count) / 3f) * 180 + 8));
            foreach (int id in cardViews.Keys.Where(id => !cardById.ContainsKey(id)).ToArray())
            {
                VoyageCardView leaving = cardViews[id];
                if (leaving != null && leaving.gameObject.activeInHierarchy)
                {
                    Vector3 destination = mergeTarget != 0 && cardViews.TryGetValue(mergeTarget, out VoyageCardView target) && target != null
                        ? target.Rect.position : leaving.Rect.position;
                    leaving.RetireTo(destination);
                    retiredCards.Add(leaving);
                }
                else if (leaving != null) Destroy(leaving.gameObject);
                cardViews.Remove(id);
            }
            retiredCards.RemoveAll(card => card == null);

            var jobById = state.Jobs.ToDictionary(job => job.Id);

            foreach (VoyageCardState card in state.Cards.OrderBy(StackDepth).ThenBy(card => card.Id))
            {
                cardViews.TryGetValue(card.Id, out VoyageCardView view);
                VoyageJobState job = null;
                bool inJob = card.JobId != 0 && jobById.TryGetValue(card.JobId, out job);
                bool staged = stagedIndex.TryGetValue(card.Id, out int index) && card.Id != state.PreparationLocation;
                bool parked = InOtherPreparation(card.Id);
                RectTransform parent = staged ? stageCards : tabletop;
                bool created = view == null;
                if (view == null)
                {
                    view = Instantiate(cardTemplate, parent, false);
                    cardViews[card.Id] = view;
                }
                if (!view.Dragging)
                {

                    Vector2 position;
                    if (staged)
                    {
                        position = SlotPosition(index);
                    }
                    else if (inJob && job.Location != 0 && job.Location != card.Id)
                    {
                        if (cardById.TryGetValue(job.Location, out VoyageCardState loc))
                        {
                            int inputIndex = job.Inputs.FindIndex(inp => inp.CardId == card.Id);
                            if (inputIndex < 0) inputIndex = 0;
                            Vector2 locPos = TablePosition(loc, view.Rect);
                            position = locPos + new Vector2(24 + inputIndex * 24, -20 - inputIndex * 16);
                        }
                        else
                        {
                            position = DesktopPosition(card, view.Rect);
                        }
                    }
                    else
                    {
                        position = DesktopPosition(card, view.Rect);
                    }

                    bool returning = view.Rect.parent != parent || card.JobId == 0 && previous != null && previous.Cards.Any(old => old.Id == card.Id && old.JobId != 0);
                    view.LayoutTo(parent, position, !created, staged ? 24f : returning ? 32f : 0f);
                    if (!view.Dragging) view.transform.SetAsLastSibling();
                }
                VoyageCardRule rule = cards[card.CardId];
                bool head = rule.Kind == VoyageCardKind.Body && rule.Tags.Contains("head");
                bool torso = rule.Kind == VoyageCardKind.Body && rule.Tags.Contains("torso");
                bool vivid = rule.Tags.Contains("san_colour");
                Color color = CardColors[(int)rule.Kind];
                if (!vivid) color = Fade(color, dread);

                Color frame;
                if (selected == card.Id) frame = Selection;
                else if (inJob || parked) frame = InJobBorder;
                else if (related.Contains(card.Id)) frame = Related;
                else if (card.Id == linkedLimb) frame = LinkedLimb;
                else if (staged) frame = StageBorder;
                else frame = Color.Lerp(color, Color.black, .24f);

                float water = head ? (float)state.San / 100f : torso ? (float)state.Hunger / 100f : -1;
                float wear = rule.Durability > 0 ? 1 - Mathf.Clamp01((float)card.Durability / rule.Durability) : 0;
                bool textCard = rule.Kind == VoyageCardKind.Information || rule.Kind == VoyageCardKind.Ending;
                string mark = textCard ? Shorten((rule.Description ?? "").Replace('\n', ' '), 21)
                    : rule.Kind == VoyageCardKind.Body ? rule.Name : CategoryMarks[(int)rule.Kind];
                bool followsLimb = preparedFollowers.Contains(card.Id);

                string statusText = inJob
                    ? (job.Location == card.Id ? $"进行中 · {job.Remaining}分" : $"工作 · {job.Remaining}分")
                    : parked ? "已放入别处" : ShortStatus(card, rule, head, torso, followsLimb);
                if (!inJob && rule.Kind == VoyageCardKind.Information && card.TimerRemaining == 0 && engine.IsLocation(card.Id)) statusText = "双击查看";
                if (!state.Cards.Any(member => member.StackParent == card.Id))
                {
                    int root = card.Id;
                    while (cardById[root].StackParent != 0) root = cardById[root].StackParent;
                    int stackCount = engine.DragCards(root, false).Count;
                    if (stackCount > 1) statusText = stackCount + " 张  " + statusText;
                }

                view.Display(this, card.Id, rule.Name, mark, statusText, textCard, artwork[rule.Id],
                    vivid || dread == 0 ? null : subduedArtwork, color, frame, water,
                    head ? new Color(.25f, .68f, .66f) : new Color(.86f, .55f, .25f), wear);
                view.SetProgress(inJob ? 1f - (float)job.Remaining / job.Duration : -1);
                bool visible = !parked && (!staged || preparationVisible);
                if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
            }
        }

        private Vector2 TablePosition(VoyageCardState card, RectTransform view)
        {
            Vector2 board = tabletop.rect.size;
            Vector2 half = view.rect.size / 2;
            return new Vector2(Mathf.Clamp(card.X * board.x, half.x + 4, board.x - half.x - 4) - half.x,
                -Mathf.Clamp(card.Y * board.y, half.y + 4, board.y - half.y - 4) + half.y);
        }

        private void TriggerFlyoutAnimations(VoyageState previous, VoyageState current)
        {
            foreach (VoyageCardState produced in current.Cards.Where(card => card.ProducedByJobId != 0 && !previous.Cards.Any(old => old.Id == card.Id)))
            {
                if (!cardViews.TryGetValue(produced.Id, out VoyageCardView view) || view.Dragging) continue;
                Vector2 destination = DesktopPosition(produced, view.Rect);
                var origin = new VoyageCardState { X = produced.ProducedFromX, Y = produced.ProducedFromY };
                view.SnapTo(tabletop, TablePosition(origin, view.Rect));
                view.LayoutTo(tabletop, destination, true, 45f);
            }
        }

        private void StopAllFlightAnimations()
        {
            foreach (VoyageCardView card in cardViews.Values) if (card != null) card.StopMotion();
            foreach (VoyageCardView card in retiredCards) if (card != null) Destroy(card.gameObject);
            retiredCards.Clear();
        }

        private void DisplayJobs()
        {
            var live = new HashSet<int>(state.Jobs.Select(job => job.Id));
            foreach (int id in taskViews.Keys.Where(id => !live.Contains(id)).ToArray())
            {
                taskViews[id].gameObject.SetActive(false);
                Destroy(taskViews[id].gameObject);
                taskViews.Remove(id);
            }
            SetHeight(taskRows, Mathf.Max(235, 6 + state.Jobs.Count * 76));
            for (int i = 0; i < state.Jobs.Count; i++)
            {
                VoyageJobState job = state.Jobs[i];
                if (!taskViews.TryGetValue(job.Id, out VoyageTaskView view))
                {
                    view = Instantiate(taskTemplate, taskRows, false);
                    int id = job.Id;
                    view.Bind(() => Command(value => value.Cancel(id)));
                    taskViews.Add(id, view);
                }
                string title = state.Known.Contains(job.RecipeId) ? recipes[job.RecipeId].Name : "尝试组合";
                List<int> held = job.Inputs.Select(input => input.CardId).ToList();
                if (job.Location != 0) held.Insert(0, job.Location);
                bool HoldsRole(string role) => held.Any(id => cardById.TryGetValue(id, out VoyageCardState card) && cards[card.CardId].Tags.Contains(role));
                view.Display(title, Shorten(string.Join(" · ", held.Select(CardName).Where(name => name.Length != 0)), 23),
                    job.Remaining, job.Duration, state.Paused, !state.Dead && !state.Won,
                    HoldsRole("head") ? (float)state.San / 100f : -1, HoldsRole("torso") ? (float)state.Hunger / 100f : -1);
                view.Rect.anchoredPosition = new Vector2(5, -3 - i * 76);
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);
            }
        }

        private void DisplayDetails()
        {
            if (state == null) return;
            int id = hovered != 0 ? hovered : selected;
            if (!cardById.TryGetValue(id, out VoyageCardState card))
            {
                cardTitle.text = "";
                cardDetail.text = "";
                return;
            }
            VoyageCardRule rule = cards[card.CardId];
            if (card.JobId != 0)
            {
                VoyageJobState job = state.Jobs.FirstOrDefault(j => j.Id == card.JobId);
                string jobName = job != null && state.Known.Contains(job.RecipeId) ? recipes[job.RecipeId].Name : "组合工作";
                cardTitle.text = rule.Name + " · [进行中]";
                int rem = job != null ? job.Remaining : 0;
                int dur = job != null ? job.Duration : 1;
                cardDetail.text = $"正在进行：{jobName} · 剩余 {rem}/{dur} 分钟 · 任务进行中卡牌已锁定";
                return;
            }
            cardTitle.text = rule.Name;
            var parts = new List<string>();
            if (rule.Durability > 0) parts.Add($"耐久 {card.Durability}/{rule.Durability}");
            if (card.Limb != 0) parts.Add("关联" + CardName(card.Limb));
            if (preparedFollowers.Contains(id)) parts.Add("跟随 · 点击选为护理目标");
            if (card.RechargeRemaining > 0) parts.Add($"恢复 {card.RechargeRemaining} 分钟");
            else if (rule.Charges > 0) parts.Add($"可采集 {card.Charges} 次");
            if (card.TimerRemaining > 0) parts.Add($"剩余 {card.TimerRemaining} 分钟");
            if (rule.Kind == VoyageCardKind.Information || rule.Kind == VoyageCardKind.Ending) parts.Add(engine.IsLocation(card.Id) ? "双击查看" : "点击查看");
            else if (!string.IsNullOrEmpty(rule.Description)) parts.Add(Shorten(rule.Description.Replace('\n', ' '), 24));
            cardDetail.text = string.Join("　·　", parts);
        }

        private static string ShortStatus(VoyageCardState card, VoyageCardRule rule, bool head, bool torso, bool followsLimb)
        {
            if (head) return "SAN";
            if (torso) return "饥饿";
            if (rule.Durability > 0 && card.Durability == 0) return "损坏";
            if (card.RechargeRemaining > 0) return $"恢复 {card.RechargeRemaining} 分钟";
            if (rule.Charges > 0) return $"可采 {card.Charges} 次";
            if (followsLimb) return card.TimerRemaining > 0 ? $"随同 · {card.TimerRemaining} 分钟" : "随同";
            if (card.TimerRemaining > 0) return $"{card.TimerRemaining} 分钟";
            if (rule.Kind == VoyageCardKind.Information || rule.Kind == VoyageCardKind.Ending) return "点击查看";
            return "";
        }

        private void Command(Func<VoyageEngine, VoyageResult> command)
        {
            if (run == null) return;
            Message("");
            run(command);
        }

        private void TogglePause()
        {
            if (state == null || state.Dead || state.Won || menuPanel.activeSelf) return;
            Command(value => value.SetPaused(!state.Paused));
        }

        private void OpenMenu()
        {
            if (menuPanel.activeSelf) return;
            if (dragging != null) { AbortDrag(); Render(); }
            CloseRead();
            resumeAfterMenu = state != null && !state.Paused && !state.Dead && !state.Won;
            if (resumeAfterMenu) Command(value => value.SetPaused(true));
            confirmRestart = false;
            menuTitle.text = "暂停";
            menuNotice.text = notice;
            menuPanel.SetActive(true);
            RefreshInteraction();
            Focus(menuClose);
        }

        private void CloseMenu()
        {
            menuPanel.SetActive(false);
            RefreshInteraction();
            if (resumeAfterMenu && state != null && !state.Dead && !state.Won) Command(value => value.SetPaused(false));
            resumeAfterMenu = false;
            confirmRestart = false;
            Focus(menuButton);
        }

        private void Restart()
        {
            if (!confirmRestart)
            {
                confirmRestart = true;
                menuNotice.text = "再次点击重新开始，当前进度会结束。";
                return;
            }
            confirmRestart = false;
            StopAllFlightAnimations();
            restart?.Invoke();
        }

        private void OpenRead(VoyageCardState card, VoyageCardRule rule)
        {
            readTitle.text = rule.Name;
            var body = new List<string>();
            if (!string.IsNullOrWhiteSpace(rule.Description)) body.Add(rule.Description);
            foreach (string id in rule.GrantsRecipes)
            {
                if (!recipes.TryGetValue(id, out VoyageRecipeRule recipe)) continue;
                var inputs = new List<string>();
                if (recipe.LocationId.Length > 0 && cards.TryGetValue(recipe.LocationId, out VoyageCardRule location)) inputs.Add(location.Name);
                foreach (VoyageInputRule input in recipe.Inputs)
                {
                    string name = cards.TryGetValue(input.CardId, out VoyageCardRule exact) ? exact.Name
                        : string.Join("或", cards.Values.Where(candidate => candidate.Tags.Contains(input.Tag)).OrderBy(candidate => candidate.SortRank)
                            .Select(candidate => candidate.Name).Distinct().Take(5));
                    if (name.Length > 0) inputs.Add(input.Quantity == 1 ? name : name + " × " + input.Quantity);
                }
                body.Add(recipe.Name + "\n" + string.Join(" ＋ ", inputs));
            }
            if (card.TimerRemaining > 0) body.Add($"剩余 {card.TimerRemaining} 分钟");
            readText.text = string.Join("\n\n", body);
            readPanel.SetActive(true);
            RefreshInteraction();
            Canvas.ForceUpdateCanvases();
            readScroll.verticalNormalizedPosition = 1;
            Focus(readClose);
        }

        private void CloseRead()
        {
            if (!readPanel.activeSelf) return;
            readPanel.SetActive(false);
            RefreshInteraction();
            Focus(null);
        }

        private void Update()
        {
            TickDesktop();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (dragging != null) { AbortDrag(); Render(); }
                else if (readPanel.activeSelf) CloseRead();
                else if (menuPanel.activeSelf) CloseMenu();
                else if (tasksPanel.activeSelf) tasksPanel.SetActive(false);
                else if (preparationVisible) { preparationVisible = false; guidanceSlot = -1; Render(); }
                else OpenMenu();
            }
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && dragging != null) { AbortDrag(); Render(); }
            if (!focused) { resumeAfterMenu = false; guidanceSlot = -1; RefreshGuidance(); }
        }

        private void OnDisable()
        {
            AbortDrag();
            StopAllFlightAnimations();
        }

        private void OnDestroy()
        {
            AbortDrag();
            StopAllFlightAnimations();
            if (subduedArtwork != null) Destroy(subduedArtwork);
        }

        private void RefreshInteraction()
        {
            bool active = !menuPanel.activeSelf && !readPanel.activeSelf;
            playInteraction.interactable = active;
            playInteraction.blocksRaycasts = active;
        }

        private bool IsAvailable(int id) => id != 0 && cardById.TryGetValue(id, out VoyageCardState card)
            && card.JobId == 0 && !engine.IsInSlot(id) && !preparedFollowers.Contains(id);

        private string CardName(int id) => cardById.TryGetValue(id, out VoyageCardState card) ? cards[card.CardId].Name : "";
        private static string Shorten(string text, int limit) => text.Length <= limit ? text : text.Substring(0, limit) + "…";
        private static Color Fade(Color color, float amount) => Color.Lerp(color, new Color(color.grayscale, color.grayscale, color.grayscale, color.a), amount * .88f);
        private static void Focus(Button button) => EventSystem.current?.SetSelectedGameObject(button == null ? null : button.gameObject);
        private static void SetHeight(RectTransform rect, float height)
        {
            Vector2 size = rect.sizeDelta;
            if (!Mathf.Approximately(size.y, height)) rect.sizeDelta = new Vector2(size.x, height);
        }

        private void Reveal(VoyageCardState card)
        {
            float hidden = tabletop.rect.height - tabletopViewport.rect.height;
            if (hidden > 0) tableScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((card.Y * tabletop.rect.height - tabletopViewport.rect.height / 2) / hidden);
            selected = card.Id;
            DisplayDetails();
        }

        private void PlayFeedback(AudioClip clip)
        {
            if (feedback != null && clip != null) feedback.PlayOneShot(clip, theme != null ? theme.Volume : .25f);
        }
    }
}
