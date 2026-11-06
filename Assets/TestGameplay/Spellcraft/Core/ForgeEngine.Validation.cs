using System;
using System.Collections.Generic;

namespace CardKKK.Spellcraft.Core
{
    public sealed partial class ForgeEngine
    {
        private sealed class RuleFailure : Exception
        {
            internal RuleFailure(string message) : base(message) { }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new RuleFailure(message);
        }

        private static bool Known<T>(T value) where T : struct
        {
            return Enum.IsDefined(typeof(T), value);
        }

        private static bool IdValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        }

        private static void ValidateDefinition(ForgeDefinition value)
        {
            Require(value != null, "缺少法术规则配置。");
            Require(value.MaxHealth > 0 && value.MaxHealth <= 100000, "玩家生命上限须为1～100000。");
            Require(value.EnergyPerTurn > 0 && value.EnergyPerTurn <= 100, "每回合能量须为1～100。");
            Require(value.SparkCap > 0 && value.SparkCap <= 10000, "火花上限须为1～10000。");
            Require(value.StartingSpark >= 0 && value.StartingSpark <= value.SparkCap, "开局火花超出上限。");
            Require(value.HandSize > 0 && value.HandSize <= 100, "每回合手牌数须为1～100。");
            Require(value.MaxCircuitActivations > 0 && value.MaxCircuitActivations <= 100, "装置每回合次数须为1～100。");
            Require(value.MaxEventsPerCommand >= 1 && value.MaxEventsPerCommand <= 10000, "单次连锁事件上限须为1～10000。");
            Require(value.HealAfterBattle >= 0 && value.HealAfterBattle <= value.MaxHealth, "战后治疗须在0到生命上限之间。");
            Require(value.Modules != null && value.Modules.Count > 0 && value.Modules.Count <= 1000, "模块目录为空或超过1000项。");
            Require(value.Cards != null && value.Cards.Count > 0 && value.Cards.Count <= 1000, "卡牌目录为空或超过1000项。");
            Require(value.StartingDeck != null && value.StartingDeck.Count > 0 && value.StartingDeck.Count <= 1000, "初始牌组须为1～1000张。");
            Require(value.StartingUnlocked != null, "缺少初始解锁列表。");
            Require(value.StartingCircuits != null && value.StartingCircuits.Count == CircuitCount, "初始装置必须恰好有3个槽位。");
            Require(value.Encounters != null && value.Encounters.Count > 0 && value.Encounters.Count <= 100, "遭遇须为1～100关。");
            var modules = new Dictionary<string, ForgeModule>(StringComparer.Ordinal);
            foreach (ForgeModule item in value.Modules)
            {
                Require(item != null && IdValid(item.Id), "模块含空项或无效ID。");
                Require(!modules.ContainsKey(item.Id), "模块ID重复：" + item.Id);
                Require(!string.IsNullOrWhiteSpace(item.Name), "模块缺少名称：" + item.Id);
                Require(Known(item.Kind) && Known(item.Trigger) && Known(item.Effect) && Known(item.Modifier), "模块枚举无效：" + item.Id);
                Require(item.Value >= 0 && item.Value <= 10000 && item.SparkCost >= 0 && item.SparkCost <= value.SparkCap, "模块数值或火花费用无效：" + item.Id);
                modules.Add(item.Id, item);
            }
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (ForgeCard item in value.Cards)
            {
                Require(item != null && IdValid(item.Id) && cards.Add(item.Id), "卡牌含空项、无效ID或重复ID。");
                Require(!string.IsNullOrWhiteSpace(item.Name) && Known(item.Kind), "卡牌名称或类型无效：" + item.Id);
                Require(item.Cost >= 0 && item.Cost <= value.EnergyPerTurn && item.SparkCost >= 0 && item.SparkCost <= value.SparkCap && item.Power > 0 && item.Power <= 10000, "卡牌费用或威力无效：" + item.Id);
            }
            foreach (string id in value.StartingDeck) Require(id != null && cards.Contains(id), "初始牌组引用了未知卡牌：" + id);
            var unlocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in value.StartingUnlocked)
                Require(id != null && modules.ContainsKey(id) && unlocked.Add(id), "初始解锁含未知或重复模块：" + id);
            foreach (ForgeCircuit circuit in value.StartingCircuits) ValidateCircuit(circuit, modules, unlocked);
            var encounters = new HashSet<string>(StringComparer.Ordinal);
            for (int encounterIndex = 0; encounterIndex < value.Encounters.Count; encounterIndex++)
            {
                ForgeEncounter encounter = value.Encounters[encounterIndex];
                Require(encounter != null && IdValid(encounter.Id) && encounters.Add(encounter.Id), "遭遇含空项、无效ID或重复ID。");
                Require(!string.IsNullOrWhiteSpace(encounter.Name), "遭遇缺少名称：" + encounter.Id);
                Require(encounter.Enemies != null && encounter.Enemies.Count > 0 && encounter.Enemies.Count <= 12, "每关敌人数须为1～12。");
                Require(encounter.RewardModuleIds != null, "遭遇缺少奖励列表：" + encounter.Id);
                var enemyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (ForgeEnemyTemplate enemy in encounter.Enemies)
                {
                    Require(enemy != null && IdValid(enemy.Id) && enemyIds.Add(enemy.Id), "同关敌人含空项、无效ID或重复ID。");
                    Require(!string.IsNullOrWhiteSpace(enemy.Name) && enemy.MaxHealth > 0 && enemy.MaxHealth <= 100000 && enemy.ReflectDamage >= 0 && enemy.ReflectDamage <= 10000, "敌人名称、生命或反伤无效：" + enemy.Id);
                    Require(enemy.Intents != null && enemy.Intents.Count > 0 && enemy.Intents.Count <= 100, "敌人必须配置1～100个意图：" + enemy.Id);
                    foreach (ForgeIntent intent in enemy.Intents)
                        Require(intent != null && Known(intent.Kind) && intent.Power > 0 && intent.Power <= 10000, "敌人意图类型或数值无效：" + enemy.Id);
                }
                var rewards = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in encounter.RewardModuleIds)
                    Require(id != null && modules.ContainsKey(id) && rewards.Add(id), "奖励含未知或重复模块：" + id);
                // 允许奖励池重叠，但不能存在一种合法先前选择让本关奖励全部领过。
                if (encounterIndex < value.Encounters.Count - 1)
                    Require(!RewardPoolCanBeExhausted(value, encounterIndex, unlocked, rewards), "存在先前选择会令本关没有可解锁奖励：" + encounter.Id);
            }
        }

        private static void ValidateCircuit(ForgeCircuit circuit, Dictionary<string, ForgeModule> modules, HashSet<string> unlocked)
        {
            Require(circuit != null && circuit.TriggerId != null && circuit.EffectId != null && circuit.ModifierId != null, "装置槽位或模块ID为空。");
            if (circuit.TriggerId.Length == 0 && circuit.EffectId.Length == 0 && circuit.ModifierId.Length == 0) return;
            ForgeModule trigger;
            ForgeModule effect;
            Require(modules.TryGetValue(circuit.TriggerId, out trigger) && trigger.Kind == ForgeModuleKind.Trigger && unlocked.Contains(circuit.TriggerId), "装置触发模块未知、未解锁或类型不符。");
            Require(modules.TryGetValue(circuit.EffectId, out effect) && effect.Kind == ForgeModuleKind.Effect && unlocked.Contains(circuit.EffectId), "装置效果模块未知、未解锁或类型不符。");
            if (circuit.ModifierId.Length == 0) return;
            ForgeModule modifier;
            Require(modules.TryGetValue(circuit.ModifierId, out modifier) && modifier.Kind == ForgeModuleKind.Modifier && unlocked.Contains(circuit.ModifierId), "装置特性模块未知、未解锁或类型不符。");
        }

        private void ValidateState(ForgeState value)
        {
            Require(value != null && Known(value.Phase), "存档状态为空或阶段无效。");
            Require(value.EncounterIndex >= 0 && value.EncounterIndex < _definition.Encounters.Count, "存档关卡超出范围。");
            Require(value.Turn >= 0 && value.Turn <= 1000000 && value.RandomState != 0, "存档回合或随机状态无效。");
            Require(value.PlayerHealth >= 0 && value.PlayerHealth <= _definition.MaxHealth && value.PlayerBlock >= 0 && value.PlayerBlock <= NumericLimit, "存档玩家生命或护盾无效。");
            Require(value.Energy >= 0 && value.Energy <= _definition.EnergyPerTurn && value.Spark >= 0 && value.Spark <= _definition.SparkCap, "存档能量或火花无效。");
            Require(value.Enemies != null && value.Circuits != null && value.UnlockedModuleIds != null && value.Hand != null && value.DrawPile != null && value.DiscardPile != null && value.CircuitActivations != null, "存档列表缺失。");
            Require(value.Circuits.Count == CircuitCount && value.CircuitActivations.Count == CircuitCount, "存档装置槽位数量不符。");
            var unlocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in value.UnlockedModuleIds)
                Require(id != null && _modules.ContainsKey(id) && unlocked.Add(id), "存档解锁模块未知或重复。");
            foreach (string id in _definition.StartingUnlocked) Require(unlocked.Contains(id), "存档缺少初始已解锁模块。");
            var possibleUnlocked = new HashSet<string>(_definition.StartingUnlocked, StringComparer.Ordinal);
            for (int i = 0; i < value.EncounterIndex; i++) possibleUnlocked.UnionWith(_definition.Encounters[i].RewardModuleIds);
            foreach (string id in unlocked) Require(possibleUnlocked.Contains(id), "存档包含尚未到达关卡的奖励。");
            Require(unlocked.Count == _definition.StartingUnlocked.Count + value.EncounterIndex, "存档解锁奖励数量与关卡进度不符。");
            // 一关只领一个奖励；验证解锁集合能够由每关恰选一次得到，拒绝同关多领。
            Require(RewardsCanExplainUnlocks(value.EncounterIndex, unlocked), "存档解锁组合无法由已完成关卡获得。");
            foreach (ForgeCircuit circuit in value.Circuits) ValidateCircuit(circuit, _modules, unlocked);
            for (int i = 0; i < CircuitCount; i++)
            {
                int count = value.CircuitActivations[i];
                Require(count >= 0 && count <= _definition.MaxCircuitActivations, "存档装置激活次数无效。");
                Require(value.Circuits[i].EffectId.Length > 0 || count == 0, "存档中的空装置不能有激活记录。");
            }
            Require(value.Hand.Count <= _definition.HandSize, "存档手牌超过上限。");
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            AddCardCounts(counts, value.Hand);
            AddCardCounts(counts, value.DrawPile);
            AddCardCounts(counts, value.DiscardPile);
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            AddCardCounts(expected, _definition.StartingDeck);
            Require(counts.Count == expected.Count, "存档牌堆与初始牌组不符。");
            foreach (KeyValuePair<string, int> pair in expected)
            {
                int actual;
                Require(counts.TryGetValue(pair.Key, out actual) && actual == pair.Value, "存档卡牌总数不符：" + pair.Key);
            }
            if (value.Phase == ForgePhase.Workshop)
            {
                Require(value.PlayerHealth > 0 && value.Enemies.Count == 0 && value.SelectedEnemy == -1 && value.Turn == 0, "工坊存档包含战斗状态。");
                Require(value.PlayerBlock == 0 && value.Energy == _definition.EnergyPerTurn && value.Spark == _definition.StartingSpark && value.Hand.Count == 0 && value.DiscardPile.Count == 0, "工坊存档资源或牌堆状态无效。");
                foreach (int count in value.CircuitActivations) Require(count == 0, "工坊装置次数必须归零。");
                return;
            }
            ForgeEncounter encounter = _definition.Encounters[value.EncounterIndex];
            Require(value.Turn > 0 && value.Enemies.Count == encounter.Enemies.Count, "存档敌人数量或战斗回合无效。");
            bool alive = false;
            for (int i = 0; i < value.Enemies.Count; i++)
            {
                ForgeEnemyState enemy = value.Enemies[i];
                ForgeEnemyTemplate template = encounter.Enemies[i];
                Require(enemy != null && enemy.DefinitionId == template.Id, "存档敌人顺序或定义不符。");
                Require(enemy.Health >= 0 && enemy.Health <= template.MaxHealth && enemy.Block >= 0 && enemy.Block <= NumericLimit && enemy.Burn >= 0 && enemy.Burn <= NumericLimit && enemy.Frost >= 0 && enemy.Frost <= NumericLimit && enemy.ChargedPower >= 0 && enemy.ChargedPower <= NumericLimit, "存档敌人生命或状态层数无效。");
                Require(enemy.IntentIndex >= 0 && enemy.IntentIndex < template.Intents.Count, "存档敌人意图索引无效。");
                alive |= enemy.Health > 0;
            }
            Require(value.SelectedEnemy >= -1 && value.SelectedEnemy < value.Enemies.Count, "存档选中敌人超出范围。");
            Require(value.SelectedEnemy == -1 || value.Enemies[value.SelectedEnemy].Health > 0, "存档选中的是已倒下的敌人。");
            if (value.Phase == ForgePhase.Battle) Require(value.PlayerHealth > 0 && alive && value.SelectedEnemy >= 0, "战斗存档没有存活双方或目标。");
            if (value.Phase == ForgePhase.Defeat) Require(value.PlayerHealth == 0 && value.PlayerBlock == 0, "失败存档中的玩家仍有生命或护盾。");
            if (value.Phase == ForgePhase.Reward || value.Phase == ForgePhase.Victory)
            {
                Require(value.PlayerHealth > 0 && !alive && value.SelectedEnemy == -1, "战后存档仍有敌人或玩家已倒下。");
                bool final = value.EncounterIndex == _definition.Encounters.Count - 1;
                Require((value.Phase == ForgePhase.Victory) == final, "存档通关阶段与关卡进度不符。");
                if (!final)
                {
                    bool reward = false;
                    foreach (string id in encounter.RewardModuleIds) if (!unlocked.Contains(id)) reward = true;
                    Require(reward, "战后没有可以领取的未解锁模块。");
                }
            }
        }

        private static void AddCardCounts(Dictionary<string, int> counts, List<string> cards)
        {
            Require(cards.Count <= 1000, "存档牌堆过大。");
            foreach (string id in cards)
            {
                Require(IdValid(id), "存档卡牌ID为空或过长。");
                int count;
                counts.TryGetValue(id, out count);
                counts[id] = count + 1;
            }
        }

        private bool RewardsCanExplainUnlocks(int completed, HashSet<string> unlocked)
        {
            var initial = new HashSet<string>(_definition.StartingUnlocked, StringComparer.Ordinal);
            var matches = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int encounter = 0; encounter < completed; encounter++)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (!MatchReward(_definition, encounter, unlocked, initial, matches, seen)) return false;
            }
            return true;
        }

        private static bool MatchReward(ForgeDefinition definition, int encounter, HashSet<string> allowed, HashSet<string> initial, Dictionary<string, int> matches, HashSet<string> seen)
        {
            foreach (string id in definition.Encounters[encounter].RewardModuleIds)
            {
                if (!allowed.Contains(id) || initial.Contains(id) || !seen.Add(id)) continue;
                int other;
                if (!matches.TryGetValue(id, out other) || MatchReward(definition, other, allowed, initial, matches, seen))
                {
                    matches[id] = encounter;
                    return true;
                }
            }
            return false;
        }

        private static bool RewardPoolCanBeExhausted(ForgeDefinition definition, int completed, HashSet<string> initial, HashSet<string> pool)
        {
            var required = new List<string>();
            foreach (string id in pool) if (!initial.Contains(id)) required.Add(id);
            if (required.Count > completed) return false;
            var forced = new Dictionary<int, string>();
            foreach (string id in required)
                if (!MatchRequiredReward(definition, id, completed, forced, new HashSet<int>())) return false;
            // 先覆盖必须已经领过的全部模块，再扩充匹配覆盖每个先前关卡。
            var matches = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<int, string> pair in forced) matches.Add(pair.Value, pair.Key);
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < completed; i++) allowed.UnionWith(definition.Encounters[i].RewardModuleIds);
            for (int i = 0; i < completed; i++)
                if (!forced.ContainsKey(i) && !MatchReward(definition, i, allowed, initial, matches, new HashSet<string>(StringComparer.Ordinal))) return false;
            return true;
        }

        private static bool MatchRequiredReward(ForgeDefinition definition, string id, int completed, Dictionary<int, string> matches, HashSet<int> seen)
        {
            for (int i = 0; i < completed; i++)
            {
                if (!definition.Encounters[i].RewardModuleIds.Contains(id) || !seen.Add(i)) continue;
                string other;
                if (!matches.TryGetValue(i, out other) || MatchRequiredReward(definition, other, completed, matches, seen))
                {
                    matches[i] = id;
                    return true;
                }
            }
            return false;
        }
    }
}
