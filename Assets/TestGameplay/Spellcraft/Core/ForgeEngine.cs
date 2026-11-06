using System;
using System.Collections.Generic;

namespace CardKKK.Spellcraft.Core
{
    /// <summary>所有命令在独立候选快照上结算；只有完整连锁与语义校验成功后才提交。</summary>
    public sealed partial class ForgeEngine
    {
        public const int CircuitCount = 3;
        private const int NumericLimit = 100000000;
        private readonly ForgeDefinition _definition;
        private readonly Dictionary<string, ForgeModule> _modules;
        private readonly Dictionary<string, ForgeCard> _cards;
        private ForgeState _state;
        private bool _executing;

        public ForgeDefinition Definition { get { return ForgeCopies.Definition(_definition); } }
        public ForgeState State { get { return ForgeCopies.State(_state); } }
        public event Action<ForgeState> Changed;

        private enum PendingKind { Trigger, Circuit, Card }

        private sealed class Pending
        {
            internal PendingKind Kind;
            internal ForgeTrigger Trigger;
            internal int CircuitIndex = -1;
            internal int EnemyIndex = -1;
            internal int Power;
            internal ForgeCardKind CardKind;
            internal string Source;
        }

        private sealed class Command
        {
            internal ForgeState State;
            internal readonly List<ForgeEvent> Events = new List<ForgeEvent>();
            internal readonly Queue<Pending> Queue = new Queue<Pending>();
            internal int Steps;
        }

        public ForgeEngine(ForgeDefinition definition, int seed)
        {
            try { ValidateDefinition(definition); }
            catch (RuleFailure exception) { throw new ArgumentException(exception.Message); }
            _definition = ForgeCopies.Definition(definition);
            _modules = new Dictionary<string, ForgeModule>(StringComparer.Ordinal);
            _cards = new Dictionary<string, ForgeCard>(StringComparer.Ordinal);
            foreach (ForgeModule item in _definition.Modules) _modules.Add(item.Id, item);
            foreach (ForgeCard item in _definition.Cards) _cards.Add(item.Id, item);
            _state = new ForgeState
            {
                Phase = ForgePhase.Workshop, PlayerHealth = _definition.MaxHealth,
                Energy = _definition.EnergyPerTurn, Spark = _definition.StartingSpark,
                Seed = seed, RandomState = seed == 0 ? unchecked((int)0xA341316Cu) : seed,
                UnlockedModuleIds = new List<string>(_definition.StartingUnlocked),
                DrawPile = new List<string>(_definition.StartingDeck)
            };
            foreach (ForgeCircuit circuit in _definition.StartingCircuits) _state.Circuits.Add(ForgeCopies.Circuit(circuit));
            for (int i = 0; i < CircuitCount; i++) _state.CircuitActivations.Add(0);
            ValidateState(_state);
        }

        public ForgeResult ConfigureCircuit(int slot, string triggerId, string effectId, string modifierId)
        {
            return Execute(command =>
            {
                RequirePhase(command.State, ForgePhase.Workshop, "请在工坊拆装法术。");
                RequireSlot(slot);
                var circuit = new ForgeCircuit { TriggerId = triggerId ?? "", EffectId = effectId ?? "", ModifierId = modifierId ?? "" };
                ValidateCircuit(circuit, _modules, new HashSet<string>(command.State.UnlockedModuleIds, StringComparer.Ordinal));
                command.State.Circuits[slot] = circuit;
                Log(command, "Configured", "装置" + (slot + 1) + "：" + DescribeCircuit(circuit), -1, slot);
            });
        }

        public ForgeResult ClearCircuit(int slot)
        {
            return ConfigureCircuit(slot, "", "", "");
        }

        public ForgeResult StartBattle()
        {
            return Execute(command =>
            {
                ForgeState state = command.State;
                RequirePhase(state, ForgePhase.Workshop, "当前不能开始战斗。");
                state.Phase = ForgePhase.Battle;
                state.Turn = 1;
                state.PlayerBlock = 0;
                state.Energy = _definition.EnergyPerTurn;
                state.Spark = _definition.StartingSpark;
                state.Enemies.Clear();
                foreach (ForgeEnemyTemplate enemy in Encounter(state).Enemies)
                    state.Enemies.Add(new ForgeEnemyState { DefinitionId = enemy.Id, Health = enemy.MaxHealth });
                state.SelectedEnemy = 0;
                state.Hand.Clear();
                state.DrawPile = new List<string>(_definition.StartingDeck);
                state.DiscardPile.Clear();
                ResetActivations(state);
                Shuffle(state.DrawPile, state);
                DrawHand(state);
                Log(command, "BattleStarted", Encounter(state).Name + "，第1回合。");
                Trigger(command, ForgeTrigger.TurnStart, -1, "第1回合开始");
            });
        }

