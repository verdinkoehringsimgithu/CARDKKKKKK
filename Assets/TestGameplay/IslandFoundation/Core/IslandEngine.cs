using System;
using System.Collections.Generic;

namespace CardKKK.Island.Core
{
    // All mutations run on a candidate snapshot. Call from the owning Unity thread.
    public sealed class IslandEngine
    {
        private readonly IslandDefinition definition;
        private readonly Dictionary<string, CardDefinition> cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeDefinition> recipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        private IslandSnapshot state;
        private bool executing;
        public IslandSnapshot Snapshot => IslandCopy.Snapshot(state);
        public event Action<IslandSnapshot> Changed;
        public event Action<Exception> ObserverFailed;

        public IslandEngine(IslandDefinition definition, IslandSnapshot initial)
        {
            IslandValidation.Definition(definition);
            this.definition = IslandCopy.Definition(definition);
            foreach (var card in this.definition.Cards) cards.Add(card.Id, card);
            foreach (var recipe in this.definition.Recipes) recipes.Add(recipe.Id, recipe);
            IslandValidation.Shape(initial);
            state = IslandCopy.Snapshot(initial);
            Validate(state);
        }

        public IslandResult Restore(IslandSnapshot snapshot)
        {
            return Execute(candidate =>
            {
                IslandValidation.Shape(snapshot);
                return IslandCopy.Snapshot(snapshot);
            });
        }

        public IslandResult Move(string instanceId, string regionId, float x, float y)
        {
            return Execute(candidate =>
            {
                var card = Find(candidate, instanceId);
                IslandValidation.Require(!Locked(candidate, card.Id) || SameRegion(card.RegionId, regionId), "工作中的卡牌不能移动归属。");
                card.RegionId = string.IsNullOrEmpty(regionId) ? null : regionId;
                card.X = x; card.Y = y;
                return candidate;
            });
        }

        public IslandResult StartRecipe(string recipeId, string regionId, IReadOnlyList<SlotBinding> bindings)
        {
            return Execute(candidate =>
            {
                IslandValidation.Require(recipeId != null && recipes.ContainsKey(recipeId), "配方不存在。");
                var recipe = recipes[recipeId];
                IslandValidation.Require(bindings != null && bindings.Count == recipe.Ingredients.Count, "配方绑定数量不符。");
                var slots = new Dictionary<string, string>(StringComparer.Ordinal);
                var selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var binding in bindings)
                {
                    IslandValidation.Require(binding != null && !string.IsNullOrEmpty(binding.SlotId) && !string.IsNullOrEmpty(binding.InstanceId), "槽位绑定无效。");
                    IslandValidation.Require(!slots.ContainsKey(binding.SlotId) && selected.Add(binding.InstanceId), "槽位或参与实例重复。");
                    slots.Add(binding.SlotId, binding.InstanceId);
                }
                var job = new RecipeJob
                {
                    Id = Guid.NewGuid().ToString("N"), RecipeId = recipe.Id,
                    RegionId = string.IsNullOrEmpty(regionId) ? null : regionId,
                    FinishesAt = checked(candidate.Minute + recipe.DurationMinutes)
                };
                foreach (var slot in recipe.Ingredients)
                {
                    IslandValidation.Require(slots.ContainsKey(slot.SlotId), "缺少配方槽位：" + slot.SlotId);
                    var instance = Find(candidate, slots[slot.SlotId]);
                    var card = cards[instance.DefinitionId];
                    IslandValidation.Require(!Locked(candidate, instance.Id), "卡牌已参与其他工作。");
                    IslandValidation.Require(SameRegion(instance.RegionId, job.RegionId) || instance.Id == job.RegionId, "参与对象必须在指定区域内。");
                    IslandValidation.Require(string.IsNullOrEmpty(slot.DefinitionId) || slot.DefinitionId == card.Id, "投入卡牌不匹配。");
                    foreach (var tag in slot.RequiredTags)
                        IslandValidation.Require(card.Tags.Contains(tag), "缺少能力标签：" + tag);
                    IslandValidation.Require(instance.Quantity >= slot.Quantity, "投入数量不足。");
                    IslandValidation.Require(slot.ValueChanges.Count == 0 || instance.Quantity == 1, "带实例损耗的参与对象必须单张。");
                    foreach (var minimum in slot.MinimumValues)
                        IslandValidation.Require(Value(instance, minimum.Key) >= minimum.Value, "数值不足：" + minimum.Key);
                    foreach (var cost in slot.ValueChanges)
                    {
                        var value = instance.Values.Find(item => item.Key == cost.Key);
                        IslandValidation.Require(value != null, "损耗数值不足：" + cost.Key);
                        value.Value = checked(value.Value + cost.Value);
                        IslandValidation.Require(value.Value >= 0, "损耗数值不足：" + cost.Key);
                    }
                    if (slot.Consume)
                    {
                        IslandValidation.Require(card.Kind != CardKind.Region, "区域需先移出内容物再拆除。");
                        instance.Quantity -= slot.Quantity;
                        if (instance.Quantity == 0) candidate.Cards.Remove(instance);
                    }
                    else job.LockedIds.Add(instance.Id);
                }
                candidate.Jobs.Add(job);
                return candidate;
            });
        }

