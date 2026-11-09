using System;
using System.Collections.Generic;

namespace CardKKK.Island.Core
{
    internal static class IslandValidation
    {
        internal static void Require(bool valid, string error)
        { if (!valid) throw new ArgumentException(error); }

        private static void Key(string value)
        { Require(!string.IsNullOrWhiteSpace(value), "标识不能为空。"); }

        internal static void Values(List<NumberValue> values, bool allowNegative = false)
        {
            Require(values != null, "数值列表不能为空。");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                Require(value != null, "数值条目不能为空。");
                Key(value.Key);
                Require(keys.Add(value.Key), "数值标识重复：" + value.Key);
                Require(allowNegative || value.Value >= 0, "数值不能为负数：" + value.Key);
            }
        }

        private static void Strings(List<string> values)
        {
            Require(values != null, "标识列表不能为空。");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            { Key(value); Require(keys.Add(value), "标识重复：" + value); }
        }

        private static void Outputs(List<CardAmount> outputs, HashSet<string> ids)
        {
            Require(outputs != null, "产物列表不能为空。");
            foreach (var output in outputs)
            {
                Require(output != null, "产物条目不能为空。");
                Require(output.DefinitionId != null && ids.Contains(output.DefinitionId), "产物定义不存在。");
                Require(output.Quantity > 0, "产物数量必须为正数。");
            }
        }

        internal static void Definition(IslandDefinition definition)
        {
            Require(definition != null, "缺少玩法定义。");
            Key(definition.ContentVersion);
            Require(definition.MaximumInstances > 0, "实例上限必须为正数。");
            Require(definition.MaximumEventBatchesPerAdvance > 0, "单次事件批次上限必须为正数。");
            Require(definition.Cards != null && definition.Recipes != null, "定义列表不能为空。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in definition.Cards)
            {
                Require(card != null, "卡牌定义不能为空。");
                Key(card.Id);
                Require(ids.Add(card.Id), "卡牌定义重复：" + card.Id);
                Require(Enum.IsDefined(typeof(CardKind), card.Kind), "卡牌分类无效。");
                Strings(card.Tags);
                Values(card.InitialValues);
                Require(card.StackLimit > 0, "堆叠上限必须为正数。");
                Require(card.Kind == CardKind.Material || card.StackLimit == 1, "非材料实例必须单张。");
                Require(card.Capacity >= 0 && (card.Kind == CardKind.Region || card.Capacity == 0), "容量仅用于区域。");
                Require(card.LifetimeMinutes >= 0, "寿命不能为负数。");
                Require(card.ExpiryOutputs != null, "耗尽产物列表不能为空。");
                Require(card.LifetimeMinutes > 0 || card.ExpiryOutputs.Count == 0, "耗尽产物需要有限寿命。");
            }
            foreach (var card in definition.Cards) Outputs(card.ExpiryOutputs, ids);
            ids.Clear();
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in definition.Cards) cardIds.Add(card.Id);
            foreach (var recipe in definition.Recipes)
            {
                Require(recipe != null, "配方不能为空。");
                Key(recipe.Id);
                Require(ids.Add(recipe.Id), "配方标识重复。");
                Require(recipe.DurationMinutes > 0, "配方时间必须为正数。");
                Require(recipe.Ingredients != null && recipe.Ingredients.Count > 0, "配方至少需要一个参与对象。");
                var slots = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slot in recipe.Ingredients)
                {
                    Require(slot != null, "配方槽位不能为空。");
                    Key(slot.SlotId);
                    Require(slots.Add(slot.SlotId), "槽位标识重复。");
                    Require(string.IsNullOrEmpty(slot.DefinitionId) || cardIds.Contains(slot.DefinitionId), "投入定义不存在。");
                    Strings(slot.RequiredTags);
                    Values(slot.MinimumValues);
                    Values(slot.ValueChanges, true);
                    Require(slot.Quantity > 0, "投入数量必须为正数。");
                    Require(!slot.Consume || slot.ValueChanges.Count == 0, "消耗材料不再扣实例数值。");
                }
                Outputs(recipe.Outputs, cardIds);
            }
        }

        internal static void Shape(IslandSnapshot snapshot)
        {
            Require(snapshot != null && snapshot.Cards != null && snapshot.Jobs != null, "快照缺少列表。");
            Require(snapshot.SchemaVersion == 1 && snapshot.Minute >= 0 && snapshot.Revision >= 0, "快照版本或时间无效。");
            foreach (var card in snapshot.Cards)
            {
                Require(card != null, "卡牌实例不能为空。");
                Key(card.Id); Key(card.DefinitionId); Values(card.Values);
                Require(!float.IsNaN(card.X) && !float.IsInfinity(card.X) && !float.IsNaN(card.Y) && !float.IsInfinity(card.Y), "坐标无效。");
            }
            foreach (var job in snapshot.Jobs)
            {
                Require(job != null, "任务不能为空。");
                Key(job.Id); Key(job.RecipeId); Strings(job.LockedIds);
            }
        }
    }
}
