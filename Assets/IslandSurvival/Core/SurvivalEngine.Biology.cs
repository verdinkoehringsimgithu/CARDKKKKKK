using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private BiologyRule BiologyFor(ItemState item)
        {
            if (item == null) return null;
            CardRule card = Card(item.DefinitionId);
            BiologyRule rule = def.Biology.Find(r => r.Role == card.Role);
            if (rule != null) return rule;
            // Wild plants share the configured division species, while their leaves remain node stock.
            NodeRule node = GetNodeRule(item.DefinitionId);
            if (node == null) return null;
            return def.Biology.Find(r => r.Role == CardRole.AloePlant && !string.IsNullOrEmpty(r.ProductionItem)
                && node.Daily.Exists(a => a.DefinitionId == r.ProductionItem));
        }

        private void InitializeBiology(SurvivalState s, ItemState item)
        {
            BiologyRule rule = BiologyFor(item);
            if (rule == null) return;
            item.BornAt = s.Minute;
            item.FeedWindowAt = s.Minute;
            CardRole role = Card(item.DefinitionId).Role;
            item.Mature = role == CardRole.AdultSnail || role == CardRole.NaturalNode;
            item.Health = 100;
        }

        private double OrganicLoad(SurvivalState s, ItemState region)
        {
            if (region == null) return 0;
            ItemState root = FindRootItem(s, region);
            if (root == null) return 0;
            return s.Items.Where(i => i.Id != root.Id && FindRootItem(s, i) == root)
                .Sum(i => Math.Max(0, Card(i.DefinitionId).OrganicWeight) * i.Quantity);
        }

        private double RegionFactor(SurvivalState s, ItemState item)
        {
            ItemState root = FindRootItem(s, item);
            if (root == null) return 1;
            CardRule rule = Card(root.DefinitionId);
            if (rule.Kind != CardKind.Area || rule.CarryingCapacity <= 0 || rule.LoadPower <= 0) return 1;
            return 1 / (1 + Math.Pow(OrganicLoad(s, root) / rule.CarryingCapacity, rule.LoadPower));
        }

        private double EnvironmentEfficiency(SurvivalState s, string ownerId) => RegionFactor(s, Item(s, ownerId));

        public SurvivalResult Feed(string itemId) => Command(s =>
        {
            if (s.Dead) return SurvivalResult.Fail("本局已经结束。");
            ItemState animal = Item(s, itemId);
            if (animal == null || !IsAnimal(animal)) return SurvivalResult.Fail("请选择需要喂养的个体。");
            if (animal.Fed) return SurvivalResult.Fail("这个喂养窗口已经吃过，不会重复扣料。");
            if (!TryFeed(s, animal, "", true)) return SurvivalResult.Fail("桌面上的饲料不足，请查看该个体的饲料需求。");
            RemoveEmpty(s);
            return SurvivalResult.Ok("已喂养。");
        });

        private bool IsAnimal(ItemState item)
        {
            CardRole role = Card(item.DefinitionId).Role;
            return role == CardRole.AdultSnail || role == CardRole.YoungSnail;
        }

        private bool TryFeed(SurvivalState s, ItemState animal, string owner, bool manual = false)
        {
            BiologyRule rule = BiologyFor(animal);
            if (rule == null || animal.Fed || rule.Feed.Count == 0) return false;
            if (!TakeBiologicalInputs(s, rule.Feed, owner, manual)) return false;
            animal.Fed = true;
            return true;
        }

        private bool TakeBiologicalInputs(SurvivalState s, List<Amount> inputs, string owner, bool manual = false)
        {
            inputs = inputs.GroupBy(a => a.DefinitionId).Select(g => new Amount(g.Key, g.Sum(a => a.Count))).ToList();
            Place place = string.IsNullOrEmpty(owner) ? Place.Table : Place.Input;
            foreach (Amount amount in inputs)
            {
                int available = s.Items.Where(i => i.OwnerId == owner && i.Place == place && i.DefinitionId == amount.DefinitionId && !IsLocked(s, i.Id))
                    .Sum(i => i.Lots.Where(l => (manual || l.AvailableAt <= s.Minute) && (l.ExpiresAt == 0 || l.ExpiresAt > s.Minute)).Sum(l => l.Count));
                ItemState region = string.IsNullOrEmpty(owner) ? null : Item(s, owner);
                Amount reserve = region == null ? null : region.Reserves.Find(a => a.DefinitionId == amount.DefinitionId);
                if (available - (reserve == null ? 0 : reserve.Count) < amount.Count) return false;
            }
            foreach (Amount amount in inputs)
            {
                int remaining = amount.Count;
                foreach (ItemState item in s.Items.Where(i => i.OwnerId == owner && i.Place == place && i.DefinitionId == amount.DefinitionId && !IsLocked(s, i.Id)).OrderBy(i => i.Id, StringComparer.Ordinal))
                {
                    foreach (Lot lot in item.Lots.Where(l => (manual || l.AvailableAt <= s.Minute) && (l.ExpiresAt == 0 || l.ExpiresAt > s.Minute)).OrderBy(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt))
                    {
                        int n = Math.Min(remaining, lot.Count);
                        lot.Count -= n; item.Quantity -= n; remaining -= n;
                        if (remaining == 0) break;
                    }
                    item.Lots.RemoveAll(l => l.Count == 0);
                    if (remaining == 0) break;
                }
            }
            return true;
        }

        public SurvivalResult Plant(string regionId, string seedItemId, string traitId = "") => Command(s =>
        {
            ItemState garden = Item(s, regionId), seed = Item(s, seedItemId);
            if (garden == null || Card(garden.DefinitionId).Role != CardRole.Garden || seed == null || seed.Quantity <= 0)
                return SurvivalResult.Fail("请选择花圃和种苗。");
            string inherited = seed.TraitId ?? "";
            if (!string.IsNullOrEmpty(traitId) && traitId != inherited) return SurvivalResult.Fail("播种只能使用该种苗已有的性状。");
            RecipeRule recipe = def.Recipes.Find(r => r.Kind == RecipeKind.Plant && TargetMatches(r, garden)
                && r.Inputs.Exists(i => i.Alternatives.Contains(seed.DefinitionId)));
            if (recipe == null) return SurvivalResult.Fail("该种苗没有适用的栽种工艺。");
            return BeginRecipe(s, recipe.Id, garden.Id, seed.Id, "", 1, inherited);
        });

        public SurvivalResult Harvest(string plantId) => BeginBiologicalCommand(RecipeKind.Harvest, plantId);
        public SurvivalResult Divide(string plantId) => BeginBiologicalCommand(RecipeKind.Divide, plantId);

        private SurvivalResult BeginBiologicalCommand(RecipeKind kind, string itemId)
        {
            return Command(s =>
            {
                ItemState item = Item(s, itemId);
                if (item == null) return SurvivalResult.Fail("目标不存在。");
                RecipeRule recipe = def.Recipes.Find(r => r.Kind == kind && TargetMatches(r, item));
                if (recipe == null) return SurvivalResult.Fail("该对象没有适用工艺。");
                return BeginRecipe(s, recipe.Id, item.Id);
            });
        }

        public SurvivalResult Breed(string firstId, string secondId, string traitId = "") => Command(s =>
        {
            ItemState first = Item(s, firstId);
            if (first == null) return SurvivalResult.Fail("请选择第一亲本。");
            RecipeRule recipe = def.Recipes.Find(r => r.Kind == RecipeKind.Breed && TargetMatches(r, first));
            if (recipe == null) return SurvivalResult.Fail("该个体没有繁殖工艺。");
            return BeginRecipe(s, recipe.Id, firstId, secondId, "", 1, traitId);
        });

        public SurvivalResult SelectSeed(string recipeId, string traitId) => Command(s =>
        {
            RecipeRule recipe = Recipe(recipeId);
            if (recipe == null || recipe.Kind != RecipeKind.SelectSeed) return SurvivalResult.Fail("请选择选育工艺。");
            return BeginRecipe(s, recipe.Id, "", "", "", 1, traitId);
        });

        private List<Amount> TraitOutputs(IEnumerable<Amount> source, string traitId, CardRole species, bool addYield)
        {
            List<Amount> result = SurvivalCopies.Copy(source.ToList());
            TraitRule trait = def.Traits.Find(t => t.Id == traitId && t.Species == species);
            if (trait == null) return result;
            if (addYield)
                foreach (Amount extra in trait.AddedOutputs)
                {
                    Amount existing = result.Find(a => a.DefinitionId == extra.DefinitionId && string.IsNullOrEmpty(a.TraitId));
                    if (existing == null) result.Add(SurvivalCopies.Copy(extra)); else existing.Count += extra.Count;
                }
            BiologyRule bio = def.Biology.Find(b => b.Role == species);
            foreach (Amount amount in result)
            {
                CardRule card = Card(amount.DefinitionId);
                if (card.Role == species || (species == CardRole.AdultSnail && card.Role == CardRole.YoungSnail)
                    || (bio != null && amount.DefinitionId == bio.SeedId)) amount.TraitId = trait.Id;
            }
            return result;
        }

        private SurvivalResult ReserveBiological(SurvivalState s, TaskState task, List<Amount> outputs, string owner, Place place)
        {
            string error = TryReserveOutputs(s, task, outputs, owner, place);
            return string.IsNullOrEmpty(error) ? SurvivalResult.Ok() : SurvivalResult.Fail(error);
        }

        private SurvivalResult BeginSpecial(SurvivalState s, TaskState task, RecipeRule recipe)
        {
            SyncConditionCards(s);
            ItemState target = Item(s, task.TargetId);
            BiologyRule biology = BiologyFor(target);
            if (recipe.Kind == RecipeKind.Care || recipe.Kind == RecipeKind.Clean)
            {
                if (target == null && !string.IsNullOrEmpty(task.WoundId))
                {
                    target = s.Items.Find(i => Card(i.DefinitionId).Kind == CardKind.Condition && Card(i.DefinitionId).WoundCard && i.ConditionId == task.WoundId);
                    if (target != null) task.TargetId = target.Id;
                }
                if (target == null || Card(target.DefinitionId).Kind != CardKind.Condition) return SurvivalResult.Fail("请选择需要护理的伤口或关联状态牌。");
                if (!TargetMatches(recipe, target)) return SurvivalResult.Fail("这项护理不适用于所选状态牌。");
                InjuryState linked = ConditionWound(s, target);
                if (linked == null) return SurvivalResult.Fail("这张状态牌没有可护理伤口，请使用该状态已有的食用或休息恢复路径。");
                if (!string.IsNullOrEmpty(task.WoundId) && task.WoundId != linked.Id)
                    return SurvivalResult.Fail("护理目标与所选状态牌不一致。");
                task.WoundId = linked.Id;
                if (!task.LockedItems.Contains(target.Id)) task.LockedItems.Add(target.Id);
                InjuryState wound = s.Body.Injuries.Find(w => w.Id == task.WoundId && w.Tags != WorkTag.None);
                if (wound == null || wound.Deficit <= 0 && !s.Body.Afflictions.Exists(a => a.InjuryId == wound.Id && a.Severity > 0))
                    return SurvivalResult.Fail("当前没有适用伤口，请选中一个开放伤口。");
                return SurvivalResult.Ok();
            }
            if (recipe.Kind == RecipeKind.Plant)
            {
                if (target == null || Card(target.DefinitionId).Role != CardRole.Garden) return SurvivalResult.Fail("请选择花圃。");
                Amount plantOutput = recipe.Outputs.Find(a => Card(a.DefinitionId).Role == CardRole.YamPlant || Card(a.DefinitionId).Role == CardRole.AloePlant);
                if (plantOutput == null) return SurvivalResult.Fail("栽种工艺没有配置植物产物。");
                CardRole species = Card(plantOutput.DefinitionId).Role;
                if (!ValidTrait(task.TraitId, species)) return SurvivalResult.Fail("该性状不适用于所选植物。");
                BiologyRule plantRule = def.Biology.Find(b => b.Role == species);
                if (plantRule == null || !task.Consumed.Exists(a => a.DefinitionId == plantRule.SeedId && (a.TraitId ?? "") == (task.TraitId ?? "")))
                    return SurvivalResult.Fail("投入种苗的性状与选中的性状不一致。");
                return ReserveBiological(s, task, TraitOutputs(recipe.Outputs, task.TraitId, species, false), target.Id, Place.Plant);
            }
            if (recipe.Kind == RecipeKind.Harvest)
            {
                if (target == null || biology == null || !target.Mature) return SurvivalResult.Fail("植物尚未成熟。");
                if (biology.MatureHoldMinutes > 0)
                {
                    double remain = target.MatureAt + biology.MatureHoldMinutes - s.Minute;
                    if (remain <= task.WorkMinutes / Math.Max(double.Epsilon, TaskEfficiency(s, task, recipe)))
                        return SurvivalResult.Fail("距离腐烂不足以完成收获，请先处理残余。");
                }
                List<Amount> outputs;
                if (Card(target.DefinitionId).Role == CardRole.AloePlant)
                {
                    if (target.Stock.Sum(a => a.Count) <= 0) return SurvivalResult.Fail("这株植物没有可采集的储叶。");
                    outputs = SurvivalCopies.Copy(target.Stock);
                }
                else outputs = TraitOutputs(recipe.Outputs, target.TraitId, Card(target.DefinitionId).Role, true);
                return ReserveBiological(s, task, outputs, target.OwnerId, string.IsNullOrEmpty(target.OwnerId) ? Place.Table : Place.Output);
            }
            if (recipe.Kind == RecipeKind.Divide)
            {
                if (target == null || biology == null || !target.Mature) return SurvivalResult.Fail("请选择成熟植株。");
                if (s.Minute < target.CooldownUntil) return SurvivalResult.Fail("母株仍在分株恢复期。");
                if (target.Stock.Any(a => a.Count > 0)) return SurvivalResult.Fail("先采完母株储叶，再进行分株。");
                return ReserveBiological(s, task, SurvivalCopies.Copy(recipe.Outputs), target.OwnerId,
                    string.IsNullOrEmpty(target.OwnerId) ? Place.Table : Place.Output);
            }
            if (recipe.Kind == RecipeKind.Breed)
            {
                ItemState second = Item(s, task.SecondaryId);
                if (target == null || second == null || second == target || Card(target.DefinitionId).Role != CardRole.AdultSnail
                    || Card(second.DefinitionId).Role != CardRole.AdultSnail) return SurvivalResult.Fail("请选择两个不同的成年亲本。");
                ItemState pen = Item(s, target.OwnerId);
                if (pen == null || Card(pen.DefinitionId).Role != CardRole.Pen || second.OwnerId != pen.Id
                    || target.Place != Place.Adult || second.Place != Place.Adult) return SurvivalResult.Fail("两个亲本必须在同一围栏的成年槽。");
                double minimumHealth = biology == null ? 100 : biology.BreedMinimumHealth;
                if (target.Health < minimumHealth || second.Health < minimumHealth) return SurvivalResult.Fail("两个亲本健康都须达到" + minimumHealth.ToString("0.##") + "。");
                if (pen.Structure <= 0) return SurvivalResult.Fail("围栏已损坏，请先维修。");
                if (!ValidTrait(task.TraitId, CardRole.AdultSnail)) return SurvivalResult.Fail("该性状不适用于亲本。");
                if (!string.IsNullOrEmpty(task.TraitId) && !s.Milestones.Contains("first_breed")) return SurvivalResult.Fail("先完成一次普通繁殖，再指定幼体性状。");
                string offspringTrait = string.IsNullOrEmpty(task.TraitId) ? target.TraitId ?? "" : task.TraitId;
                return ReserveBiological(s, task, TraitOutputs(recipe.Outputs, offspringTrait, CardRole.AdultSnail, false), pen.Id, Place.Young);
            }
            if (recipe.Kind == RecipeKind.SelectSeed)
            {
                if (!s.Milestones.Contains("first_harvest")) return SurvivalResult.Fail("先完成一次人工收获，再进行种苗选育。");
                if (string.IsNullOrEmpty(task.TraitId) || !ValidTrait(task.TraitId, CardRole.YamPlant)) return SurvivalResult.Fail("请选择一种适用的种苗性状。");
                return ReserveBiological(s, task, TraitOutputs(recipe.Outputs, task.TraitId, CardRole.YamPlant, false), "", Place.Table);
            }
            if (!recipe.Human && target != null && Card(target.DefinitionId).Role == CardRole.AdultSnail)
            {
                if (target.Place != Place.Adult || !target.Fed || target.Health <= 0 || biology == null)
                    return SurvivalResult.Fail("生产需要围栏成年槽内已完成本窗口喂养的个体。");
                if (target.LastProductionWindow == target.FeedWindowAt) return SurvivalResult.Fail("该个体在本喂养窗口已经启动过生产。");
                ItemState pen = Item(s, target.OwnerId);
                if (pen == null || pen.Structure <= 0) return SurvivalResult.Fail("围栏已损坏，请先维修。");
                task.WorkMinutes = biology.ProductionMinutes;
                SurvivalResult result = ReserveBiological(s, task, TraitOutputs(biology.Production, target.TraitId, CardRole.AdultSnail, true), pen.Id, Place.Output);
                if (result.Success) target.LastProductionWindow = target.FeedWindowAt;
                return result;
            }
            return SurvivalResult.Ok();
        }

        private bool ValidTrait(string id, CardRole species) => string.IsNullOrEmpty(id) || def.Traits.Exists(t => t.Id == id && t.Species == species);

        private void FinishSpecial(SurvivalState s, TaskState task, RecipeRule recipe)
        {
            ItemState target = Item(s, task.TargetId);
            if (recipe.Kind == RecipeKind.Care || recipe.Kind == RecipeKind.Clean)
            {
                InjuryState wound = s.Body.Injuries.Find(w => w.Id == task.WoundId);
                if (wound == null) return;
                double strength = Math.Max(0, recipe.CareStrength);
                double duration = recipe.CareDuration;
                CardRule dressing = task.Consumed.Select(a => Card(a.DefinitionId)).FirstOrDefault(c => c.CareStrength > 0 && c.CareDuration > 0);
                if (recipe.Care == CareKind.Dressing && dressing != null)
                {
                    if (strength <= 0) strength = dressing.CareStrength;
                    if (duration <= 0) duration = dressing.CareDuration;
                }
                if (strength <= 0 && recipe.Care == CareKind.Gel) strength = def.Body.GelStrength;
                if (strength <= 0 && recipe.Care == CareKind.Leaf) strength = def.Body.LeafStrength;
                long until = s.Minute + (long)Math.Ceiling(duration);
                switch (recipe.Care)
                {
                    case CareKind.Dressing:
                        wound.DressingStrength = strength; wound.DressingUntil = until;
                        wound.DressingId = dressing == null ? recipe.Id : dressing.Id;
                        break;
                    case CareKind.Gel:
                    case CareKind.Leaf:
                        wound.CareStrength = strength; wound.CareUntil = until;
                        break;
                    case CareKind.Clean:
                        wound.Cleanliness = Clamp(wound.Cleanliness + (strength > 0 ? strength : def.Body.CleanAmount));
                        break;
                }
                Mark(s, "care_completed");
            }
            if (recipe.Kind == RecipeKind.Harvest && target != null)
            {
                Mark(s, "first_harvest");
                if (Card(target.DefinitionId).Role == CardRole.AloePlant) target.Stock.Clear();
                else s.Items.Remove(target);
            }
            if (recipe.Kind == RecipeKind.Divide && target != null)
            {
                BiologyRule rule = BiologyFor(target);
                target.CooldownUntil = s.Minute + (long)Math.Ceiling(rule.DivisionCooldownMinutes);
                target.Production = 0;
                target.Status = "分株恢复中";
            }
            if (recipe.Kind == RecipeKind.Breed) Mark(s, "first_breed");
        }

        private void CancelBiologicalTasks(SurvivalState s, ItemState item)
        {
            foreach (TaskState task in s.Tasks.Where(t => t.TargetId == item.Id || t.SecondaryId == item.Id || t.LockedItems.Contains(item.Id)).ToArray())
            {
                s.Reservations.RemoveAll(r => r.TaskId == task.Id);
                s.Tasks.Remove(task);
            }
        }

        private void BecomeResidue(SurvivalState s, ItemState item, BiologyRule rule, string message)
        {
            CancelBiologicalTasks(s, item);
            s.Items.Remove(item);
            ItemState firstResidue = null;
            foreach (Amount death in rule.Death)
            {
                int remaining = death.Count;
                while (remaining > 0)
                {
                    int count = Math.Min(remaining, Card(death.DefinitionId).StackLimit);
                    ItemState residue = CreateItem(s, death.DefinitionId, count, firstResidue == null ? item.OwnerId : firstResidue.Id,
                        Place.Residue, item.Slot, item.X, item.Y);
                    if (residue == null) throw new InvalidOperationException("生物残余无法原位保留，请检查槽位配置。");
                    residue.ResidueFrom = firstResidue == null ? item.Place : Place.Residue;
                    if (firstResidue == null) firstResidue = residue;
                    remaining -= count;
                }
            }
            AddLog(s, message);
        }

        private void TickBiologyExpire(SurvivalState s)
        {
            foreach (string id in s.Items.OrderBy(i => i.Id, StringComparer.Ordinal).Select(i => i.Id).ToArray())
            {
                ItemState item = Item(s, id);
                if (item == null) continue;
                BiologyRule rule = BiologyFor(item);
                if (rule == null) continue;
                if (item.Mature && rule.MatureHoldMinutes > 0 && s.Minute >= item.MatureAt + rule.MatureHoldMinutes)
                {
                    BecomeResidue(s, item, rule, Card(item.DefinitionId).Name + "已腐烂，未完成的收获已停止。");
                    continue;
                }
                if (!IsAnimal(item)) continue;
                long window = Math.Max(1, (long)Math.Ceiling(rule.FeedWindowMinutes));
                while (s.Minute >= item.FeedWindowAt + window)
                {
                    item.Health = Clamp(item.Health + (item.Fed ? rule.HealthGain : -rule.HealthLoss), 0, 100);
                    item.FeedWindowAt += window;
                    item.Fed = false;
                    if (item.Health <= 0) break;
                }
                if (item.Health <= 0)
                {
                    BecomeResidue(s, item, rule, Card(item.DefinitionId).Name + "死亡，相关生产或繁殖已停止。");
                    continue;
                }
                ItemState pen = Item(s, item.OwnerId);
                if (!item.Fed && pen != null && Card(pen.DefinitionId).Role == CardRole.Pen) TryFeed(s, item, pen.Id);
            }
        }

        private void TickBiology(SurvivalState s)
        {
            foreach (string id in s.Items.OrderBy(i => i.Id, StringComparer.Ordinal).Select(i => i.Id).ToArray())
            {
                ItemState item = Item(s, id);
                if (item == null || item.BornAt >= s.Minute) continue;
                BiologyRule rule = BiologyFor(item);
                if (rule == null || Card(item.DefinitionId).Role == CardRole.NaturalNode) continue;
                CardRole role = Card(item.DefinitionId).Role;
                ItemState owner = Item(s, item.OwnerId);
                if (role == CardRole.YoungSnail)
                {
                    if (owner == null || Card(owner.DefinitionId).Role != CardRole.Pen) { item.Status = "放入围栏后继续成长"; continue; }
                    item.Growth += 1;
                    if (item.Growth >= rule.GrowMinutes && !string.IsNullOrEmpty(rule.MatureId))
                    {
                        item.DefinitionId = rule.MatureId; item.Mature = true; item.MatureAt = s.Minute; item.Growth = 0;
                        CardRule adult = Card(item.DefinitionId);
                        item.Durability = adult.InitialDurability; item.MaxDurability = adult.InitialDurability; item.Structure = adult.Structure;
                        TryPlaceAdult(s, item, owner);
                    }
                    continue;
                }
                if (role == CardRole.AdultSnail)
                {
                    if (owner == null || Card(owner.DefinitionId).Role != CardRole.Pen) continue;
                    if (item.Place == Place.Young && !TryPlaceAdult(s, item, owner)) continue;
                    if (item.Place != Place.Adult || !item.Fed || owner.Structure <= 0 || IsLocked(s, item.Id)
                        || item.LastProductionWindow == item.FeedWindowAt) continue;
                    RecipeRule production = def.Recipes.Find(r => !r.Human && r.AutomaticMinutes <= 0 && TargetMatches(r, item)
                        && r.RequiredRole == CardRole.AdultSnail);
                    if (production != null)
                    {
                        SurvivalResult begin = BeginRecipe(s, production.Id, item.Id);
                        item = Item(s, id);
                        if (item != null) item.Status = begin.Success ? "" : begin.Error;
                    }
                    continue;
                }
                if (owner == null || Card(owner.DefinitionId).Role != CardRole.Garden) continue;
                if (!item.Mature)
                {
                    TraitRule trait = def.Traits.Find(t => t.Id == item.TraitId && t.Species == role);
                    double duration = rule.GrowMinutes * (trait == null ? 1 : trait.DurationMultiplier);
                    item.Growth += RegionFactor(s, item);
                    if (item.Growth >= duration)
                    {
                        item.Mature = true; item.MatureAt = s.Minute; item.Production = 0;
                        item.Status = "";
                    }
                    continue;
                }
                if (role != CardRole.AloePlant || s.Minute < item.CooldownUntil || IsLocked(s, item.Id)) continue;
                if (item.Stock.Sum(a => a.Count) >= rule.StoredLimit) { item.Status = "储叶已满"; continue; }
                if (item.Production <= 0)
                {
                    if (owner.Structure <= 0) { item.Status = "花圃已损坏，维修后启动下一轮产叶"; continue; }
                    if (!TakeBiologicalInputs(s, rule.Feed, owner.Id)) { item.Status = "等待肥料"; continue; }
                    item.Production = double.Epsilon;
                }
                item.Status = "";
                item.Production += RegionFactor(s, item);
                if (item.Production >= rule.ProductionMinutes)
                {
                    foreach (Amount produced in rule.Production)
                    {
                        Amount stored = item.Stock.Find(a => a.DefinitionId == produced.DefinitionId);
                        if (stored == null) { stored = new Amount(produced.DefinitionId, 0); item.Stock.Add(stored); }
                        stored.Count = Math.Min(rule.StoredLimit, stored.Count + produced.Count);
                    }
                    item.Production = 0;
                }
            }
        }

        private bool TryPlaceAdult(SurvivalState s, ItemState item, ItemState pen)
        {
            int slots = Card(pen.DefinitionId).AdultSlots;
            for (int slot = 0; slot < slots; slot++)
            {
                if (s.Items.Exists(i => i.Id != item.Id && i.OwnerId == pen.Id && (i.Place == Place.Adult || i.Place == Place.Residue && i.ResidueFrom == Place.Adult) && i.Slot == slot)
                    || s.Reservations.Exists(r => r.OwnerId == pen.Id && r.Place == Place.Adult && r.Slot == slot)) continue;
                item.Place = Place.Adult; item.Slot = slot; item.Status = "";
                return true;
            }
            item.Status = "已成年，成年槽不足；移至另一围栏后可以生产";
            return false;
        }

        // Individual feed windows are settled by TickBiologyExpire, independently of the global day.
        private void OnDailyBiology(SurvivalState s) { }
    }
}