        public ForgeResult SelectEnemy(int enemyIndex)
        {
            return Execute(command =>
            {
                RequirePhase(command.State, ForgePhase.Battle, "现在不能选择战斗目标。");
                RequireTarget(command.State, enemyIndex);
                command.State.SelectedEnemy = enemyIndex;
            });
        }

        public ForgeResult PlayCard(int handIndex, int targetEnemy)
        {
            return Execute(CardCommand(handIndex, targetEnemy));
        }

        private Action<Command> CardCommand(int handIndex, int targetEnemy)
        {
            return command =>
            {
                ForgeState state = command.State;
                RequirePhase(state, ForgePhase.Battle, "现在不能出牌。");
                Require(handIndex >= 0 && handIndex < state.Hand.Count, "这张手牌已经不在手中。");
                ForgeCard card = _cards[state.Hand[handIndex]];
                bool targeted = card.Kind == ForgeCardKind.Strike || card.Kind == ForgeCardKind.Ignite || card.Kind == ForgeCardKind.Discharge;
                if (targeted) RequireTarget(state, targetEnemy);
                int sparkCost = card.SparkCost;
                int power = card.Power;
                Require(state.Energy >= card.Cost, "能量不足，需要" + card.Cost + "点。");
                Require(state.Spark >= sparkCost, "火花不足，需要" + sparkCost + "点。");
                if (card.Kind == ForgeCardKind.Discharge)
                {
                    int discharge = state.Spark - sparkCost;
                    Require(discharge > 0, "至少留有1点火花才能放电。");
                    power = checked(card.Power * discharge);
                    sparkCost = state.Spark;
                }
                state.Energy -= card.Cost;
                state.Spark -= sparkCost;
                state.Hand.RemoveAt(handIndex);
                state.DiscardPile.Add(card.Id);
                if (targeted) state.SelectedEnemy = targetEnemy;
                string source = "卡牌「" + card.Name + "」";
                Log(command, "CardPlayed", source + "消耗" + card.Cost + "能量、" + sparkCost + "火花" + (card.Kind == ForgeCardKind.Discharge ? "，放电威力" + power + "。" : "。"), targeted ? targetEnemy : -1, -1, sparkCost);
                if (sparkCost > 0) Trigger(command, ForgeTrigger.SparkSpent, targeted ? targetEnemy : -1, source + "消耗" + sparkCost + "火花");
                command.Queue.Enqueue(new Pending { Kind = PendingKind.Card, CardKind = card.Kind, Power = power, EnemyIndex = targeted ? targetEnemy : -1, Source = source });
            };
        }

        public ForgeResult ActivateCircuit(int slot, int targetEnemy)
        {
            return Execute(CircuitCommand(slot, targetEnemy));
        }

        private Action<Command> CircuitCommand(int slot, int targetEnemy)
        {
            return command =>
            {
                RequirePhase(command.State, ForgePhase.Battle, "现在不能启动装置。");
                RequireSlot(slot);
                ForgeCircuit circuit = command.State.Circuits[slot];
                Require(circuit.TriggerId.Length > 0, "这个装置槽还没有装好。");
                Require(_modules[circuit.TriggerId].Trigger == ForgeTrigger.Manual, "自动装置会在条件发生时触发。");
                Activate(command, slot, targetEnemy, "手动启动", true);
            };
        }

        public ForgePreview PreviewCard(int handIndex, int targetEnemy)
        {
            return Preview(CardCommand(handIndex, targetEnemy));
        }

        public ForgePreview PreviewCircuit(int slot, int targetEnemy)
        {
            return Preview(CircuitCommand(slot, targetEnemy));
        }

        private ForgePreview Preview(Action<Command> operation)
        {
            ForgeState state;
            ForgeResult result = Execute(operation, false, out state);
            return new ForgePreview { Success = result.Success, Error = result.Error, State = state, Events = result.Events };
        }

