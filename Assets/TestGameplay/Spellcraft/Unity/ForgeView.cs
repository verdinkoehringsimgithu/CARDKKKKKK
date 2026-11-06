using System;
using System.Collections.Generic;
using System.Text;
using CardKKK.Spellcraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Spellcraft.Unity
{
    /// <summary>序列化法术桌面。只读取快照、发送命令意图并显示核心已提交的结果。</summary>
    public sealed class ForgeView : MonoBehaviour
    {
        [Header("桌面")]
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private Image background;
        [SerializeField] private Text heroHealth, heroBlock, sparkText, energyText, encounterText, turnText;
        [SerializeField] private ForgeCardView[] enemies, handCards, rewardCards;
        [SerializeField] private ForgeDeviceView[] devices;
        [SerializeField] private GameObject handPanel, workshopPanel, rewardPanel, resultPanel;
        [SerializeField] private GameObject desktopPanel, menuPanel;
        [SerializeField] private Button menuButton, closeMenuButton;
        [SerializeField] private Text workshopText, rewardText, resultTitle, resultText, pileText;
        [SerializeField] private Button endTurnButton, startBattleButton, returnButton, restartButton,
            saveButton, loadButton, resultRestartButton, resultReturnButton;
        [Header("拆装")]
        [SerializeField] private GameObject assemblyPanel;
        [SerializeField] private Text assemblyTitle, assemblyPreview;
        [SerializeField] private ScrollRect assemblyScroll, tooltipScroll;
        [SerializeField] private RectTransform triggerContent, effectContent, modifierContent;
        [SerializeField] private ForgeCardView optionTemplate;
        [SerializeField] private Button closeAssemblyButton, installButton, clearButton;
        [Header("反馈")]
        [SerializeField] private Text eventText, messageText, tooltipText;
        [SerializeField] private GameObject messagePanel, tooltipPanel;
        private ForgeConfiguration config;
        private ForgeEngine engine;
        private ForgeDefinition definition;
        private ForgeState state;
        private readonly Dictionary<string, ForgeModule> modules = new Dictionary<string, ForgeModule>(StringComparer.Ordinal);
        private readonly Dictionary<string, ForgeCard> cards = new Dictionary<string, ForgeCard>(StringComparer.Ordinal);
        private readonly List<ForgeCardView> options = new List<ForgeCardView>();
        private readonly Queue<string> events = new Queue<string>();
        private int assemblySlot = -1;
        private int SelectedEnemy => state != null ? state.SelectedEnemy : -1;
        private string chosenTrigger = "", chosenEffect = "", chosenModifier = "";
        private bool buttonsBound;
        private float messageTime;
        private GameObject returnFocus;
        private static readonly KeyCode[] CircuitKeys = { KeyCode.Q, KeyCode.W, KeyCode.E };

        public event Action<int, int> CardRequested, CircuitRequested;
        public event Action<int, string, string, string> CircuitConfigured;
        public event Action<int> CircuitCleared, TargetRequested;
        public event Action EndTurnRequested, StartBattleRequested, ReturnRequested, RestartRequested, SaveRequested, LoadRequested;
        public event Action<string> RewardRequested;

        private void Awake() { BindButtons(); ApplySafeArea(); }

        public void Initialize(ForgeEngine engine, ForgeConfiguration configuration)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.engine = engine; definition = engine.Definition;
            modules.Clear(); cards.Clear(); events.Clear(); eventText.text = "";
            foreach (ForgeModule module in definition.Modules) modules.Add(module.Id, module);
            foreach (ForgeCard card in definition.Cards) cards.Add(card.Id, card);
            foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = config.Font;
            background.sprite = config.Background;
            background.color = config.Background != null ? Color.white : new Color(0.035f, 0.10f, 0.14f);
            foreach (ForgeCardView card in enemies) card.Initialize(config);
            foreach (ForgeCardView card in handCards) card.Initialize(config);
            foreach (ForgeCardView card in rewardCards) card.Initialize(config);
            foreach (ForgeDeviceView device in devices) device.Initialize(config);
            if (menuPanel != null) menuPanel.SetActive(false);
            BindButtons(); BuildModulePool(); CloseAssembly();
            ShowMessage(""); ShowTooltip("");
        }

        private void BindButtons()
        {
            if (buttonsBound) return; buttonsBound = true;
            endTurnButton.onClick.AddListener(() => EndTurnRequested?.Invoke());
            startBattleButton.onClick.AddListener(() => StartBattleRequested?.Invoke());
            returnButton.onClick.AddListener(() => ReturnRequested?.Invoke());
            restartButton.onClick.AddListener(() => RestartRequested?.Invoke());
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            loadButton.onClick.AddListener(() => LoadRequested?.Invoke());
            resultRestartButton.onClick.AddListener(() => RestartRequested?.Invoke());
            resultReturnButton.onClick.AddListener(() => ReturnRequested?.Invoke());
            closeAssemblyButton.onClick.AddListener(CloseAssembly);
            installButton.onClick.AddListener(InstallCircuit);
            clearButton.onClick.AddListener(ClearCircuit);
            if (menuButton != null) menuButton.onClick.AddListener(OpenMenu);
            if (closeMenuButton != null) closeMenuButton.onClick.AddListener(CloseMenu);
            foreach (ForgeCardView enemy in enemies)
            { enemy.Clicked += SelectEnemy; enemy.Focused += InspectEnemy; }
            foreach (ForgeCardView hand in handCards)
            { hand.Clicked += PlayCard; hand.Focused += InspectCard; }
            foreach (ForgeCardView reward in rewardCards) reward.Clicked += ChooseReward;
            foreach (ForgeDeviceView device in devices)
            {
                device.Clicked += UseDevice;
                device.Inspected += text => { if (!string.IsNullOrEmpty(text)) ShowTooltip(text); };
                device.PreviewRequested += InspectCircuit;
            }
        }

        public void Render(ForgeState value)
        {
            if (value == null || definition == null) return;
            ForgePhase previous = state != null ? state.Phase : (ForgePhase)(-1);
            state = value;
            ShowTooltip("");
            heroHealth.text = "生命 " + state.PlayerHealth + "/" + definition.MaxHealth;
            heroBlock.text = "护盾 " + state.PlayerBlock;
            sparkText.text = "火花 " + state.Spark + "/" + definition.SparkCap;
            energyText.text = "能量 " + state.Energy + "/" + definition.EnergyPerTurn;
            ForgeEncounter encounter = Encounter(state.EncounterIndex);
            encounterText.text = encounter != null ? (state.EncounterIndex + 1) + " / " + definition.Encounters.Count + "  " + encounter.Name : "试炼结束";
            turnText.text = state.Phase == ForgePhase.Battle ? "第 " + state.Turn + " 回合" : PhaseName(state.Phase);
            pileText.text = "抽牌 " + state.DrawPile.Count + "   弃牌 " + state.DiscardPile.Count;
            bool battle = state.Phase == ForgePhase.Battle;
            handPanel.SetActive(battle); workshopPanel.SetActive(state.Phase == ForgePhase.Workshop);
            rewardPanel.SetActive(state.Phase == ForgePhase.Reward);
            resultPanel.SetActive(state.Phase == ForgePhase.Victory || state.Phase == ForgePhase.Defeat);
            endTurnButton.interactable = battle;
            startBattleButton.interactable = state.Phase == ForgePhase.Workshop;
            workshopText.text = "下一场  " + (encounter != null ? encounter.Name + "\n" + encounter.Description : "") + "\n战后恢复 " + definition.HealAfterBattle + " 生命";
            if (!battle && assemblySlot >= 0 && state.Phase != ForgePhase.Workshop) CloseAssembly();
            RenderEnemies(encounter, battle);
            RenderDevices(); RenderHand(battle); RenderRewards(encounter);
            if (state.Phase == ForgePhase.Victory || state.Phase == ForgePhase.Defeat)
            {
                resultTitle.text = state.Phase == ForgePhase.Victory ? "试炼完成" : "试炼失败";
                resultText.text = state.Phase == ForgePhase.Victory
                    ? "所有试炼已完成\n剩余生命 " + state.PlayerHealth + " / " + definition.MaxHealth
                    : "倒在了" + (encounter != null ? "「" + encounter.Name + "」" : "试炼中") + "\n第 " + state.Turn + " 回合";
            }
            if (assemblySlot >= 0) RefreshAssembly();
            UpdateDesktopVisibility();
            RefreshNavigation(previous != state.Phase);
        }

        private void RenderEnemies(ForgeEncounter encounter, bool battle)
        {
            int count = battle ? state.Enemies.Count : encounter != null ? encounter.Enemies.Count : 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                bool visible = i < count && state.Phase != ForgePhase.Victory && state.Phase != ForgePhase.Defeat;
                enemies[i].gameObject.SetActive(visible); if (!visible) continue;
                ForgeEnemyState enemy = battle ? state.Enemies[i] : null;
                ForgeEnemyTemplate model = battle ? EnemyDefinition(encounter, enemy.DefinitionId) : encounter.Enemies[i];
                if (model == null) continue;
                ForgeIntent intent = model.Intents.Count > 0 ? model.Intents[(enemy != null ? enemy.IntentIndex : 0) % model.Intents.Count] : null;
                int health = enemy != null ? enemy.Health : model.MaxHealth;
                string intentText = battle ? engine.DescribeIntent(i) : intent == null ? "" : intent.Name + (intent.Power > 0 ? " " + intent.Power : "");
                string status = health <= 0 ? "已击破" : "生命 " + health + "/" + model.MaxHealth;
                if (enemy != null)
                {
                    if (enemy.Block > 0) status += "  盾 " + enemy.Block;
                    if (enemy.Burn > 0) status += "\n燃烧 " + enemy.Burn;
                    if (enemy.Frost > 0) status += "  冻结 " + enemy.Frost;
                }
                enemies[i].Bind(i, model.Id, model.Name, intentText,
                    battle && i == SelectedEnemy && health > 0 ? "目标" : "", status,
                    config.ArtFor(model.SpriteIndex), battle && health > 0, battle && SelectedEnemy == i && health > 0);
            }
        }

        private void RenderDevices()
        {
            for (int i = 0; i < devices.Length; i++)
            {
                ForgeCircuit circuit = i < state.Circuits.Count ? state.Circuits[i] : null;
                ForgeModule trigger = Module(circuit != null ? circuit.TriggerId : "");
                ForgeModule effect = Module(circuit != null ? circuit.EffectId : "");
                ForgeModule modifier = Module(circuit != null ? circuit.ModifierId : "");
                bool fitted = trigger != null && effect != null;
                int uses = i < state.CircuitActivations.Count ? state.CircuitActivations[i] : 0;
                int sparkCost = fitted ? CircuitSparkCost(effect, modifier) : 0;
                bool workshop = state.Phase == ForgePhase.Workshop;
                bool manual = fitted && trigger.Trigger == ForgeTrigger.Manual;
                bool canUse = workshop || (state.Phase == ForgePhase.Battle && manual && state.Energy > 0 && state.Spark >= sparkCost && uses < definition.MaxCircuitActivations);
                string action = workshop ? "拆装" : !fitted ? "空槽" : !manual ? "自动 · " + sparkCost + " 火花" : uses >= definition.MaxCircuitActivations ? "次数已满" : state.Energy <= 0 ? "能量不足" : state.Spark < sparkCost ? "火花不足" : "启动 · 1能量 " + sparkCost + "火花";
                string details = fitted ? engine.DescribeCircuit(i) + "\n" + engine.DescribeModule(trigger.Id) + " " + engine.DescribeModule(effect.Id) + (modifier != null ? " " + engine.DescribeModule(modifier.Id) : "") : "选择触发与效果，装进这个槽位。";
                devices[i].Bind(i, "装置 " + (i + 1), trigger != null ? trigger.Name : "未装触发",
                    effect != null ? effect.Name + " · " + effect.Value : "未装效果", modifier != null ? ModifierCaption(modifier) : "无改装",
                    workshop ? "每回合最多 " + definition.MaxCircuitActivations + " 次" : "本回合 " + uses + "/" + definition.MaxCircuitActivations,
                    action, effect != null ? config.ArtFor(effect.SpriteIndex) : null, canUse, details);
            }
        }

        private void RenderHand(bool battle)
        {
            for (int i = 0; i < handCards.Length; i++)
            {
                bool visible = battle && i < state.Hand.Count;
                handCards[i].gameObject.SetActive(visible); if (!visible) continue;
                if (!cards.TryGetValue(state.Hand[i], out ForgeCard card)) continue;
                bool energy = state.Energy >= card.Cost, spark = card.Kind == ForgeCardKind.Discharge ? state.Spark > card.SparkCost : state.Spark >= card.SparkCost;
                string price = card.Cost + " 能量" + (card.Kind == ForgeCardKind.Discharge ? " · " + (card.SparkCost > 0 ? card.SparkCost + "+余量" : "全部") + "火花" : card.SparkCost > 0 ? " · " + card.SparkCost + " 火花" : "");
                handCards[i].Bind(i, card.Id, card.Name, CardFaceRule(engine.DescribeCard(card.Id)), price,
                    !energy ? "能量不足" : !spark ? "火花不足" : "", config.ArtFor(card.SpriteIndex), energy && spark);
            }
        }

        private void RenderRewards(ForgeEncounter encounter)
        {
            if (state.Phase != ForgePhase.Reward) return;
            ForgeEncounter next = Encounter(state.EncounterIndex + 1);
            rewardText.text = "选择一个零件" + (next != null ? "   ·   下一场：" + next.Name + "\n" + next.Description : "");
            List<string> ids = encounter != null ? encounter.RewardModuleIds : new List<string>();
            int cursor = 0;
            foreach (string id in ids)
            {
                ForgeModule module = Module(id);
                if (module == null || state.UnlockedModuleIds.Contains(id)) continue;
                if (cursor >= rewardCards.Length) break;
                rewardCards[cursor].gameObject.SetActive(true);
                rewardCards[cursor].Bind(cursor, module.Id, module.Name, FirstSentence(engine.DescribeModule(module.Id)), KindName(module.Kind), "获得后可随时拆装", config.ArtFor(module.SpriteIndex), true);
                cursor++;
            }
            for (; cursor < rewardCards.Length; cursor++) rewardCards[cursor].gameObject.SetActive(false);
        }

        public void Present(ForgeResult result)
        {
            if (result == null) return;
            if (!result.Success) { ShowMessage(result.Error, true); return; }
            if (messageTime < 0) ShowMessage("");
            int[] damageTotals = new int[enemies.Length];
            bool[] wasHit = new bool[enemies.Length];
            foreach (ForgeEvent item in result.Events)
            {
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    string line = ShortEvent(item).Replace("\r", "").Replace("\n", " ");
                    events.Enqueue(line.Length > 27 ? line.Substring(0, 26) + "…" : line);
                    while (events.Count > 7) events.Dequeue();
                }
                if (item.Kind == "CircuitActivated" && item.CircuitIndex >= 0 && item.CircuitIndex < devices.Length) devices[item.CircuitIndex].Present();
                if (item.EnemyIndex >= 0 && item.EnemyIndex < enemies.Length)
                {
                    if (item.Kind == "EnemyHit" || item.Kind == "Burn")
                    { damageTotals[item.EnemyIndex] += item.Value; wasHit[item.EnemyIndex] = true; }
                }
            }
            // 一条命令内的快速连击合并成真实生命伤害总数，逐段数值仍在短事件链中保留。
            for (int i = 0; i < enemies.Length; i++) if (wasHit[i]) enemies[i].Present(damageTotals[i], true);
            StringBuilder log = new StringBuilder();
            foreach (string line in events) { if (log.Length > 0) log.Append("\n"); log.Append(line); }
            eventText.text = log.ToString();
        }

        public void ShowMessage(string text, bool error = false)
        {
            messageText.text = text ?? ""; messagePanel.SetActive(!string.IsNullOrEmpty(text));
            messageText.color = error ? new Color(1, 0.65f, 0.53f) : new Color(1, 0.91f, 0.64f);
            messageTime = error ? -1 : 3.5f;
            PlaceMessage();
        }

        private void SelectEnemy(ForgeCardView card)
        {
            if (state == null || state.Phase != ForgePhase.Battle || card.Index >= state.Enemies.Count || state.Enemies[card.Index].Health <= 0) return;
            if (card.Index != SelectedEnemy) TargetRequested?.Invoke(card.Index);
        }
        private void PlayCard(ForgeCardView card)
        {
            if (state != null && state.Phase == ForgePhase.Battle && assemblySlot < 0) CardRequested?.Invoke(card.Index, SelectedEnemy);
        }
        private void ChooseReward(ForgeCardView card) => RewardRequested?.Invoke(card.Id);
        private void UseDevice(int index)
        {
            if (state == null) return;
            if (state.Phase == ForgePhase.Workshop) OpenAssembly(index);
            else if (state.Phase == ForgePhase.Battle && assemblySlot < 0) CircuitRequested?.Invoke(index, SelectedEnemy);
        }

        private void BuildModulePool()
        {
            options.Clear(); optionTemplate.gameObject.SetActive(false);
            Dictionary<ForgeModuleKind, Queue<ForgeCardView>> pools = new Dictionary<ForgeModuleKind, Queue<ForgeCardView>>();
            RectTransform[] contents = { triggerContent, effectContent, modifierContent };
            for (int i = 0; i < contents.Length; i++)
            {
                Queue<ForgeCardView> pool = new Queue<ForgeCardView>();
                foreach (ForgeCardView item in contents[i].GetComponentsInChildren<ForgeCardView>(true))
                { item.Clicked -= SelectModule; item.gameObject.SetActive(false); pool.Enqueue(item); }
                pools.Add((ForgeModuleKind)i, pool);
            }
            // 选择项来自一个已序列化模板；整张桌面、敌牌、手牌、装置和所有面板都预置在Prefab内。
            foreach (ForgeModule module in definition.Modules)
            {
                RectTransform parent = module.Kind == ForgeModuleKind.Trigger ? triggerContent : module.Kind == ForgeModuleKind.Effect ? effectContent : modifierContent;
                ForgeCardView option = pools[module.Kind].Count > 0 ? pools[module.Kind].Dequeue() : Instantiate(optionTemplate, parent);
                option.name = module.Name; option.Initialize(config);
                option.Clicked += SelectModule;
                option.Focused -= RevealOption;
                option.Focused += RevealOption;
                option.gameObject.SetActive(true);
                options.Add(option);
                option.Bind(0, module.Id, module.Name, FirstSentence(engine.DescribeModule(module.Id)), "", "", config.ArtFor(module.SpriteIndex), false);
            }
        }
        private void OpenAssembly(int index)
        {
            assemblySlot = index;
            ForgeCircuit circuit = index < state.Circuits.Count ? state.Circuits[index] : null;
            chosenTrigger = circuit != null ? circuit.TriggerId : FirstUnlocked(ForgeModuleKind.Trigger);
            chosenEffect = circuit != null ? circuit.EffectId : FirstUnlocked(ForgeModuleKind.Effect);
            chosenModifier = circuit != null ? circuit.ModifierId : FirstUnlocked(ForgeModuleKind.Modifier);
            returnFocus = devices[index].Button.gameObject;
            if (menuPanel != null) menuPanel.SetActive(false);
            assemblyPanel.SetActive(true); UpdateDesktopVisibility(); ShowTooltip(""); RefreshAssembly(); SetNavigationScope(assemblyPanel.transform); Focus(closeAssemblyButton);
        }
        private void SelectModule(ForgeCardView card)
        {
            ForgeModule module = Module(card.Id); if (module == null) return;
            if (module.Kind == ForgeModuleKind.Trigger) chosenTrigger = module.Id;
            else if (module.Kind == ForgeModuleKind.Effect) chosenEffect = module.Id;
            else chosenModifier = module.Id;
            RefreshAssembly();
        }
        private void RefreshAssembly()
        {
            assemblyTitle.text = "编辑装置 " + (assemblySlot + 1);
            foreach (ForgeCardView option in options)
            {
                ForgeModule module = Module(option.Id);
                bool unlocked = state.UnlockedModuleIds.Contains(module.Id);
                bool selected = module.Id == chosenTrigger || module.Id == chosenEffect || module.Id == chosenModifier;
                option.Bind(option.Index, module.Id, module.Name, unlocked ? FirstSentence(engine.DescribeModule(module.Id)) : UnlockAt(module.Id),
                    unlocked ? (selected ? "已选" : "") : "锁定", "", config.ArtFor(module.SpriteIndex), unlocked, selected);
            }
            ForgeModule trigger = Module(chosenTrigger), effect = Module(chosenEffect), modifier = Module(chosenModifier);
            assemblyPreview.text = (trigger != null ? trigger.Name : "选择触发") + "  →  " + (effect != null ? effect.Name : "选择效果") + "  →  " + (modifier != null ? modifier.Name : "无改装")
                + (effect != null ? "\n每次消耗 " + (trigger != null && trigger.Trigger == ForgeTrigger.Manual ? "1 能量、" : "") + CircuitSparkCost(effect, modifier) + " 火花 · 每回合最多 " + definition.MaxCircuitActivations + " 次" : "")
                + (trigger != null ? "\n触发：" + engine.DescribeModule(trigger.Id) : "")
                + (effect != null ? "\n效果：" + engine.DescribeModule(effect.Id) : "")
                + (modifier != null && modifier.Modifier != ForgeModifier.None ? "\n改装：" + engine.DescribeModule(modifier.Id) : "");
            ResetScroll(assemblyScroll);
            installButton.interactable = trigger != null && effect != null && state.UnlockedModuleIds.Contains(chosenTrigger) && state.UnlockedModuleIds.Contains(chosenEffect)
                && (string.IsNullOrEmpty(chosenModifier) || state.UnlockedModuleIds.Contains(chosenModifier));
            clearButton.interactable = assemblySlot >= 0 && assemblySlot < state.Circuits.Count && Module(state.Circuits[assemblySlot].TriggerId) != null;
        }
        private void InstallCircuit()
        {
            if (assemblySlot < 0 || !installButton.interactable) return;
            CircuitConfigured?.Invoke(assemblySlot, chosenTrigger, chosenEffect, chosenModifier);
            CloseAssembly();
        }
        private void ClearCircuit()
        {
            if (assemblySlot < 0) return;
            CircuitCleared?.Invoke(assemblySlot); CloseAssembly();
        }
        private void CloseAssembly()
        {
            assemblySlot = -1; assemblyPanel.SetActive(false);
            UpdateDesktopVisibility();
            if (state != null) SetNavigationScope(resultPanel.activeSelf ? resultPanel.transform : rewardPanel.activeSelf ? rewardPanel.transform : null);
            if (returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
            returnFocus = null;
        }
        private void ShowTooltip(string text)
        {
            tooltipText.text = text ?? "";
            tooltipPanel.SetActive(!string.IsNullOrEmpty(text) && assemblySlot < 0);
            if (tooltipPanel.activeInHierarchy) ResetScroll(tooltipScroll);
        }

        private void OpenMenu()
        {
            if (menuPanel == null || assemblySlot >= 0) return;
            ShowTooltip(""); menuPanel.SetActive(true); UpdateDesktopVisibility();
            SetNavigationScope(menuPanel.transform); Focus(closeMenuButton);
        }
        private void CloseMenu()
        {
            if (menuPanel == null) return;
            menuPanel.SetActive(false); UpdateDesktopVisibility();
            SetNavigationScope(resultPanel.activeSelf ? resultPanel.transform : rewardPanel.activeSelf ? rewardPanel.transform : null);
            if (state != null && (state.Phase == ForgePhase.Reward || state.Phase == ForgePhase.Victory || state.Phase == ForgePhase.Defeat)) FocusDefault();
            else Focus(menuButton);
        }
        private void UpdateDesktopVisibility()
        {
            if (desktopPanel != null) desktopPanel.SetActive(assemblySlot < 0 && (menuPanel == null || !menuPanel.activeSelf) &&
                (state == null || state.Phase == ForgePhase.Workshop || state.Phase == ForgePhase.Battle));
            PlaceMessage();
        }
        private void PlaceMessage()
        {
            if (messagePanel == null || messageText == null) return;
            bool compact = assemblySlot >= 0 || ((menuPanel == null || !menuPanel.activeSelf) &&
                (state == null || state.Phase == ForgePhase.Workshop || state.Phase == ForgePhase.Battle));
            RectTransform rect = (RectTransform)messagePanel.transform;
            rect.anchoredPosition = compact ? new Vector2(32, assemblySlot >= 0 ? -649 : -601) : new Vector2(300, -804);
            rect.sizeDelta = compact ? new Vector2(1536, 26) : new Vector2(1000, 64);
            messageText.fontSize = compact ? 15 : 18;
        }
        private static void ResetScroll(ScrollRect scroll)
        {
            if (scroll == null || scroll.content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
        }

        private void InspectCard(ForgeCardView card)
        {
            if (engine == null || state == null || state.Phase != ForgePhase.Battle || !cards.TryGetValue(card.Id, out ForgeCard model)) return;
            ShowPreview(engine.PreviewCard(card.Index, SelectedEnemy), model.Name + "：" + engine.DescribeCard(model.Id));
        }
        private void InspectEnemy(ForgeCardView card)
        {
            if (engine == null || state == null) return;
            ForgeEncounter encounter = Encounter(state.EncounterIndex);
            ForgeEnemyTemplate model = state.Phase == ForgePhase.Battle && card.Index < state.Enemies.Count
                ? EnemyDefinition(encounter, state.Enemies[card.Index].DefinitionId)
                : encounter != null && card.Index < encounter.Enemies.Count ? encounter.Enemies[card.Index] : null;
            if (model != null) ShowTooltip(model.Name + "：" + model.Description
                + (state.Phase == ForgePhase.Battle ? "\n下一动作：" + engine.DescribeIntent(card.Index) : ""));
        }
        private void RevealOption(ForgeCardView option)
        {
            ScrollRect scroll = option.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, option.transform);
            float offset = bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y
                : bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y : 0;
            scroll.content.anchoredPosition += Vector2.up * offset;
            scroll.StopMovement();
        }
        private void InspectCircuit(int index)
        {
            if (engine == null || state == null || state.Phase != ForgePhase.Battle || index >= state.Circuits.Count) return;
            ForgeModule trigger = Module(state.Circuits[index].TriggerId);
            if (trigger != null && trigger.Trigger == ForgeTrigger.Manual)
                ShowPreview(engine.PreviewCircuit(index, SelectedEnemy), engine.DescribeCircuit(index));
        }
        private void ShowPreview(ForgePreview preview, string rule)
        {
            if (!preview.Success) { ShowTooltip(rule + "\n" + preview.Error); return; }
            ForgeState next = preview.State;
            StringBuilder text = new StringBuilder(rule);
            text.Append("\n预计  生命 ").Append(state.PlayerHealth).Append("→").Append(next.PlayerHealth)
                .Append("   盾 ").Append(state.PlayerBlock).Append("→").Append(next.PlayerBlock)
                .Append("   火花 ").Append(state.Spark).Append("→").Append(next.Spark);
            ForgeEncounter encounter = Encounter(state.EncounterIndex);
            for (int i = 0; i < state.Enemies.Count && i < next.Enemies.Count; i++)
            {
                ForgeEnemyState before = state.Enemies[i], after = next.Enemies[i];
                if (before.Health == after.Health && before.Block == after.Block && before.Burn == after.Burn && before.Frost == after.Frost) continue;
                ForgeEnemyTemplate model = EnemyDefinition(encounter, before.DefinitionId);
                text.Append("\n").Append(model != null ? model.Name : "目标").Append("  生命 ").Append(before.Health).Append("→").Append(after.Health);
                if (before.Block != after.Block) text.Append("  盾 ").Append(before.Block).Append("→").Append(after.Block);
                if (before.Burn != after.Burn) text.Append("  燃烧 ").Append(before.Burn).Append("→").Append(after.Burn);
                if (before.Frost != after.Frost) text.Append("  冻结 ").Append(before.Frost).Append("→").Append(after.Frost);
            }
            int steps = 0;
            foreach (ForgeEvent item in preview.Events)
            {
                if (item.Kind != "CircuitActivated") continue;
                text.Append(steps++ == 0 ? "\n连锁  " : " → ").Append("装置").Append(item.CircuitIndex + 1);
            }
            ShowTooltip(text.ToString());
        }
        private string ShortEvent(ForgeEvent item)
        {
            string source = item.CircuitIndex >= 0 ? "装置" + (item.CircuitIndex + 1) + " → " : "";
            string enemy = "目标";
            if (state != null && item.EnemyIndex >= 0 && item.EnemyIndex < state.Enemies.Count)
            {
                ForgeEnemyTemplate model = EnemyDefinition(Encounter(state.EncounterIndex), state.Enemies[item.EnemyIndex].DefinitionId);
                if (model != null) enemy = model.Name;
            }
            switch (item.Kind)
            {
                case "CircuitActivated": return source + "启动 · " + item.Value + "火花";
                case "EnemyHit": return source + enemy + " −" + item.Value + "生命";
                case "Burn": return enemy + " 燃烧 −" + item.Value;
                case "Ignited": return source + enemy + " 燃烧 +" + item.Value;
                case "Frosted": return source + enemy + " 冻结 +" + item.Value;
                case "Frozen": return enemy + " 冻结，跳过行动";
                case "EnemyGuarded": return enemy + " 护盾 +" + item.Value;
                case "EnemyCharged": return enemy + " 蓄力 +" + item.Value;
                case "SparkDrained": return "火花 −" + item.Value;
                case "SparkGained": return source + "火花 +" + item.Value;
                case "BlockGained": return source + "护盾 +" + item.Value;
                case "Healed": return "生命 +" + item.Value;
                case "PlayerHit": return item.Value > 0 ? "生命 −" + item.Value : "护盾挡住攻击";
                case "Configured": return "装置" + (item.CircuitIndex + 1) + " 已装上";
                case "Suppressed": return "装置" + (item.CircuitIndex + 1) + (item.Text.Contains("上限") ? " 次数已满" : " 火花不足");
                case "CardPlayed":
                    int separator = item.Text.IndexOf("消耗", StringComparison.Ordinal);
                    return separator > 0 ? "打出 " + item.Text.Substring(0, separator) : item.Text;
                default: return item.Text;
            }
        }

        public void Unbind()
        {
            state = null; definition = null; engine = null; assemblySlot = -1;
            assemblyPanel.SetActive(false); tooltipPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
            UpdateDesktopVisibility();
        }
        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            foreach (ForgeCardView card in enemies) if (card.gameObject.activeInHierarchy) card.Animate(delta);
            foreach (ForgeCardView card in handCards) if (card.gameObject.activeInHierarchy) card.Animate(delta);
            foreach (ForgeDeviceView device in devices) device.Animate(delta);
            if (messageTime > 0) { messageTime -= delta; if (messageTime <= 0) messagePanel.SetActive(false); }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (assemblySlot >= 0) CloseAssembly();
                else if (menuPanel != null && menuPanel.activeSelf) CloseMenu();
                else OpenMenu();
            }
            if (state == null || assemblySlot >= 0 || (menuPanel != null && menuPanel.activeSelf) || state.Phase != ForgePhase.Battle) return;
            if (Input.GetKeyDown(KeyCode.Space) && endTurnButton.interactable) EndTurnRequested?.Invoke();
            for (int i = 0; i < Mathf.Min(9, state.Hand.Count); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) && i < handCards.Length && handCards[i].Button.interactable) PlayCard(handCards[i]);
            for (int i = 0; i < devices.Length && i < CircuitKeys.Length; i++)
                if (Input.GetKeyDown(CircuitKeys[i]) && devices[i].Button.interactable) UseDevice(i);
        }
        private void OnRectTransformDimensionsChange() { if (safeArea != null) ApplySafeArea(); }
        private void ApplySafeArea()
        {
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            Vector2 min = safe.position, max = safe.position + safe.size;
            safeArea.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            safeArea.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
        }
        private void FocusDefault()
        {
            if (state.Phase == ForgePhase.Workshop) Focus(startBattleButton);
            else if (state.Phase == ForgePhase.Battle) Focus(endTurnButton);
            else if (state.Phase == ForgePhase.Reward)
            { foreach (ForgeCardView card in rewardCards) if (card.gameObject.activeSelf) { Focus(card.Button); break; } }
            else Focus(resultRestartButton);
        }
        private static void Focus(Button button) { if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject); }
        private void RefreshNavigation(bool resetFocus)
        {
            Transform scope = menuPanel != null && menuPanel.activeSelf ? menuPanel.transform
                : assemblySlot >= 0 ? assemblyPanel.transform
                : resultPanel.activeSelf ? resultPanel.transform
                : rewardPanel.activeSelf ? rewardPanel.transform : null;
            SetNavigationScope(scope);
            GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Button focused = current != null ? current.GetComponent<Button>() : null;
            bool valid = focused != null && focused.isActiveAndEnabled && focused.interactable &&
                (scope == null || focused.transform.IsChildOf(scope));
            if (!resetFocus && valid) return;
            if (menuPanel != null && menuPanel.activeSelf) Focus(closeMenuButton);
            else if (assemblySlot >= 0) Focus(closeAssemblyButton);
            else FocusDefault();
        }
        private void SetNavigationScope(Transform scope)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.navigation = new Navigation { mode = button.gameObject.activeInHierarchy && (scope == null || button.transform.IsChildOf(scope)) ? Navigation.Mode.Automatic : Navigation.Mode.None };
        }
        private ForgeModule Module(string id) => !string.IsNullOrEmpty(id) && modules.TryGetValue(id, out ForgeModule module) ? module : null;
        private ForgeEncounter Encounter(int index) => index >= 0 && index < definition.Encounters.Count ? definition.Encounters[index] : null;
        private static ForgeEnemyTemplate EnemyDefinition(ForgeEncounter encounter, string id)
        { if (encounter != null) foreach (ForgeEnemyTemplate enemy in encounter.Enemies) if (enemy.Id == id) return enemy; return null; }
        private string FirstUnlocked(ForgeModuleKind kind)
        { foreach (ForgeModule module in definition.Modules) if (module.Kind == kind && state.UnlockedModuleIds.Contains(module.Id)) return module.Id; return ""; }
        private string UnlockAt(string id)
        { for (int i = 0; i < definition.Encounters.Count; i++) if (definition.Encounters[i].RewardModuleIds.Contains(id)) return "第 " + (i + 1) + " 场胜利后可选"; return "尚未获得"; }
        private static int CircuitSparkCost(ForgeModule effect, ForgeModule modifier)
        { return Mathf.Max(0, effect.SparkCost + (modifier != null && modifier.Modifier == ForgeModifier.Overload ? 1 : 0)); }
        private static string FirstSentence(string rule)
        { int end = rule.IndexOf('。'); return end >= 0 ? rule.Substring(0, end + 1) : rule; }
        private static string CardFaceRule(string rule)
        {
            // 费用另有固定标签；牌面保留核心生成的首句规则，补充规则放在悬停预览。
            if (rule.StartsWith("消耗", StringComparison.Ordinal))
            { int comma = rule.IndexOf('，'); if (comma >= 0) rule = rule.Substring(comma + 1); }
            return FirstSentence(rule);
        }
        private static string ModifierCaption(ForgeModule module)
        {
            switch (module.Modifier)
            {
                case ForgeModifier.Fork: return module.Name + " · −1 全体";
                case ForgeModifier.Echo: return module.Name + " · −1 两次";
                case ForgeModifier.Overload: return module.Name + " · +2";
                default: return module.Name;
            }
        }
        private static string KindName(ForgeModuleKind kind) => kind == ForgeModuleKind.Trigger ? "触发" : kind == ForgeModuleKind.Effect ? "效果" : "改装";
        private static string PhaseName(ForgePhase phase) => phase == ForgePhase.Workshop ? "拆装" : phase == ForgePhase.Reward ? "获得零件" : phase == ForgePhase.Victory ? "试炼完成" : "试炼结束";
    }
}
