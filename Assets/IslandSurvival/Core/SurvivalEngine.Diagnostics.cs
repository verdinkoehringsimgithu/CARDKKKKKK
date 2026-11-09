using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private Dictionary<string, double> CaptureResources(SurvivalState s)
        {
            var snapshot = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (CardRule card in def.Cards.Where(c => c.Kind == CardKind.Material)) snapshot[card.Id] = 0;
            foreach (ItemState item in s.Items)
                if (Card(item.DefinitionId).Kind == CardKind.Material) snapshot[item.DefinitionId] += item.Quantity;
            snapshot["body:food"] = s.Body.Food;
            snapshot["body:mind"] = s.Body.Mind;
            snapshot["body:vital"] = s.Body.Vitality;
            snapshot["body:health"] = Math.Pow(s.Body.Integrity, def.Body.HealthWeight) * Math.Pow(s.Body.Function, 1 - def.Body.HealthWeight);
            return snapshot;
        }

        private void RecordResourceChanges(SurvivalState s, Dictionary<string, double> before)
        {
            Dictionary<string, double> after = CaptureResources(s);
            foreach (KeyValuePair<string, double> entry in after.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                double previous;
                if (before == null || !before.TryGetValue(entry.Key, out previous)) previous = 0;
                double delta = entry.Value - previous;
                if (Math.Abs(delta) < 0.0000000001) continue;
                ResourceChange existing = s.History.FindLast(h => h.Minute == s.Minute && h.Key == entry.Key);
                if (existing == null) s.History.Add(new ResourceChange { Minute = s.Minute, Key = entry.Key, Delta = delta });
                else existing.Delta += delta;
            }
            s.History.RemoveAll(h => h.Minute < s.Minute - 1440 || Math.Abs(h.Delta) < 0.0000000001);
        }

        // 仅沿材料加工链反查可食物，种植/繁殖的肥料和饲料不被误当作食物来源。
        private HashSet<string> FoodMaterials()
        {
            var foods = new HashSet<string>(def.Cards.Where(c => c.Kind == CardKind.Material && c.Nutrition > 0).Select(c => c.Id), StringComparer.Ordinal);
            bool changed;
            do
            {
                changed = false;
                foreach (RecipeRule recipe in def.Recipes)
                {
                    if (!recipe.Outputs.Any(a => foods.Contains(a.DefinitionId))) continue;
                    foreach (Ingredient ingredient in recipe.Inputs.Where(i => i.Consumed))
                        foreach (string id in ingredient.Alternatives)
                            if (Card(id).Kind == CardKind.Material && foods.Add(id)) changed = true;
                }
            } while (changed);
            return foods;
        }

        private HashSet<CardRole> FoodSpecies(HashSet<string> foods)
        {
            var species = new HashSet<CardRole>();
            foreach (BiologyRule biology in def.Biology)
                if (biology.Production.Any(a => foods.Contains(a.DefinitionId))) species.Add(biology.Role);
            foreach (RecipeRule recipe in def.Recipes.Where(r => r.Kind == RecipeKind.Harvest && r.Outputs.Any(a => foods.Contains(a.DefinitionId))))
                foreach (CardRule card in def.Cards.Where(c => c.Kind == CardKind.Creature &&
                    (recipe.TargetDefinitions.Count > 0 && recipe.TargetDefinitions.Contains(c.Id) || recipe.RequiredRole != CardRole.None && recipe.RequiredRole == c.Role)))
                    species.Add(card.Role);
            bool changed;
            do
            {
                changed = false;
                foreach (BiologyRule biology in def.Biology)
                    if (!string.IsNullOrEmpty(biology.MatureId) && species.Contains(Card(biology.MatureId).Role) && species.Add(biology.Role)) changed = true;
            } while (changed);
            return species;
        }

        private void CheckFoodSources(SurvivalState s)
        {
            if (s.Dead || s.Completed && !s.ContinueAfterWin) return;
            HashSet<string> foods = FoodMaterials();
            HashSet<CardRole> species = FoodSpecies(foods);
            bool running = false;
            foreach (ItemState item in s.Items)
            {
                NodeRule node = GetNodeRule(item.DefinitionId);
                if (node == null || node.RefreshMinutes <= 0) continue;
                // 潮汐中的种苗/动物需要实际进入人工生产，不能仅凭未来可领取就称已在供食。
                IEnumerable<Amount> renewal = node.Tide ? def.TideFixed.Concat(def.TidePeriodic.SelectMany(p => p.Items)) : node.Daily;
                if (renewal.Any(a => a.Count > 0 && foods.Contains(a.DefinitionId))) { running = true; break; }
            }
            if (!running)
                running = s.Tasks.Any(t => s.Reservations.Any(r => r.TaskId == t.Id &&
                    (foods.Contains(r.DefinitionId) || Card(r.DefinitionId).Kind == CardKind.Creature && species.Contains(Card(r.DefinitionId).Role) &&
                    !string.IsNullOrEmpty(r.OwnerId) && (r.Place == Place.Plant || r.Place == Place.Young || r.Place == Place.Adult))));
            if (!running)
                foreach (ItemState item in s.Items.Where(i => i.Quantity > 0 && Card(i.DefinitionId).Kind == CardKind.Creature && species.Contains(Card(i.DefinitionId).Role)))
                    if (BiologicalFoodSource(s, item, foods)) { running = true; break; }
            if (running) { s.FoodSourceAlert = false; return; }
            if (s.FoodSourceAlert) return;
            s.FoodSourceAlert = true; s.Paused = true;
            AddLog(s, "所有食物来源均已停产。请检查生长个体、喂养、生产设施和产出位置；已有食物库存仍可食用。");
        }

        private bool BiologicalFoodSource(SurvivalState s, ItemState item, HashSet<string> foods)
        {
            BiologyRule biology = BiologyFor(item);
            ItemState owner = Item(s, item.OwnerId);
            if (biology == null || owner == null) return false;
            CardRole role = Card(item.DefinitionId).Role;
            CardRole ownerRole = Card(owner.DefinitionId).Role;
            if (ownerRole == CardRole.Garden && item.Place == Place.Plant)
            {
                // 已投入生长及产叶不因花圃损坏中断；成熟保存期限仍按真实时刻判断。
                if (!item.Mature) return biology.GrowMinutes > 0;
                if (biology.MatureHoldMinutes > 0) return s.Minute < item.MatureAt + biology.MatureHoldMinutes;
                if (item.Stock.Any(a => a.Count > 0 && foods.Contains(a.DefinitionId))) return true;
                if (item.Production > 0) return true;
                return owner.Structure > 0 && s.Minute >= item.CooldownUntil && !IsLocked(s, item.Id) && BiologicalInputAvailable(s, biology, owner);
            }
            if (ownerRole != CardRole.Pen || item.Health <= 0) return false;
            if (role == CardRole.YoungSnail)
                return item.Place == Place.Young && biology.GrowMinutes > 0 && (item.Fed || BiologicalInputAvailable(s, biology, owner));
            if (role != CardRole.AdultSnail || item.Place != Place.Adult || owner.Structure <= 0 || IsLocked(s, item.Id)) return false;
            if (!item.Fed && !BiologicalInputAvailable(s, biology, owner)) return false;
            RecipeRule production = def.Recipes.Find(r => !r.Human && r.AutomaticMinutes <= 0 && r.RequiredRole == role && TargetMatches(r, item));
            if (production == null) return false;
            SurvivalState candidate = SurvivalCopies.Copy(s);
            var task = new TaskState { Id = NewId(candidate, "food_probe_"), RecipeId = production.Id, TargetId = item.Id };
            List<Amount> outputs = TraitOutputs(biology.Production, item.TraitId, role, true);
            return TryReserveOutputs(candidate, task, outputs, owner.Id, Place.Output) == "";
        }

        private bool BiologicalInputAvailable(SurvivalState s, BiologyRule biology, ItemState owner)
        {
            foreach (var group in biology.Feed.GroupBy(a => a.DefinitionId))
            {
                int available = s.Items.Where(i => i.OwnerId == owner.Id && i.Place == Place.Input && i.DefinitionId == group.Key && !IsLocked(s, i.Id))
                    .Sum(i => UsableCount(i, s.Minute, s.Minute));
                Amount reserve = owner.Reserves.Find(a => a.DefinitionId == group.Key);
                if (available - (reserve == null ? 0 : reserve.Count) < group.Sum(a => a.Count)) return false;
            }
            return true;
        }

        private string DeathDescription(SurvivalState s)
        {
            string source = s.Body.LastVitalDamageSource;
            if (!string.IsNullOrEmpty(source))
            {
                RecipeRule recipe = Recipe(source);
                AfflictionRule affliction = def.Afflictions.Find(a => a.Id == source);
                ItemState item = Item(s, source);
                string label = recipe != null ? recipe.Name : affliction != null ? affliction.Name : item != null ? Card(item.DefinitionId).Name : source;
                return "生命耗尽。最近的生命损伤来自：" + label + "。";
            }
            string[] burdens = s.Body.Afflictions.Select(a => new { State = a, Rule = def.Afflictions.Find(r => r.Id == a.RuleId) })
                .Where(a => a.Rule != null && a.Rule.VitalWeight > 0 && a.State.Severity > a.Rule.VitalThreshold)
                .OrderByDescending(a => a.Rule.VitalWeight * Math.Pow(Clamp((a.State.Severity - a.Rule.VitalThreshold) / (1 - a.Rule.VitalThreshold)), a.Rule.CurvePower))
                .Take(3).Select(a => a.Rule.Name + "（" + (a.State.Severity * 100).ToString("0") + "%）").ToArray();
            return burdens.Length > 0 ? "生命耗尽。主要生命负担：" + string.Join("、", burdens) + "。" : "生命耗尽。请查看身体状态和最近24小时的资源变化。";
        }

        private string EmergencyCareWarning(SurvivalState s, RecipeRule recipe)
        {
            if (!recipe.ForcedGather) return "";
            List<RecipeRule> dressings = def.Recipes.Where(r => r.Care == CareKind.Dressing && RecipeKnown(s, r)).ToList();
            if (dressings.Any(r => CareMaterialsAvailable(s, r))) return "";
            string[] names = dressings.SelectMany(r => r.Inputs.Where(i => i.Consumed).SelectMany(i => i.Alternatives))
                .Distinct().Select(id => Card(id).Name).ToArray();
            string required = names.Length == 0 ? "应急敷料" : string.Join("或", names);
            string[] paths = def.Recipes.Where(r => RecipeKnown(s, r) && (r.Care == CareKind.Clean || r.Care == CareKind.Gel || r.Care == CareKind.Leaf))
                .Select(r => r.Name + (CareMaterialsAvailable(s, r) ? "（材料条件已具备）" : "（需补料）")).ToArray();
            return "当前没有可用的" + required + "。" + (paths.Length == 0 ? "" : "已有清洁与护理路径：" + string.Join("、", paths) + "；清洁和草药护理不提供敷料止血效果。") + "仍可选择开始本次强行采集。";
        }

        private bool CareMaterialsAvailable(SurvivalState s, RecipeRule recipe)
        {
            foreach (Ingredient ingredient in recipe.Inputs)
                if (!ingredient.Alternatives.Any(id => s.Items.Where(i => i.DefinitionId == id && !IsLocked(s, i.Id)).Sum(i => UsableCount(i, s.Minute)) >= ingredient.Count)) return false;
            return true;
        }
    }
}