        public ForgeResult EndTurn()
        {
            return Execute(command =>
            {
                ForgeState state = command.State;
                RequirePhase(state, ForgePhase.Battle, "现在不能结束回合。");
                Log(command, "TurnEnded", "结束第" + state.Turn + "回合。");
                for (int i = 0; i < state.Enemies.Count && state.Phase == ForgePhase.Battle; i++)
                {
                    ForgeEnemyState enemy = state.Enemies[i];
                    if (enemy.Health <= 0 || enemy.Burn <= 0) continue;
                    int burn = enemy.Burn;
                    enemy.Burn--;
                    int damage = Math.Min(enemy.Health, burn);
                    enemy.Health -= damage;
                    Log(command, "Burn", EnemyName(state, i) + "燃烧，损失" + damage + "生命，燃烧剩" + enemy.Burn + "层。", i, -1, damage);
                    UpdateSelectedEnemy(state);
                    CheckOutcome(command);
                    if (state.Phase != ForgePhase.Battle) break;
                    if (damage > 0) Trigger(command, ForgeTrigger.BurnTick, i, EnemyName(state, i) + "燃烧造成" + damage + "伤害");
                    ResolveQueue(command);
                }
                for (int i = 0; i < state.Enemies.Count && state.Phase == ForgePhase.Battle; i++)
                {
                    ForgeEnemyState enemy = state.Enemies[i];
                    if (enemy.Health <= 0) continue;
                    enemy.Block = 0;
                    if (enemy.Frost > 0)
                    {
                        enemy.Frost--;
                        Log(command, "Frozen", EnemyName(state, i) + "被冻结，跳过行动，冻结剩" + enemy.Frost + "层。", i);
                        continue;
                    }
                    ExecuteIntent(command, i);
                    ResolveQueue(command);
                }
                if (state.Phase != ForgePhase.Battle) return;
                state.Turn = checked(state.Turn + 1);
                state.PlayerBlock = 0;
                state.Energy = _definition.EnergyPerTurn;
                state.DiscardPile.AddRange(state.Hand);
                state.Hand.Clear();
                ResetActivations(state);
                DrawHand(state);
                Log(command, "TurnStarted", "第" + state.Turn + "回合，能量补满，抽到" + state.Hand.Count + "张牌。");
                Trigger(command, ForgeTrigger.TurnStart, -1, "第" + state.Turn + "回合开始");
            });
        }

        public ForgeResult ChooseReward(string moduleId)
        {
            return Execute(command =>
            {
                ForgeState state = command.State;
                RequirePhase(state, ForgePhase.Reward, "现在没有待选奖励。");
                Require(moduleId != null && Encounter(state).RewardModuleIds.Contains(moduleId), "这个模块不在本次奖励中。");
                Require(!state.UnlockedModuleIds.Contains(moduleId), "这个模块已经解锁，请选另一个。");
                state.UnlockedModuleIds.Add(moduleId);
                state.EncounterIndex++;
                state.Phase = ForgePhase.Workshop;
                state.Turn = 0;
                state.Enemies.Clear();
                state.SelectedEnemy = -1;
                state.PlayerBlock = 0;
                state.Energy = _definition.EnergyPerTurn;
                state.Spark = _definition.StartingSpark;
                state.Hand.Clear();
                state.DiscardPile.Clear();
                state.DrawPile = new List<string>(_definition.StartingDeck);
                ResetActivations(state);
                Log(command, "RewardChosen", "已解锁「" + _modules[moduleId].Name + "」，回到工坊。");
            });
        }

        public ForgeResult Restore(ForgeState state)
        {
            return Execute(command =>
            {
                ValidateState(state);
                command.State = ForgeCopies.State(state);
                Log(command, "Restored", "进度已恢复。");
            });
        }

        public string DescribeCircuit(int slot)
        {
            if (slot < 0 || slot >= CircuitCount) return "装置槽位不存在。";
            return DescribeCircuit(_state.Circuits[slot]);
        }

        /// <summary>目录规则只读取定义值；实际目标、护盾和连锁结果由预览命令结算。</summary>
        public string DescribeModule(string id)
        {
            ForgeModule module;
            if (id == null || !_modules.TryGetValue(id, out module)) return "没有这个模块。";
            if (module.Kind != ForgeModuleKind.Effect)
                return string.IsNullOrWhiteSpace(module.Description) ? module.Name : module.Description.Trim();
            string rule = "每次消耗" + module.SparkCost + "火花，";
            switch (module.Effect)
            {
                case ForgeEffect.Bolt: rule += "对目标造成" + module.Value + "点直伤。"; break;
                case ForgeEffect.Barrier: rule += "获得" + module.Value + "点护盾。"; break;
                case ForgeEffect.Charge: rule += "获得" + module.Value + "火花，不超过火花上限。"; break;
                case ForgeEffect.Ignite: rule += "给目标施加" + module.Value + "层燃烧。"; break;
                case ForgeEffect.Frost: rule += "给目标施加" + module.Value + "层冻结。"; break;
                default: rule += "对目标造成" + module.Value + "点直伤，并按实际生命伤害恢复生命。"; break;
            }
            return WithDescription(rule, module.Description);
        }

