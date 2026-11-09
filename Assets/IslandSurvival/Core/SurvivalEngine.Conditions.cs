using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        /// <summary>
        /// 数学状态拥有严重度、护理与期限；状态牌以唯一ConditionId引用它，并拥有真实卡牌位置与任务锁。
        /// 本方法只补齐与结束实例，不会重新生成伤口、重置异常或恢复已经支付的材料。
        /// </summary>
        private void SyncConditionCards(SurvivalState s)
        {
            var expected = new Dictionary<string, CardRule>(StringComparer.Ordinal);
            CardRule woundCard = def.Cards.Find(c => c.Kind == CardKind.Condition && c.WoundCard);
            foreach (InjuryState wound in s.Body.Injuries.Where(w => w.Tags != WorkTag.None))
            {
                if (woundCard == null) throw new InvalidOperationException("缺少伤口状态牌配置。");
                expected.Add(wound.Id, woundCard);
            }
            foreach (AfflictionState affliction in s.Body.Afflictions)
            {
                CardRule definition = def.Cards.Find(c => c.Kind == CardKind.Condition && !c.WoundCard && c.ConditionRuleId == affliction.RuleId);
                if (definition == null) throw new InvalidOperationException("缺少异常对应的状态牌配置：" + affliction.RuleId);
                expected.Add(affliction.Id, definition);
            }

            // Ending a mathematical state ends its actual card and any operation that exclusively targets that card.
            foreach (ItemState item in s.Items.Where(i => Card(i.DefinitionId).Kind == CardKind.Condition).ToArray())
            {
                if (!expected.TryGetValue(item.ConditionId ?? "", out CardRule definition))
                {
                    EndConditionCard(s, item);
                    continue;
                }
                if (item.DefinitionId != definition.Id) throw new InvalidOperationException("状态牌定义与所引用的状态不一致。");
                if (s.Items.Count(i => Card(i.DefinitionId).Kind == CardKind.Condition && i.ConditionId == item.ConditionId) != 1)
                    throw new InvalidOperationException("同一个状态存在重复状态牌。");
            }
            foreach (KeyValuePair<string, CardRule> entry in expected.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (s.Items.Exists(i => Card(i.DefinitionId).Kind == CardKind.Condition && i.ConditionId == entry.Key)) continue;
                ConditionPosition(s, out double x, out double y);
                ItemState card = CreateItem(s, entry.Value.Id, 1, "", Place.Table, -1, x, y);
                if (card == null) throw new InvalidOperationException("无法建立独立状态牌：" + entry.Value.Name);
                card.ConditionId = entry.Key;
                // Conditions never become consumable lots or decay products; one inert lot preserves inventory invariants.
                card.Lots.Clear();
                card.Lots.Add(new Lot { Count = 1, ExpiresAt = 0, AvailableAt = s.Minute });
            }
            foreach (TaskState task in s.Tasks.ToArray())
            {
                task.QueueWounds.RemoveAll(id => !s.Body.Injuries.Exists(w => w.Id == id && w.Tags != WorkTag.None));
                if (!string.IsNullOrEmpty(task.WoundId) && !s.Body.Injuries.Exists(w => w.Id == task.WoundId && w.Tags != WorkTag.None))
                    CancelEndedConditionTask(s, task);
            }
        }

        private void EndConditionCard(SurvivalState s, ItemState card)
        {
            foreach (TaskState task in s.Tasks.Where(t => t.TargetId == card.Id || t.SecondaryId == card.Id || t.LockedItems.Contains(card.Id)).ToArray())
                CancelEndedConditionTask(s, task);
            s.Items.Remove(card);
        }

        private void CancelEndedConditionTask(SurvivalState s, TaskState task)
        {
            if (!s.Tasks.Contains(task)) return;
            CancelStartedTask(s, task);
            AddLog(s, "对应状态已结束，当前护理已停止。已投入的材料保留为本次消耗。");
        }

        private InjuryState ConditionWound(SurvivalState s, ItemState card)
        {
            if (card == null) return null;
            CardRule definition = Card(card.DefinitionId);
            if (definition == null || definition.Kind != CardKind.Condition || string.IsNullOrEmpty(card.ConditionId)) return null;
            string woundId = card.ConditionId;
            if (!definition.WoundCard)
            {
                AfflictionState affliction = s.Body.Afflictions.Find(a => a.Id == card.ConditionId && a.RuleId == definition.ConditionRuleId);
                if (affliction == null || string.IsNullOrEmpty(affliction.InjuryId)) return null;
                woundId = affliction.InjuryId;
            }
            return s.Body.Injuries.Find(w => w.Id == woundId && w.Tags != WorkTag.None);
        }

        private void ConditionPosition(SurvivalState s, out double x, out double y)
        {
            int columns = Math.Max(1, (int)Math.Floor(def.Width / def.CellWidth));
            int rows = Math.Max(1, (int)Math.Floor(def.Height / def.CellHeight));
            ItemState[] conditions = s.Items.Where(i => Card(i.DefinitionId).Kind == CardKind.Condition).ToArray();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    double candidateX = column * def.CellWidth, candidateY = row * def.CellHeight;
                    if (!conditions.Any(i => i.X + def.CellWidth > candidateX && candidateX + def.CellWidth > i.X
                        && i.Y + def.CellHeight > candidateY && candidateY + def.CellHeight > i.Y))
                    {
                        x = candidateX; y = candidateY; return;
                    }
                }
            // A full condition desk cannot prevent a genuine injury. Cascade deterministically within its own bounds.
            int cell = conditions.Length % (columns * rows);
            double inset = Math.Min(def.CellWidth, def.CellHeight) / (1 + conditions.Length / (columns * rows));
            x = Math.Min(Math.Max(0, def.Width - def.CellWidth), cell % columns * def.CellWidth + inset);
            y = Math.Min(Math.Max(0, def.Height - def.CellHeight), cell / columns * def.CellHeight + inset);
        }
    }
}
