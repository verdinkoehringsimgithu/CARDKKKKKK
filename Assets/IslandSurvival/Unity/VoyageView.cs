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
    public sealed class VoyageView : MonoBehaviour
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
        private readonly Dictionary<int, Coroutine> flyingCards = new Dictionary<int, Coroutine>();
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
            if (desaturateShader != null) subduedArtwork = new Material(desaturateShader) { hideFlags = HideFlags.HideAndDontSave };
            pauseButton.onClick.AddListener(TogglePause);
            menuButton.onClick.AddListener(OpenMenu);
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
            artwork.Clear();
            foreach (VoyageCardRule rule in rules.Cards) artwork.Add(rule.Id, configuration.FindArtwork(rule.Id));
            foreach (VoyageCardView card in cardViews.Values) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            foreach (VoyageTaskView job in taskViews.Values) { job.gameObject.SetActive(false); Destroy(job.gameObject); }
            cardViews.Clear();
            taskViews.Clear();
            state = null;
            selected = hovered = 0;
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
            cardById = state.Cards.ToDictionary(card => card.Id);
            IReadOnlyList<int> followers = engine.PreparedFollowers;
            preparedFollowers.Clear();
            preparedFollowers.UnionWith(followers);
            if (!cardById.ContainsKey(selected)) selected = 0;
            if (!cardById.ContainsKey(hovered)) hovered = 0;
            if (dragging != null && (!cardById.TryGetValue(dragging.CardId, out VoyageCardState dragged)
                || dragged.JobId != 0 || preparedFollowers.Contains(dragging.CardId) || state.Dead || state.Won))
                AbortDrag();

            bool ended = state.Dead || state.Won;
            int minuteOfDay = state.Minute % 1440;
            clockText.text = $"第 {state.Minute / 1440 + 1} 天  {minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
            pauseLabel.text = state.Paused ? "继续" : "暂停";
            pauseButton.interactable = !ended;
            clearButton.interactable = !ended && state.Staged.Count > 0;
            startButton.interactable = !ended && state.Staged.Count > 0;
            stageCount.text = (state.Staged.Count + followers.Count) + " 张";
            taskCount.text = state.Jobs.Count.ToString();
            emptyTasks.SetActive(state.Jobs.Count == 0);

            VoyageOffer staged = engine.Preview();
            VoyageOffer hint = state.Staged.Count == 0 && IsAvailable(selected) ? engine.Preview(selected) : staged;
            startLabel.text = staged.Match == VoyageMatch.Ready || state.Staged.Count == 0 ? "开始" : "试一试";
            DisplayStage(hint);
            DisplayCards(new HashSet<int>(hint.RelatedCardIds), followers);
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
            if (state.Staged.Contains(id)) Command(value => value.Unstage(id));
            else if (IsAvailable(id)) Command(value => value.Stage(id));
        }

        internal void RemoveFromStage(int id) => QuickAction(id);

        internal bool BeginDrag(VoyageCardView card)
        {
            if (state == null || state.Dead || state.Won || dragging != null || menuPanel.activeSelf || readPanel.activeSelf || preparedFollowers.Contains(card.CardId)
                || !cardById.TryGetValue(card.CardId, out VoyageCardState source) || source.JobId != 0) return false;
            dragging = card;
            selected = card.CardId;
            engine.Hold(card.CardId, true);
            PlayFeedback(dragPaper);
            return true;
        }

        internal void Drop(VoyageCardView card, PointerEventData pointer)
        {
            if (dragging != card) return;
            dragging = null;
            int id = card.CardId;
            engine.Hold(id, false);
            if (RectTransformUtility.RectangleContainsScreenPoint(stageDrop, pointer.position, pointer.pressEventCamera))
            {
                Command(value => value.Stage(id));
            }
            else if (RectTransformUtility.RectangleContainsScreenPoint(tabletopViewport, pointer.position, pointer.pressEventCamera))
            {
                int merge = FindMergeTarget(id, pointer);
                int dropTarget = FindDropTargetCard(id, pointer);
                Vector3 local = tabletop.InverseTransformPoint(card.Rect.TransformPoint(card.Rect.rect.center));
                Vector2 size = tabletop.rect.size;
                float x = Mathf.Clamp01(local.x / size.x);
                float y = Mathf.Clamp01(-local.y / size.y);
                if (merge != 0)
                {
                    Command(value => value.Merge(id, merge));
                }
                else if (dropTarget != 0 && CanCombineOnDesk(id, dropTarget))
                {
                    Command(value => value.StagePair(dropTarget, id));
                }
                else
                {
                    if (state.Staged.Contains(id)) Command(value => value.Unstage(id));
                    EnsureClearDropPoint(ref x, ref y, id);
                    Command(value => value.Move(id, x, y));
                }
            }
            Render();
        }

        private int FindDropTargetCard(int id, PointerEventData pointer)
        {
            foreach (RaycastResult hit in RaycastCards(pointer))
            {
                VoyageCardView view = hit.gameObject.GetComponentInParent<VoyageCardView>();
                if (view == null || view.CardId == id || !IsAvailable(view.CardId)) continue;
                return view.CardId;
            }
            return 0;
        }

        private bool CanCombineOnDesk(int sourceId, int targetId)
        {
            if (!cardById.TryGetValue(sourceId, out VoyageCardState src) || !cardById.TryGetValue(targetId, out VoyageCardState tgt)) return false;
            if (src.JobId != 0 || tgt.JobId != 0) return false;
            VoyageCardRule tgtRule = cards[tgt.CardId];
            return tgtRule.Kind == VoyageCardKind.Facility || tgtRule.Kind == VoyageCardKind.Resource || tgtRule.Kind == VoyageCardKind.Material;
        }

        private void EnsureClearDropPoint(ref float x, ref float y, int selfId)
        {
            const float threshold = 0.035f;
            foreach (VoyageCardState other in state.Cards)
            {
                if (other.Id == selfId || other.JobId != 0 || state.Staged.Contains(other.Id)) continue;
                if (Mathf.Abs(other.X - x) < threshold && Mathf.Abs(other.Y - y) < threshold)
                {
                    x = Mathf.Clamp(x + 0.045f, 0.05f, 0.95f);
                    y = Mathf.Clamp(y + 0.035f, 0.05f, 0.95f);
                }
            }
        }

        private int FindMergeTarget(int id, PointerEventData pointer)
        {
            if (!cardById.TryGetValue(id, out VoyageCardState source) || cards[source.CardId].Kind != VoyageCardKind.Injury) return 0;
            foreach (RaycastResult hit in RaycastCards(pointer))
            {
                VoyageCardView view = hit.gameObject.GetComponentInParent<VoyageCardView>();
                if (view == null || view.CardId == id || !IsAvailable(view.CardId)) continue;
                VoyageCardState target = cardById[view.CardId];
                return target.CardId == source.CardId && target.Limb == source.Limb ? view.CardId : 0;
            }
            return 0;
        }

        private static List<RaycastResult> RaycastCards(PointerEventData pointer)
        {
            var results = new List<RaycastResult>();
            if (EventSystem.current != null) EventSystem.current.RaycastAll(pointer, results);
            return results;
        }

        private void AbortDrag()
        {
            if (dragging == null) return;
            engine?.Hold(dragging.CardId, false);
            dragging.FinishVisualDrag();
            dragging = null;
        }

        private void DisplayStage(VoyageOffer offer)
        {
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

        private void DisplayCards(HashSet<int> related, IReadOnlyList<int> followers)
        {
            float dread = Mathf.InverseLerp(75, 5, (float)state.San);
            tableTone.color = Fade(boardColor, dread);
            if (subduedArtwork != null) subduedArtwork.SetFloat("_Desaturation", dread * .9f);
            var stagedIndex = new Dictionary<int, int>();
            for (int i = 0; i < state.Staged.Count; i++) stagedIndex.Add(state.Staged[i], i);
            for (int i = 0; i < followers.Count; i++) stagedIndex.Add(followers[i], state.Staged.Count + i);
            int linkedLimb = cardById.TryGetValue(selected, out VoyageCardState selectedCard) ? selectedCard.Limb : 0;
            SetHeight(stageCards, Mathf.Max(334, Mathf.CeilToInt(stagedIndex.Count / 3f) * 110 + 4));
            foreach (int id in cardViews.Keys.Where(id => !cardById.ContainsKey(id)).ToArray())
            {
                cardViews[id].gameObject.SetActive(false);
                Destroy(cardViews[id].gameObject);
                cardViews.Remove(id);
            }

            var jobById = state.Jobs.ToDictionary(job => job.Id);

            foreach (VoyageCardState card in state.Cards)
            {
                cardViews.TryGetValue(card.Id, out VoyageCardView view);
                VoyageJobState job = null;
                bool inJob = card.JobId != 0 && jobById.TryGetValue(card.JobId, out job);
                bool staged = stagedIndex.TryGetValue(card.Id, out int index);
                RectTransform parent = staged ? stageCards : tabletop;
                if (view == null)
                {
                    view = Instantiate(cardTemplate, parent, false);
                    cardViews.Add(card.Id, view);
                }
                if (!view.Dragging)
                {
                    if (view.Rect.parent != parent) view.Rect.SetParent(parent, false);
                    Vector2 position;
                    if (staged)
                    {
                        position = new Vector2(8 + index % 3 * 144, -5 - index / 3 * 110);
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
                            position = TablePosition(card, view.Rect);
                        }
                    }
                    else
                    {
                        position = TablePosition(card, view.Rect);
                    }

                    if (!flyingCards.ContainsKey(card.Id))
                    {
                        if (view.Rect.anchoredPosition != position) view.Rect.anchoredPosition = position;
                    }
                }
                VoyageCardRule rule = cards[card.CardId];
                bool head = rule.Kind == VoyageCardKind.Body && rule.Tags.Contains("head");
                bool torso = rule.Kind == VoyageCardKind.Body && rule.Tags.Contains("torso");
                bool vivid = rule.Tags.Contains("san_colour");
                Color color = CardColors[(int)rule.Kind];
                if (!vivid) color = Fade(color, dread);

                Color frame;
                if (selected == card.Id) frame = Selection;
                else if (inJob) frame = InJobBorder;
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
                    : ShortStatus(card, rule, head, torso, followsLimb);

                view.Display(this, card.Id, rule.Name, mark, statusText, textCard, artwork[rule.Id],
                    vivid || dread == 0 ? null : subduedArtwork, color, frame, water,
                    head ? new Color(.25f, .68f, .66f) : new Color(.86f, .55f, .25f), wear);
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);
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
            var newCards = current.Cards.Where(c => !previous.Cards.Any(old => old.Id == c.Id)).ToList();
            if (newCards.Count == 0) return;

            foreach (VoyageCardState newCard in newCards)
            {
                if (!cardViews.TryGetValue(newCard.Id, out VoyageCardView view)) continue;
                Vector2 targetPos = TablePosition(newCard, view.Rect);
                Vector2 anchorPos = targetPos + new Vector2(0, 30);
                if (newCard.ProducedByJobId != 0)
                {
                    var origin = new VoyageCardState { X = newCard.ProducedFromX, Y = newCard.ProducedFromY };
                    anchorPos = TablePosition(origin, view.Rect);
                }
                if (flyingCards.ContainsKey(newCard.Id))
                {
                    if (flyingCards[newCard.Id] != null) StopCoroutine(flyingCards[newCard.Id]);
                    flyingCards.Remove(newCard.Id);
                }
                Coroutine routine = StartCoroutine(FlyCard(newCard.Id, view, anchorPos, targetPos, 0.30f));
                flyingCards.Add(newCard.Id, routine);
            }
        }

        private IEnumerator FlyCard(int cardId, VoyageCardView view, Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            view.Rect.anchoredPosition = from;
            while (elapsed < duration)
            {
                if (view == null || view.Dragging || state.Staged.Contains(cardId)) break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curvedT = Mathf.Sin(t * Mathf.PI * 0.5f);
                Vector2 pos = Vector2.Lerp(from, to, curvedT);
                pos.y += 45f * Mathf.Sin(t * Mathf.PI);
                if (view != null) view.Rect.anchoredPosition = pos;
                yield return null;
            }
            if (view != null && !view.Dragging && !state.Staged.Contains(cardId)) view.Rect.anchoredPosition = to;
            flyingCards.Remove(cardId);
        }

        private void StopAllFlightAnimations()
        {
            foreach (var pair in flyingCards)
            {
                if (pair.Value != null) StopCoroutine(pair.Value);
                if (cardViews.TryGetValue(pair.Key, out VoyageCardView v) && cardById.TryGetValue(pair.Key, out VoyageCardState s))
                {
                    v.Rect.anchoredPosition = TablePosition(s, v.Rect);
                }
            }
            flyingCards.Clear();
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
            if (rule.Kind == VoyageCardKind.Information || rule.Kind == VoyageCardKind.Ending) parts.Add("点击查看");
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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (dragging != null) { AbortDrag(); Render(); }
                else if (readPanel.activeSelf) CloseRead();
                else if (menuPanel.activeSelf) CloseMenu();
                else OpenMenu();
            }
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && dragging != null) { AbortDrag(); Render(); }
            if (!focused) resumeAfterMenu = false;
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
            && card.JobId == 0 && !state.Staged.Contains(id) && !preparedFollowers.Contains(id);

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