        public string DescribeCard(string id)
        {
            ForgeCard card;
            if (id == null || !_cards.TryGetValue(id, out card)) return "没有这张卡牌。";
            string rule = "消耗" + card.Cost + "能量";
            if (card.Kind == ForgeCardKind.Discharge)
            {
                if (card.SparkCost > 0)
                    rule += "，先支付" + card.SparkCost + "固定火花，再耗尽剩余火花；每点用于放电的火花造成" + card.Power + "点直伤。";
                else rule += "，耗尽全部火花，每点火花造成" + card.Power + "点直伤。";
                rule += card.SparkCost > 0 ? "支付固定费用后至少需留有1火花。" : "至少需要1火花才能放电。";
            }
            else
            {
                if (card.SparkCost > 0) rule += "、" + card.SparkCost + "火花";
                rule += "，";
                switch (card.Kind)
                {
                    case ForgeCardKind.Strike: rule += "对目标造成" + card.Power + "点直伤。"; break;
                    case ForgeCardKind.Guard: rule += "获得" + card.Power + "点护盾。"; break;
                    case ForgeCardKind.Charge: rule += "获得" + card.Power + "火花，不超过火花上限。"; break;
                    default: rule += "给目标施加" + card.Power + "层燃烧。"; break;
                }
            }
            return WithDescription(rule, card.Description);
        }

        private static string WithDescription(string rule, string description)
        {
            return string.IsNullOrWhiteSpace(description) ? rule : rule + " " + description.Trim();
        }

        public string DescribeIntent(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= _state.Enemies.Count) return "没有这个敌人。";
            ForgeEnemyState enemy = _state.Enemies[enemyIndex];
            if (enemy.Health <= 0) return "已倒下";
            ForgeIntent intent = EnemyTemplate(_state, enemyIndex).Intents[enemy.IntentIndex];
            string text;
            switch (intent.Kind)
            {
                case ForgeIntentKind.Attack: text = "攻击 " + AttackPower(_state, enemy, intent); break;
                case ForgeIntentKind.DoubleAttack: text = "连击 " + AttackPower(_state, enemy, intent) + " × 2"; break;
                case ForgeIntentKind.Guard: text = "获得 " + intent.Power + " 护盾"; break;
                case ForgeIntentKind.DrainSpark: text = "吸取 " + Math.Min(intent.Power, _state.Spark) + " 火花（至多" + intent.Power + "）"; break;
                default: text = "蓄力 +" + intent.Power + "（下次攻击）"; break;
            }
            if (enemy.Frost > 0) text = "冻结：本次跳过；原意图「" + text + "」";
            return text;
        }

        private ForgeResult Execute(Action<Command> operation)
        {
            ForgeState ignored;
            return Execute(operation, true, out ignored);
        }

        private ForgeResult Execute(Action<Command> operation, bool commit, out ForgeState outcome)
        {
            outcome = ForgeCopies.State(_state);
            if (_executing) return Failure("正在结算上一项操作，请稍后再试。");
            _executing = true;
            try
            {
                var command = new Command { State = ForgeCopies.State(_state) };
                operation(command);
                ResolveQueue(command);
                ValidateState(command.State);
                outcome = ForgeCopies.State(command.State);
                if (commit)
                {
                    _state = command.State;
                    NotifyChanged();
                }
                return new ForgeResult { Success = true, Events = command.Events };
            }
            catch (RuleFailure exception) { return Failure(exception.Message); }
            catch (OverflowException) { return Failure("数值过大，本次操作已撤回。"); }
            finally { _executing = false; }
        }

        private static ForgeResult Failure(string error)
        {
            return new ForgeResult { Success = false, Error = error };
        }

        private void NotifyChanged()
        {
            Action<ForgeState> observers = Changed;
            if (observers == null) return;
            foreach (Delegate observer in observers.GetInvocationList())
            {
                try { ((Action<ForgeState>)observer)(ForgeCopies.State(_state)); }
                catch (Exception) { /* 显示观察者不能破坏已经提交的规则操作，也不能阻断其余观察者。 */ }
            }
        }

        private void Step(Command command)
        {
            command.Steps++;
            Require(command.Steps <= _definition.MaxEventsPerCommand, "连锁超过单次事件上限，本次操作已撤回。请调整装置组合。");
        }

        private void Log(Command command, string kind, string text, int enemy = -1, int circuit = -1, int value = 0)
        {
            Step(command);
            command.Events.Add(new ForgeEvent { Kind = kind, Text = text, EnemyIndex = enemy, CircuitIndex = circuit, Value = value });
        }

