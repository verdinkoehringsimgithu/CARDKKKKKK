using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private SurvivalResult PrepareTarget(SurvivalState s, TaskState task, RecipeRule recipe, ItemState target, List<Amount> outputs)
        {
            CardRule rule = target == null ? null : Card(target.DefinitionId);
            if (recipe.Kind == RecipeKind.Expand)
            {
                if (s.ExpansionLevel >= def.Expansions.Count) return SurvivalResult.Fail("生产区已经扩至上限。");
                ExpansionRule expansion = def.Expansions[s.ExpansionLevel];
                if (!string.IsNullOrEmpty(expansion.Milestone) && !s.Milestones.Contains(expansion.Milestone)) return SurvivalResult.Fail("还未达到这次扩建的里程碑。");
                task.WorkMinutes = expansion.Minutes;
                var costs = expansion.Cost.Select(a => new Ingredient { Alternatives = new List<string> { a.DefinitionId }, Count = a.Count }).ToList();
                string error = ConsumeIngredients(s, task, costs, recipe, null, task.WorkMinutes, null);
                if (error != "") return SurvivalResult.Fail(error);
                outputs.Clear();
            }
            if (recipe.Kind == RecipeKind.Repair)
            {
                if (target == null) return SurvivalResult.Fail("请选择需要维修的对象。");
                if (rule.Kind == CardKind.Tool)
                {
                    if (!rule.Repairable) return SurvivalResult.Fail(rule.Name + "不能维修，请拆解后重新制作。");
                    if (rule.RepairLoss <= 0 || target.MaxDurability <= rule.RepairLoss) return SurvivalResult.Fail("最大耐久已经不足以再次打磨。");
                    if (target.Durability >= target.MaxDurability) return SurvivalResult.Fail("工具没有损耗。");
                    if (rule.RepairMinutes <= 0) return SurvivalResult.Fail("工具的维修工时尚未配置。");
                    task.WorkMinutes = rule.RepairMinutes;
                    if (!string.IsNullOrEmpty(rule.RepairToolId))
                    {
                        ItemState stone = s.Items.Where(i => i.DefinitionId == rule.RepairToolId && UsableCount(i, s.Minute) > 0 && !IsLocked(s, i.Id)).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (stone == null) return SurvivalResult.Fail("还缺不消耗的磨具：" + Card(rule.RepairToolId).Name + "。");
                        if (!task.LockedItems.Contains(stone.Id)) task.LockedItems.Add(stone.Id);
                    }
                }
                else
                {
                    if (rule.Structure <= 0) return SurvivalResult.Fail("这个对象不需要结构维修。");
                    if (target.Structure >= rule.Structure) return SurvivalResult.Fail("结构耐久已经完好。");
                    if (recipe.RepairAmount <= 0) return SurvivalResult.Fail("建筑维修量尚未配置。");
                }
                outputs.Clear();
            }
            if (recipe.Kind == RecipeKind.Salvage || recipe.Kind == RecipeKind.Demolish)
            {
                if (target == null) return SurvivalResult.Fail("请选择拆解目标。");
                if (recipe.Kind == RecipeKind.Salvage && rule.Kind != CardKind.Tool) return SurvivalResult.Fail("请使用建筑的拆除操作。");
                if (recipe.Kind == RecipeKind.Demolish && rule.Kind != CardKind.Facility && rule.Kind != CardKind.Area) return SurvivalResult.Fail("请选择建筑。");
                if (s.Items.Exists(i => i.OwnerId == target.Id)) return SurvivalResult.Fail("请先移出全部内容物。");
                outputs.Clear(); outputs.AddRange(SurvivalCopies.Copy(rule.Salvage));
            }
            if (recipe.Kind == RecipeKind.Destroy && recipe.Permanent)
            {
                if (target == null) return SurvivalResult.Fail("请选择要永久移除的对象。");
                if (s.Items.Exists(i => i.OwnerId == target.Id)) return SurvivalResult.Fail("请先移出内容物。");
            }
            if (recipe.Kind == RecipeKind.Gather)
            {
                if (target == null) return SurvivalResult.Fail("请选择采集节点。");
                NodeRule node = GetNodeRule(target.DefinitionId);
                if (node == null) return SurvivalResult.Fail("这个对象没有可采集储量。");
                outputs.Clear();
                if (node.Tide)
                {
                    if (s.TideTakenDay == s.TideDay || s.Tide.Count == 0) return SurvivalResult.Fail("今天的潮汐包已经收取。");
                    outputs.AddRange(SurvivalCopies.Copy(s.Tide));
                    s.Tide.Clear(); s.TideTakenDay = s.TideDay;
                }
                else
                {
                    IEnumerable<Amount> requested = recipe.Outputs.Count > 0 ? recipe.Outputs : target.Stock;
                    foreach (Amount amount in requested.ToArray())
                    {
                        Amount stock = target.Stock.Find(a => a.DefinitionId == amount.DefinitionId);
                        int take = stock == null ? 0 : Math.Min(amount.Count, stock.Count);
                        if (take < amount.Count && !recipe.AllowPartialInputs) return SurvivalResult.Fail("节点储量不足以完成本轮采集，请等待补充。");
                        if (take > 0) { outputs.Add(new Amount(amount.DefinitionId, take)); stock.Count -= take; }
                    }
                    if (outputs.Count == 0) return SurvivalResult.Fail("节点暂时没有可采集的资源。");
                }
            }
            return SurvivalResult.Ok();
        }

        private string FindWorkTool(SurvivalState s, TaskState task, RecipeRule recipe, out ItemState tool)
        {
            tool = null;
            if (string.IsNullOrEmpty(recipe.RequiredAbility)) return "";
            var capable = s.Items.Where(i => i.Quantity > 0 && !IsLocked(s, i.Id) && Card(i.DefinitionId).Ability == recipe.RequiredAbility).ToArray();
            if (capable.Length == 0) return "缺少能力：" + recipe.RequiredAbility + "。";
            int hardness = capable.Max(i => Card(i.DefinitionId).Hardness);
            if (hardness < recipe.RequiredHardness) return "刀具硬度不足：需要" + recipe.RequiredHardness + "，当前" + hardness + "。";
            tool = capable.Where(i => Card(i.DefinitionId).Hardness >= recipe.RequiredHardness && i.Durability >= recipe.ToolWear && i.Durability > 0)
                .OrderByDescending(i => (Card(i.DefinitionId).SpecialtyTags & recipe.Tags) != 0 ? Card(i.DefinitionId).SpecialtyEfficiency : 1)
                .ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (tool == null) return "符合硬度的刀具剩余耐久不足：本轮需要" + recipe.ToolWear + "。";
            if (!task.LockedItems.Contains(tool.Id)) task.LockedItems.Add(tool.Id);
            return "";
        }

        private double TaskEfficiency(SurvivalState s, TaskState task, RecipeRule recipe)
        {
            double specialty = 1;
            foreach (string id in task.LockedItems)
            {
                ItemState item = Item(s, id);
                if (item == null) continue;
                CardRule rule = Card(item.DefinitionId);
                if (!string.IsNullOrEmpty(recipe.RequiredAbility) && rule.Ability == recipe.RequiredAbility && (rule.SpecialtyTags & recipe.Tags) != 0)
                    specialty = Math.Max(specialty, rule.SpecialtyEfficiency);
            }
            return Clamp(WorkEfficiency(s, recipe) * specialty, def.Body.EfficiencyMin, def.Body.EfficiencyMax);
        }

        private string ConsumeIngredients(SurvivalState s, TaskState task, List<Ingredient> ingredients, RecipeRule recipe, string ownerId, double estimate, ItemState selected)
        {
            foreach (Ingredient ingredient in ingredients)
            {
                if (ingredient.Count <= 0 || ingredient.Alternatives.Count == 0) return "工艺材料配置不完整。";
                List<string> alternatives = ingredient.Alternatives;
                if (!string.IsNullOrEmpty(task.PreferredMaterialId) && alternatives.Contains(task.PreferredMaterialId))
                    alternatives = new List<string> { task.PreferredMaterialId };
                else if (selected != null && Card(selected.DefinitionId).Kind == CardKind.Material && alternatives.Contains(selected.DefinitionId))
                    alternatives = new List<string> { selected.DefinitionId };
                string chosen = ""; int available = 0; List<ItemState> sources = null;
                foreach (string id in alternatives)
                {
                    List<ItemState> candidates = InputCandidates(s, task, recipe, ownerId, id);
                    double freshUntil = !recipe.Human ? s.Minute + estimate : s.Minute;
                    int total = candidates.Sum(i => UsableCount(i, freshUntil, recipe.Human ? long.MaxValue : s.Minute));
                    int reserve = 0;
                    if (ingredient.Consumed && !recipe.Human && !string.IsNullOrEmpty(ownerId))
                    {
                        ItemState owner = Item(s, ownerId);
                        Amount retained = owner == null ? null : owner.Reserves.Find(a => a.DefinitionId == id);
                        reserve = retained == null ? 0 : retained.Count;
                    }
                    int usable = Math.Max(0, total - reserve);
                    if (usable >= ingredient.Count || (recipe.AllowPartialInputs && usable > 0))
                    { chosen = id; available = usable; sources = candidates; break; }
                }
                if (sources == null)
                {
                    string name = string.Join("或", alternatives.Select(id => Card(id) == null ? id : Card(id).Name));
                    return (ownerId != null ? "输入槽还缺" : "还缺") + ingredient.Count + "份" + name + (!recipe.Human ? "（保留量及周期结束前过期的批次不能使用）。" : "。");
                }
                int needed = recipe.AllowPartialInputs ? Math.Min(ingredient.Count, available) : ingredient.Count;
                if (!ingredient.Consumed)
                {
                    foreach (ItemState source in sources)
                    {
                        if (!task.LockedItems.Contains(source.Id)) task.LockedItems.Add(source.Id);
                        needed -= Math.Min(needed, source.Quantity);
                        if (needed == 0) break;
                    }
                    continue;
                }
                // 排除周期结束前过期的批次，合堆中的新产物也要等到下一分钟。
                foreach (ItemState source in sources)
                {
                    int count = Math.Min(needed, UsableCount(source, !recipe.Human ? s.Minute + estimate : s.Minute, recipe.Human ? long.MaxValue : s.Minute));
                    if (count <= 0) continue;
                    CardRule material = Card(chosen);
                    if (material.OrganicWeight > 0)
                    {
                        ItemState root = FindRootItem(s, source);
                        bool region = root != null && (Card(root.DefinitionId).Kind == CardKind.Area || Card(root.DefinitionId).Kind == CardKind.Facility);
                        string sourceId = region ? root.Id : "table";
                        string contactOwner = region ? root.Id : "";
                        WorkContact contact = task.Contacts.Find(c => c.SourceId == sourceId && c.OwnerId == contactOwner);
                        if (contact == null)
                        {
                            contact = new WorkContact { SourceId = sourceId, OwnerId = contactOwner };
                            task.Contacts.Add(contact);
                        }
                        contact.Load += count * material.OrganicWeight;
                    }
                    ConsumeFreshExact(source, count, !recipe.Human ? s.Minute + estimate : s.Minute, recipe.Human ? long.MaxValue : s.Minute);
                    if (source.Id == task.PreferredItemId && source.Quantity <= 0) task.PreferredItemId = "";
                    if (material.CountsFood) task.ConsumedNutrition += material.Nutrition * count;
                    Amount consumed = task.Consumed.Find(a => a.DefinitionId == chosen && a.TraitId == source.TraitId);
                    if (consumed == null) task.Consumed.Add(new Amount(chosen, count) { TraitId = source.TraitId }); else consumed.Count += count;
                    needed -= count;
                    if (needed == 0) break;
                }
            }
            return "";
        }

        private List<ItemState> InputCandidates(SurvivalState s, TaskState task, RecipeRule recipe, string ownerId, string definitionId)
        {
            bool hasTraits = recipe.Kind == RecipeKind.Plant && s.Items.Any(i => i.DefinitionId == definitionId && !string.IsNullOrEmpty(i.TraitId));
            bool selectedSeed = recipe.Kind == RecipeKind.Plant && def.Biology.Any(b => b.SeedId == definitionId);
            bool scoped = ownerId != null && !selectedSeed;
            return s.Items.Where(i => i.DefinitionId == definitionId && Card(i.DefinitionId).Kind != CardKind.Condition && i.Quantity > 0 && !IsLocked(s, i.Id) &&
                (!scoped || (i.OwnerId == ownerId && i.Place == Place.Input)) &&
                (recipe.Human ? i.Place != Place.Equipment : true) &&
                (!hasTraits || i.TraitId == task.TraitId))
                .OrderByDescending(i => i.Id == task.PreferredItemId)
                .ThenByDescending(i => i.Id == task.SecondaryId || i.Id == task.TargetId)
                .ThenBy(i => i.Lots.Count == 0 ? long.MaxValue : i.Lots.Min(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt))
                .ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        }

        private static void ConsumeFreshExact(ItemState item, int count, double afterMinute, long availableAt)
        {
            if (item.Lots.Count == 0) { ConsumeExact(item, count, availableAt); return; }
            int left = count;
            foreach (Lot lot in item.Lots.Where(l => l.AvailableAt <= availableAt && (l.ExpiresAt == 0 || l.ExpiresAt > afterMinute)).OrderBy(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt).ToArray())
            {
                int take = Math.Min(left, lot.Count); lot.Count -= take; left -= take;
                if (left == 0) break;
            }
            if (left != 0) throw new InvalidOperationException("可用食材批次已经变化。");
            item.Lots.RemoveAll(l => l.Count <= 0); item.Quantity -= count;
        }

        private string ConsumeFuel(SurvivalState s, TaskState task, RecipeRule recipe, ItemState target, ItemState secondary, List<Amount> outputs)
        {
            if (recipe.Fuel <= 0) return "";
            ItemState fire = new[] { target, secondary }.FirstOrDefault(i => i != null && Card(i.DefinitionId).Role == CardRole.Fire);
            if (fire == null) return "缺少明确绑定的篝火。";
            if (fire.Fuel + 0.0000001 < recipe.Fuel) return "篝火燃料不足：本轮需要" + recipe.Fuel + "。";
            CardRule rule = Card(fire.DefinitionId);
            fire.Fuel -= recipe.Fuel;
            if (rule.AshEveryFuel > 0 && !string.IsNullOrEmpty(rule.AshId))
            {
                fire.AshRemainder += recipe.Fuel;
                int ash = (int)Math.Floor((fire.AshRemainder + 0.0000001) / rule.AshEveryFuel);
                fire.AshRemainder -= ash * rule.AshEveryFuel;
                if (ash > 0)
                {
                    Amount amount = outputs.Find(a => a.DefinitionId == rule.AshId);
                    if (amount == null) outputs.Add(new Amount(rule.AshId, ash)); else amount.Count += ash;
                }
            }
            return "";
        }
    }
}