        public IslandResult AdvanceTo(long minute)
        {
            return Execute(candidate =>
            {
                IslandValidation.Require(minute >= candidate.Minute && minute < long.MaxValue, "时间无效或发生倒退。");
                int batches = 0;
                while (true)
                {
                    long next = long.MaxValue;
                    foreach (var card in candidate.Cards)
                        if (card.ExpiresAt > 0 && card.ExpiresAt < next) next = card.ExpiresAt;
                    foreach (var job in candidate.Jobs)
                        if (job.FinishesAt < next) next = job.FinishesAt;
                    if (next == long.MaxValue || next > minute) break;
                    IslandValidation.Require(batches < definition.MaximumEventBatchesPerAdvance, "时间跨度内事件过多，请缩短推进时间。");
                    batches++;
                    candidate.Minute = next;
                    Expire(candidate);
                    var complete = candidate.Jobs.FindAll(job => job.FinishesAt == next);
                    complete.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                    foreach (var job in complete)
                    {
                        candidate.Jobs.Remove(job);
                        Produce(candidate, recipes[job.RecipeId].Outputs, job.RegionId);
                    }
                    Validate(candidate);
                }
                candidate.Minute = minute;
                return candidate;
            });
        }

        private void Expire(IslandSnapshot candidate)
        {
            var expired = candidate.Cards.FindAll(card => card.ExpiresAt > 0 && card.ExpiresAt <= candidate.Minute);
            expired.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var card in expired)
            {
                candidate.Jobs.RemoveAll(job => job.LockedIds.Contains(card.Id));
                foreach (var child in candidate.Cards)
                    if (child.RegionId == card.Id)
                    { child.RegionId = null; child.X += card.X; child.Y += card.Y; }
                foreach (var job in candidate.Jobs)
                    if (job.RegionId == card.Id) job.RegionId = null;
                candidate.Cards.Remove(card);
                var outputs = cards[card.DefinitionId].ExpiryOutputs;
                Produce(candidate, outputs, card.RegionId, card.Quantity);
            }
        }

        private void Produce(IslandSnapshot candidate, List<CardAmount> outputs, string regionId, int multiplier = 1)
        {
            foreach (var output in outputs)
            {
                var card = cards[output.DefinitionId];
                int remaining = checked(output.Quantity * multiplier);
                while (remaining > 0)
                {
                    IslandValidation.Require(candidate.Cards.Count < definition.MaximumInstances, "产物超出实例容量。");
                    int quantity = Math.Min(remaining, card.StackLimit);
                    candidate.Cards.Add(new CardInstance
                    {
                        Id = Guid.NewGuid().ToString("N"), DefinitionId = card.Id, RegionId = regionId,
                        Quantity = quantity, Values = IslandCopy.Values(card.InitialValues),
                        ExpiresAt = card.LifetimeMinutes == 0 ? 0 : checked(candidate.Minute + card.LifetimeMinutes)
                    });
                    remaining -= quantity;
                }
            }
        }

        private void Validate(IslandSnapshot candidate)
        {
            IslandValidation.Shape(candidate);
            IslandValidation.Require(candidate.ContentVersion == definition.ContentVersion, "内容版本不兼容。");
            IslandValidation.Require(candidate.Cards.Count <= definition.MaximumInstances, "实例超出容量。");
            var instances = new Dictionary<string, CardInstance>(StringComparer.Ordinal);
            foreach (var card in candidate.Cards)
            {
                IslandValidation.Require(cards.ContainsKey(card.DefinitionId) && !instances.ContainsKey(card.Id), "卡牌定义缺失或实例重复。");
                instances.Add(card.Id, card);
                var template = cards[card.DefinitionId];
                IslandValidation.Require(card.Quantity > 0 && card.Quantity <= template.StackLimit, "实例数量无效。");
                IslandValidation.Require(card.ExpiresAt == 0 ? template.LifetimeMinutes == 0 : template.LifetimeMinutes > 0 && card.ExpiresAt > candidate.Minute && card.ExpiresAt < long.MaxValue, "寿命数据无效。");
                IslandValidation.Require(card.Values.Count == template.InitialValues.Count, "实例数值结构不符。");
                foreach (var value in template.InitialValues)
                    IslandValidation.Require(card.Values.Exists(item => item.Key == value.Key), "实例缺少数值：" + value.Key);
            }
            var occupancy = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var card in candidate.Cards)
            {
                if (string.IsNullOrEmpty(card.RegionId)) continue;
                CheckRegion(instances, card.RegionId);
                IslandValidation.Require(cards[card.DefinitionId].Kind != CardKind.Region, "区域不能嵌套。");
                occupancy.TryGetValue(card.RegionId, out long count);
                occupancy[card.RegionId] = count + 1;
            }
            var jobIds = new HashSet<string>(StringComparer.Ordinal);
            var locked = new HashSet<string>(StringComparer.Ordinal);
            long reserved = candidate.Cards.Count;
            foreach (var job in candidate.Jobs)
            {
                IslandValidation.Require(jobIds.Add(job.Id) && recipes.ContainsKey(job.RecipeId), "任务重复或配方不存在。");
                IslandValidation.Require(job.FinishesAt > candidate.Minute && job.FinishesAt < long.MaxValue, "任务完成时间无效。");
                if (!string.IsNullOrEmpty(job.RegionId)) CheckRegion(instances, job.RegionId);
                foreach (var id in job.LockedIds)
                    IslandValidation.Require(instances.ContainsKey(id) && locked.Add(id), "任务引用缺失或重复锁定。");
                int retainedIndex = 0;
                foreach (var slot in recipes[job.RecipeId].Ingredients)
                {
                    if (slot.Consume) continue;
                    IslandValidation.Require(retainedIndex < job.LockedIds.Count, "任务缺少参与对象。");
                    var participant = instances[job.LockedIds[retainedIndex++]];
                    var template = cards[participant.DefinitionId];
                    IslandValidation.Require(string.IsNullOrEmpty(slot.DefinitionId) || participant.DefinitionId == slot.DefinitionId, "任务参与对象定义不符。");
                    foreach (var tag in slot.RequiredTags)
                        IslandValidation.Require(template.Tags.Contains(tag), "任务参与对象能力不符。");
                    IslandValidation.Require(participant.Quantity >= slot.Quantity, "任务参与对象数量不足。");
                    IslandValidation.Require(SameRegion(participant.RegionId, job.RegionId) || participant.Id == job.RegionId, "任务参与对象区域不符。");
                }
                IslandValidation.Require(retainedIndex == job.LockedIds.Count, "任务含多余参与对象。");
                long outputCount = OutputCount(recipes[job.RecipeId].Outputs);
                reserved = checked(reserved + outputCount);
                if (!string.IsNullOrEmpty(job.RegionId))
                {
                    occupancy.TryGetValue(job.RegionId, out long count);
                    occupancy[job.RegionId] = checked(count + outputCount);
                }
            }
            IslandValidation.Require(reserved <= definition.MaximumInstances, "当前产物预留超过实例容量。");
            foreach (var region in occupancy)
                IslandValidation.Require(region.Value <= cards[instances[region.Key].DefinitionId].Capacity, "区域容量不足。");
        }

        private long OutputCount(List<CardAmount> outputs)
        {
            long count = 0;
            foreach (var output in outputs)
                count = checked(count + ((long)output.Quantity + cards[output.DefinitionId].StackLimit - 1) / cards[output.DefinitionId].StackLimit);
            return count;
        }

        private void CheckRegion(Dictionary<string, CardInstance> instances, string id)
        { IslandValidation.Require(instances.ContainsKey(id) && cards[instances[id].DefinitionId].Kind == CardKind.Region, "所属区域不存在。"); }

        private static int Value(CardInstance instance, string key)
        { var value = instance.Values.Find(item => item.Key == key); return value == null ? -1 : value.Value; }

        private static bool SameRegion(string a, string b)
        { return string.Equals(a ?? "", b ?? "", StringComparison.Ordinal); }

        private static CardInstance Find(IslandSnapshot snapshot, string id)
        {
            var card = snapshot.Cards.Find(item => item.Id == id);
            IslandValidation.Require(card != null, "卡牌实例不存在。");
            return card;
        }

        private static bool Locked(IslandSnapshot snapshot, string id)
        { return snapshot.Jobs.Exists(job => job.LockedIds.Contains(id)); }

        private IslandResult Execute(Func<IslandSnapshot, IslandSnapshot> command)
        {
            if (executing) return new IslandResult(false, "正在处理上一条命令。");
            executing = true;
            try
            {
                var candidate = command(IslandCopy.Snapshot(state));
                Validate(candidate);
                candidate.Revision = checked(state.Revision + 1);
                state = candidate;
                if (Changed != null)
                    foreach (Action<IslandSnapshot> observer in Changed.GetInvocationList())
                        try { observer(Snapshot); }
                        catch (Exception error) { try { ObserverFailed?.Invoke(error); } catch { } }
                return new IslandResult(true);
            }
            catch (ArgumentException error) { return new IslandResult(false, error.Message); }
            catch (OverflowException) { return new IslandResult(false, "数值超出范围。"); }
            finally { executing = false; }
        }
    }
}