        private static void RequirePhase(ForgeState state, ForgePhase phase, string message)
        {
            Require(state.Phase == phase, message);
        }

        private static void RequireSlot(int slot)
        {
            Require(slot >= 0 && slot < CircuitCount, "装置槽位不存在。");
        }

        private static void RequireTarget(ForgeState state, int enemy)
        {
            Require(enemy >= 0 && enemy < state.Enemies.Count && state.Enemies[enemy].Health > 0, "请选择仍然存活的敌人。");
        }

        private ForgeEncounter Encounter(ForgeState state) { return _definition.Encounters[state.EncounterIndex]; }
        private ForgeEnemyTemplate EnemyTemplate(ForgeState state, int enemy) { return Encounter(state).Enemies[enemy]; }
        private string EnemyName(ForgeState state, int enemy) { return EnemyTemplate(state, enemy).Name; }

        private static bool NeedsEnemy(ForgeEffect effect)
        {
            return effect == ForgeEffect.Bolt || effect == ForgeEffect.Ignite || effect == ForgeEffect.Frost || effect == ForgeEffect.Drain;
        }

        private ForgeModifier Modifier(ForgeCircuit circuit)
        {
            return circuit.ModifierId.Length == 0 ? ForgeModifier.None : _modules[circuit.ModifierId].Modifier;
        }

        private string DescribeCircuit(ForgeCircuit circuit)
        {
            if (circuit.TriggerId.Length == 0) return "空槽";
            ForgeModule trigger = _modules[circuit.TriggerId];
            ForgeModule effect = _modules[circuit.EffectId];
            ForgeModifier modifier = Modifier(circuit);
            int value = EffectiveValue(effect.Value, modifier);
            string effectText;
            switch (effect.Effect)
            {
                case ForgeEffect.Bolt: effectText = "造成" + value + "伤害"; break;
                case ForgeEffect.Barrier: effectText = "获得" + value + "护盾"; break;
                case ForgeEffect.Charge: effectText = "获得" + value + "火花"; break;
                case ForgeEffect.Ignite: effectText = "施加" + value + "燃烧"; break;
                case ForgeEffect.Frost: effectText = "施加" + value + "冻结"; break;
                default: effectText = "造成" + value + "伤害并按实际伤害治疗"; break;
            }
            if (modifier == ForgeModifier.Fork) effectText += NeedsEnemy(effect.Effect) ? "，命中所有敌人" : "（分流使数值减1）";
            if (modifier == ForgeModifier.Echo) effectText += "，执行2次";
            if (modifier == ForgeModifier.Fork || modifier == ForgeModifier.Echo) effectText += "（强度最低1）";
            string result = trigger.Name + " → " + effectText;
            if (circuit.ModifierId.Length > 0) result += " · " + _modules[circuit.ModifierId].Name;
            int spark = effect.SparkCost + (modifier == ForgeModifier.Overload ? 1 : 0);
            result += "；消耗" + (trigger.Trigger == ForgeTrigger.Manual ? "1能量、" : "") + spark + "火花";
            result += "；每回合至多" + _definition.MaxCircuitActivations + "次";
            return result;
        }

        private static int EffectiveValue(int power, ForgeModifier modifier)
        {
            if (modifier == ForgeModifier.Fork || modifier == ForgeModifier.Echo) return Math.Max(1, power - 1);
            return modifier == ForgeModifier.Overload ? checked(power + 2) : power;
        }

        private void Activate(Command command, int slot, int target, string source, bool manual)
        {
            ForgeState state = command.State;
            ForgeCircuit circuit = state.Circuits[slot];
            ForgeModule effect = _modules[circuit.EffectId];
            ForgeModifier modifier = Modifier(circuit);
            string label = "装置" + (slot + 1) + "「" + effect.Name + "」";
            if (state.CircuitActivations[slot] >= _definition.MaxCircuitActivations)
            {
                if (manual) throw new RuleFailure("这个装置本回合的启动次数已用完。");
                Log(command, "Suppressed", source + " → " + label + "已达本回合上限，跳过。", target, slot);
                return;
            }
            if (NeedsEnemy(effect.Effect))
            {
                if (manual) RequireTarget(state, target);
                else target = AutoTarget(state, target);
                if (target < 0) return;
            }
            else target = AutoTarget(state, target);
            int sparkCost = effect.SparkCost + (modifier == ForgeModifier.Overload ? 1 : 0);
            if (manual) Require(state.Energy >= 1, "启动装置需要1点能量。");
            if (state.Spark < sparkCost)
            {
                if (manual) throw new RuleFailure("火花不足，启动这个装置需要" + sparkCost + "点。");
                Log(command, "Suppressed", source + " → " + label + "需要" + sparkCost + "火花，现有" + state.Spark + "，跳过。", target, slot);
                return;
            }
            // 费用和次数先预占，之后才把资源事件与效果放进同一个FIFO队列。
            state.CircuitActivations[slot]++;
            if (NeedsEnemy(effect.Effect)) state.SelectedEnemy = target;
            if (manual) state.Energy--;
            state.Spark -= sparkCost;
            string chain = source + " → " + label;
            Log(command, "CircuitActivated", chain + "，消耗" + (manual ? "1能量、" : "") + sparkCost + "火花（" + state.CircuitActivations[slot] + "/" + _definition.MaxCircuitActivations + "）。", target, slot, sparkCost);
            if (sparkCost > 0) Trigger(command, ForgeTrigger.SparkSpent, target, chain + "消耗" + sparkCost + "火花");
            command.Queue.Enqueue(new Pending { Kind = PendingKind.Circuit, CircuitIndex = slot, EnemyIndex = target, Source = chain });
        }

        private static int AutoTarget(ForgeState state, int related)
        {
            if (related >= 0 && related < state.Enemies.Count && state.Enemies[related].Health > 0) return related;
            if (state.SelectedEnemy >= 0 && state.SelectedEnemy < state.Enemies.Count && state.Enemies[state.SelectedEnemy].Health > 0) return state.SelectedEnemy;
            for (int i = 0; i < state.Enemies.Count; i++) if (state.Enemies[i].Health > 0) return i;
            return -1;
        }

        private static void UpdateSelectedEnemy(ForgeState state)
        {
            state.SelectedEnemy = AutoTarget(state, state.SelectedEnemy);
        }

        private static void Trigger(Command command, ForgeTrigger trigger, int enemy, string source)
        {
            command.Queue.Enqueue(new Pending { Kind = PendingKind.Trigger, Trigger = trigger, EnemyIndex = enemy, Source = source });
        }

        private void ResolveQueue(Command command)
        {
            while (command.Queue.Count > 0 && command.State.Phase == ForgePhase.Battle)
            {
                Step(command);
                Pending pending = command.Queue.Dequeue();
                if (pending.Kind == PendingKind.Trigger)
                {
                    for (int slot = 0; slot < CircuitCount; slot++)
                    {
                        ForgeCircuit circuit = command.State.Circuits[slot];
                        if (circuit.TriggerId.Length == 0) continue;
                        ForgeTrigger trigger = _modules[circuit.TriggerId].Trigger;
                        if (trigger != ForgeTrigger.Manual && trigger == pending.Trigger)
                            Activate(command, slot, pending.EnemyIndex, pending.Source, false);
                    }
                }
                else if (pending.Kind == PendingKind.Circuit) ResolveCircuit(command, pending);
                else ResolveCard(command, pending);
            }
            if (command.State.Phase != ForgePhase.Battle) command.Queue.Clear();
        }

        private void ResolveCircuit(Command command, Pending pending)
        {
            ForgeCircuit circuit = command.State.Circuits[pending.CircuitIndex];
            ForgeModule effect = _modules[circuit.EffectId];
            ForgeModifier modifier = Modifier(circuit);
            int value = EffectiveValue(effect.Value, modifier);
            int repeats = modifier == ForgeModifier.Echo ? 2 : 1;
            for (int repeat = 0; repeat < repeats && command.State.Phase == ForgePhase.Battle; repeat++)
            {
                string source = pending.Source + (repeats > 1 ? "·回响" + (repeat + 1) : "");
                if (modifier == ForgeModifier.Fork && NeedsEnemy(effect.Effect))
                {
                    for (int i = 0; i < command.State.Enemies.Count && command.State.Phase == ForgePhase.Battle; i++)
                        if (command.State.Enemies[i].Health > 0) ApplyEffect(command, effect.Effect, value, i, source, pending.CircuitIndex);
                }
                else
                {
                    int target = AutoTarget(command.State, pending.EnemyIndex);
                    ApplyEffect(command, effect.Effect, value, target, source, pending.CircuitIndex);
                }
            }
        }

        private void ResolveCard(Command command, Pending pending)
        {
            ForgeEffect effect;
            switch (pending.CardKind)
            {
                case ForgeCardKind.Guard: effect = ForgeEffect.Barrier; break;
                case ForgeCardKind.Charge: effect = ForgeEffect.Charge; break;
                case ForgeCardKind.Ignite: effect = ForgeEffect.Ignite; break;
                default: effect = ForgeEffect.Bolt; break;
            }
            ApplyEffect(command, effect, pending.Power, AutoTarget(command.State, pending.EnemyIndex), pending.Source, -1);
        }

        private void ApplyEffect(Command command, ForgeEffect effect, int power, int target, string source, int circuit)
        {
            ForgeState state = command.State;
            if (NeedsEnemy(effect) && target < 0) return;
            switch (effect)
            {
                case ForgeEffect.Bolt:
                    HitEnemy(command, target, power, false, source, circuit);
                    break;
                case ForgeEffect.Drain:
                    HitEnemy(command, target, power, true, source, circuit);
                    break;
                case ForgeEffect.Barrier:
                    state.PlayerBlock = checked(state.PlayerBlock + power);
                    Log(command, "BlockGained", source + " → 获得" + power + "护盾，现有" + state.PlayerBlock + "。", -1, circuit, power);
                    break;
                case ForgeEffect.Charge:
                    int gained = Math.Min(power, _definition.SparkCap - state.Spark);
                    state.Spark += gained;
                    Log(command, "SparkGained", source + " → 获得" + gained + "火花，现有" + state.Spark + "/" + _definition.SparkCap + "。", -1, circuit, gained);
                    break;
                case ForgeEffect.Ignite:
                    state.Enemies[target].Burn = checked(state.Enemies[target].Burn + power);
                    Log(command, "Ignited", source + " → " + EnemyName(state, target) + "增加" + power + "燃烧，现有" + state.Enemies[target].Burn + "层。", target, circuit, power);
                    break;
                case ForgeEffect.Frost:
                    state.Enemies[target].Frost = checked(state.Enemies[target].Frost + power);
                    Log(command, "Frosted", source + " → " + EnemyName(state, target) + "增加" + power + "冻结，现有" + state.Enemies[target].Frost + "层。", target, circuit, power);
                    break;
            }
        }

        private void HitEnemy(Command command, int target, int power, bool drain, string source, int circuit)
        {
            ForgeState state = command.State;
            ForgeEnemyState enemy = state.Enemies[target];
            int blocked = Math.Min(enemy.Block, power);
            enemy.Block -= blocked;
            int damage = Math.Min(enemy.Health, power - blocked);
            enemy.Health -= damage;
            Log(command, "EnemyHit", source + " → " + EnemyName(state, target) + "承受" + damage + "伤害" + (blocked > 0 ? "，护盾吸收" + blocked : "") + "，生命" + enemy.Health + "。", target, circuit, damage);
            UpdateSelectedEnemy(state);
            // 直接命中才反伤；致命命中仍会反弹。双方同时倒下时先判玩家失败。
            int reflect = EnemyTemplate(state, target).ReflectDamage;
            if (power > 0 && reflect > 0)
                HurtPlayer(command, reflect, target, source + " → " + EnemyName(state, target) + "反弹");
            if (state.PlayerHealth <= 0) return;
            if (drain && damage > 0)
            {
                int healed = Math.Min(damage, _definition.MaxHealth - state.PlayerHealth);
                state.PlayerHealth += healed;
                Log(command, "Healed", source + " → 汲取实际伤害" + damage + "，恢复" + healed + "生命。", target, circuit, healed);
            }
            CheckOutcome(command);
        }

        private void HurtPlayer(Command command, int power, int enemyIndex, string source)
        {
            ForgeState state = command.State;
            int blocked = Math.Min(state.PlayerBlock, power);
            state.PlayerBlock -= blocked;
            int damage = Math.Min(state.PlayerHealth, power - blocked);
            state.PlayerHealth -= damage;
            Log(command, "PlayerHit", source + " → 护盾吸收" + blocked + "，损失" + damage + "生命，剩余" + state.PlayerHealth + "。", enemyIndex, -1, damage);
            if (state.PlayerHealth <= 0)
            {
                state.Phase = ForgePhase.Defeat;
                Log(command, "Defeat", "生命耗尽，本次试炼结束。");
                command.Queue.Clear();
                return;
            }
            if (blocked > 0) Trigger(command, ForgeTrigger.Blocked, enemyIndex, source + "被护盾挡住" + blocked);
            if (damage > 0) Trigger(command, ForgeTrigger.HitTaken, enemyIndex, source + "造成" + damage + "生命损失");
        }

        private void CheckOutcome(Command command)
        {
            ForgeState state = command.State;
            if (state.Phase != ForgePhase.Battle) return;
            if (state.PlayerHealth <= 0)
            {
                state.Phase = ForgePhase.Defeat;
                command.Queue.Clear();
                Log(command, "Defeat", "生命耗尽，本次试炼结束。");
                return;
            }
            foreach (ForgeEnemyState enemy in state.Enemies) if (enemy.Health > 0) return;
            state.SelectedEnemy = -1;
            int healed = Math.Min(_definition.HealAfterBattle, _definition.MaxHealth - state.PlayerHealth);
            state.PlayerHealth += healed;
            bool final = state.EncounterIndex == _definition.Encounters.Count - 1;
            state.Phase = final ? ForgePhase.Victory : ForgePhase.Reward;
            command.Queue.Clear();
            Log(command, final ? "Victory" : "BattleWon", (final ? "全部试炼完成。" : "战斗获胜，选择一个模块。") + (healed > 0 ? "恢复" + healed + "生命。" : ""), -1, -1, healed);
        }

        private int AttackPower(ForgeState state, ForgeEnemyState enemy, ForgeIntent intent)
        {
            // 1～4回合为基础攻击，之后每完成4回合增加1点，意图显示使用同一计算。
            return checked(intent.Power + enemy.ChargedPower + Math.Max(0, (state.Turn - 1) / 4));
        }

        private void ExecuteIntent(Command command, int index)
        {
            ForgeState state = command.State;
            ForgeEnemyState enemy = state.Enemies[index];
            ForgeEnemyTemplate template = EnemyTemplate(state, index);
            ForgeIntent intent = template.Intents[enemy.IntentIndex];
            string source = EnemyName(state, index) + (string.IsNullOrWhiteSpace(intent.Name) ? "行动" : "「" + intent.Name + "」");
            enemy.IntentIndex = (enemy.IntentIndex + 1) % template.Intents.Count;
            switch (intent.Kind)
            {
                case ForgeIntentKind.Attack:
                case ForgeIntentKind.DoubleAttack:
                    int attack = AttackPower(state, enemy, intent);
                    enemy.ChargedPower = 0;
                    int count = intent.Kind == ForgeIntentKind.DoubleAttack ? 2 : 1;
                    for (int hit = 0; hit < count && state.Phase == ForgePhase.Battle && enemy.Health > 0; hit++)
                    {
                        HurtPlayer(command, attack, index, source + (count > 1 ? "第" + (hit + 1) + "击" : ""));
                        ResolveQueue(command);
                    }
                    break;
                case ForgeIntentKind.Guard:
                    enemy.Block = intent.Power;
                    Log(command, "EnemyGuarded", source + " → 获得" + intent.Power + "护盾。", index, -1, intent.Power);
                    break;
                case ForgeIntentKind.DrainSpark:
                    int drained = Math.Min(state.Spark, intent.Power);
                    state.Spark -= drained;
                    // 被吸走不是主动花费，因此不触发“消耗火花”。
                    Log(command, "SparkDrained", source + " → 吸走" + drained + "火花，剩余" + state.Spark + "。", index, -1, drained);
                    break;
                case ForgeIntentKind.Charge:
                    enemy.ChargedPower = checked(enemy.ChargedPower + intent.Power);
                    Log(command, "EnemyCharged", source + " → 下次攻击额外增加" + enemy.ChargedPower + "。", index, -1, intent.Power);
                    break;
            }
        }

        private static void ResetActivations(ForgeState state)
        {
            for (int i = 0; i < CircuitCount; i++) state.CircuitActivations[i] = 0;
        }

        private void DrawHand(ForgeState state)
        {
            while (state.Hand.Count < _definition.HandSize)
            {
                if (state.DrawPile.Count == 0)
                {
                    if (state.DiscardPile.Count == 0) break;
                    state.DrawPile.AddRange(state.DiscardPile);
                    state.DiscardPile.Clear();
                    Shuffle(state.DrawPile, state);
                }
                int top = state.DrawPile.Count - 1;
                state.Hand.Add(state.DrawPile[top]);
                state.DrawPile.RemoveAt(top);
            }
        }

        private static uint NextRandom(ForgeState state)
        {
            uint value = unchecked((uint)state.RandomState);
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state.RandomState = unchecked((int)value);
            return value;
        }

        private static void Shuffle(List<string> cards, ForgeState state)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                uint bound = (uint)(i + 1);
                // 拒绝低端余数，避免取模偏差；所有拒绝也推进并保存同一随机状态。
                // xorshift32遍历1～uint.MaxValue，因此先减1得到0～uint.MaxValue-1。
                uint threshold = uint.MaxValue % bound;
                uint value;
                do { value = NextRandom(state) - 1u; } while (value < threshold);
                int other = (int)(value % bound);
                string card = cards[i];
                cards[i] = cards[other];
                cards[other] = card;
            }
        }
    }
}
