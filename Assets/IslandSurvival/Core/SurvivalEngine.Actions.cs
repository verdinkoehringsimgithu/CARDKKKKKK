using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        public SurvivalResult StartRecipe(string recipeId, string targetId = "", string secondaryId = "", string woundId = "", int batch = 1, string traitId = "", string ingredientId = "")
            => Command(s => BeginRecipe(s, recipeId, targetId, secondaryId, woundId, batch, traitId, ingredientId));

        public SurvivalResult PreviewRecipe(string recipeId, string targetId = "", string secondaryId = "", string woundId = "", int batch = 1, string traitId = "", string ingredientId = "")
            => Command(s => BeginRecipe(s, recipeId, targetId, secondaryId, woundId, batch, traitId, ingredientId), true);

        public SurvivalResult CancelTask(string taskId) => Command(s =>
        {
            TaskState task = s.Tasks.Find(t => t.Id == taskId);
            if (task == null) return SurvivalResult.Fail("工作已经结束或不存在。");
            ItemState owner = Item(s, task.TargetId);
            if (owner != null) { owner.Repeat = false; owner.RepeatRecipe = ""; owner.RepeatSecondaryId = ""; }
            CancelStartedTask(s, task);
            AddLog(s, "已中止工作；本轮材料、工具损耗和已付劳动保留。尚未开始的批次不扣费。");
            return SurvivalResult.Ok();
        });

        public SurvivalResult CancelQueuedWound(string taskId, string woundId) => Command(s =>
        {
            TaskState task = s.Tasks.Find(t => t.Id == taskId);
            if (task == null || !task.QueueWounds.Remove(woundId)) return SurvivalResult.Fail("该伤口不在未开始的护理队列中。");
            return SurvivalResult.Ok("已移除这项尚未开始的护理。");
        });

        public SurvivalResult Refuel(string fireId, string fuelItemId, int count = 1) => Command(s =>
        {
            ItemState fire = Item(s, fireId), fuel = Item(s, fuelItemId);
            if (s.Dead) return SurvivalResult.Fail("本局已经结束。");
            if (fire == null || Card(fire.DefinitionId).Role != CardRole.Fire) return SurvivalResult.Fail("请选择篝火。");
            if (fire.Structure <= 0) return SurvivalResult.Fail("篝火已损坏，请先维修。");
            if (fuel == null || count <= 0 || UsableCount(fuel, s.Minute) < count || IsLocked(s, fuel.Id)) return SurvivalResult.Fail("燃料数量不足或正在占用。");
            CardRule rule = Card(fuel.DefinitionId), fireRule = Card(fire.DefinitionId);
            if (rule.FuelValue <= 0) return SurvivalResult.Fail("这张材料不能作为燃料。");
            if (fire.Fuel + rule.FuelValue * count > fireRule.FuelCapacity + 0.0000001)
                return SurvivalResult.Fail("燃料会超过上限；请减少装料数量。");
            ConsumeExact(fuel, count);
            fire.Fuel += rule.FuelValue * count;
            RemoveEmpty(s);
            return SurvivalResult.Ok("已装入燃料。");
        });

        public SurvivalResult Eat(string itemId, int count = 1) => Command(s =>
        {
            ItemState item = Item(s, itemId);
            if (s.Dead) return SurvivalResult.Fail("本局已经结束。");
            if (item == null || count <= 0 || item.Quantity < count) return SurvivalResult.Fail("食物数量不足。");
            if (IsLocked(s, itemId)) return SurvivalResult.Fail("食物正在被工作占用。");
            CardRule rule = Card(item.DefinitionId);
            if (rule.Kind != CardKind.Material || rule.Nutrition <= 0) return SurvivalResult.Fail("这张材料不能直接食用。");
            if (UsableCount(item, s.Minute) < count) return SurvivalResult.Fail("食物已经过期。");
            ConsumeExact(item, count);
            s.Body.Food = Clamp(s.Body.Food + rule.Nutrition * count / 100.0);
            RemoveEmpty(s);
            return SurvivalResult.Ok("已食用" + rule.Name + "。超出饱食上限的部分不储存。");
        });

        public SurvivalResult Repair(string itemId) => StartTypedRecipe(RecipeKind.Repair, itemId);
        public SurvivalResult Salvage(string itemId) => StartTypedRecipe(RecipeKind.Salvage, itemId);

        private SurvivalResult StartTypedRecipe(RecipeKind kind, string itemId)
        {
            return Command(s =>
            {
                ItemState item = Item(s, itemId);
                if (item == null) return SurvivalResult.Fail("目标不存在。");
                RecipeRule rule = def.Recipes.FirstOrDefault(r => r.Kind == kind && TargetMatches(r, item));
                if (rule == null) return SurvivalResult.Fail(kind == RecipeKind.Repair ? "该对象没有维修工艺。" : "该对象没有拆解工艺。");
                return BeginRecipe(s, rule.Id, itemId, "", "", 1, "");
            });
        }

        public SurvivalResult SetRepeat(string itemId, string recipeId, bool enabled) => Command(s =>
        {
            ItemState item = Item(s, itemId);
            RecipeRule recipe = Recipe(recipeId);
            if (item == null || recipe == null || recipe.Human || recipe.AutomaticMinutes > 0 || !TargetMatches(recipe, item))
                return SurvivalResult.Fail("只能为适用的自动生产设置重复。");
            if (!RecipeKnown(s, recipe)) return SurvivalResult.Fail("尚未发现这项工艺。");
            if (enabled && recipe.SecondaryRole != CardRole.None)
            {
                TaskState active = s.Tasks.Find(t => t.TargetId == item.Id && t.RecipeId == recipe.Id);
                string secondaryId = active == null ? item.RepeatSecondaryId : active.SecondaryId;
                ItemState secondary = Item(s, secondaryId);
                if (secondary == null || Card(secondary.DefinitionId).Role != recipe.SecondaryRole)
                    return SurvivalResult.Fail("请先绑定两台设备并启动一轮，再开启重复。");
                item.RepeatSecondaryId = secondaryId;
            }
            item.Repeat = enabled;
            item.RepeatRecipe = enabled ? recipeId : "";
            if (!enabled) item.RepeatSecondaryId = "";
            if (!enabled && item.Status == "等待材料或输出位置") item.Status = "";
            return SurvivalResult.Ok(enabled ? "已开启逐周期重复。" : "已关闭重复；已开始的本轮继续。");
        });

        public SurvivalResult SetReserve(string itemId, string definitionId, int count) => Command(s =>
        {
            ItemState item = Item(s, itemId);
            if (item == null || Card(definitionId) == null || Card(definitionId).Kind != CardKind.Material || count < 0)
                return SurvivalResult.Fail("保留量或材料无效。");
            CardRule owner = Card(item.DefinitionId);
            if (owner.Kind != CardKind.Area && owner.Kind != CardKind.Facility) return SurvivalResult.Fail("请选择生产区域或设施。");
            Amount reserve = item.Reserves.Find(a => a.DefinitionId == definitionId);
            if (count == 0) { if (reserve != null) item.Reserves.Remove(reserve); }
            else if (reserve == null) item.Reserves.Add(new Amount(definitionId, count));
            else reserve.Count = count;
            return SurvivalResult.Ok("已设置材料保留量。");
        });

        private SurvivalResult BeginRecipe(SurvivalState s, string recipeId, string targetId = "", string secondaryId = "", string woundId = "", int batch = 1, string traitId = "", string ingredientId = "", string preferredDefinitionId = "")
        {
            // 自动循环也调用本入口；失败必须局部回滚，不能依赖最外层Advance失败。
            SurvivalState candidate = SurvivalCopies.Copy(s);
            SurvivalResult result = PrepareRecipe(candidate, recipeId, targetId, secondaryId, woundId, batch, traitId, ingredientId, preferredDefinitionId);
            if (!result.Success) return result;
            foreach (var field in typeof(SurvivalState).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                field.SetValue(s, field.GetValue(candidate));
            return result;
        }

        private SurvivalResult PrepareRecipe(SurvivalState s, string recipeId, string targetId, string secondaryId, string woundId, int batch, string traitId, string ingredientId = "", string preferredDefinitionId = "")
        {
            RecipeRule recipe = Recipe(recipeId);
            if (recipe == null) return SurvivalResult.Fail("工艺不存在。");
            ItemState preferredIngredient = null;
            if (!string.IsNullOrEmpty(ingredientId))
            {
                preferredIngredient = Item(s, ingredientId);
                if (preferredIngredient == null || preferredIngredient.Quantity <= 0 || Card(preferredIngredient.DefinitionId).Kind != CardKind.Material ||
                    !recipe.Inputs.Any(i => i.Alternatives.Contains(preferredIngredient.DefinitionId)))
                    return SurvivalResult.Fail("选中的材料已不存在，或不属于这项工艺的输入。");
                if (!string.IsNullOrEmpty(preferredDefinitionId) && preferredDefinitionId != preferredIngredient.DefinitionId)
                    return SurvivalResult.Fail("选中的材料与本批次指定的材料类型不一致。");
                preferredDefinitionId = preferredIngredient.DefinitionId;
            }
            if (!string.IsNullOrEmpty(preferredDefinitionId) &&
                (Card(preferredDefinitionId) == null || Card(preferredDefinitionId).Kind != CardKind.Material || !recipe.Inputs.Any(i => i.Alternatives.Contains(preferredDefinitionId))))
                return SurvivalResult.Fail("指定的材料类型不属于这项工艺的输入。");
            if (s.Dead || (s.Completed && !s.ContinueAfterWin)) return SurvivalResult.Fail("本局已结束，请先选择继续经营或读取存档。");
            if (batch < 1 || batch > 1000) return SurvivalResult.Fail("批量次数必须在1到1000之间。");
            if (!RecipeKnown(s, recipe)) return SurvivalResult.Fail("尚未发现这项工艺。");
            if (recipe.Human)
            {
                TaskState active = s.Tasks.Find(t => t.Human && !t.Suspended);
                if (recipe.Kind == RecipeKind.Rest && active != null && Recipe(active.RecipeId).Kind != RecipeKind.Rest)
                    active.Suspended = true;
                else if (active != null || (recipe.Kind != RecipeKind.Rest && s.Tasks.Exists(t => t.Human && t.Suspended)))
                    return SurvivalResult.Fail("人力正在工作，请先完成或中止。");
            }
            if (recipe.Human && recipe.Kind != RecipeKind.Rest && s.Body.Mind <= 0) return SurvivalResult.Fail("精神不足，请先休息。");
            ItemState target = Item(s, targetId), secondary = Item(s, secondaryId);
            bool conditionTarget = target != null && Card(target.DefinitionId).Kind == CardKind.Condition;
            bool medical = recipe.Kind == RecipeKind.Care || recipe.Kind == RecipeKind.Clean;
            if (conditionTarget && !medical) return SurvivalResult.Fail("状态卡请使用适用的护理方式，不能作为材料加工或拆除。");
            if ((recipe.TargetRequired || recipe.RequiredRole != CardRole.None || recipe.TargetDefinitions.Count > 0) && target == null)
                return SurvivalResult.Fail("请先选择工作目标。");
            if (target != null && !conditionTarget && !TargetMatches(recipe, target)) return SurvivalResult.Fail("该工艺不适用于选中对象。");
            if (target != null && IsLocked(s, target.Id)) return SurvivalResult.Fail("目标正在工作，请先完成或中止。");
            if (recipe.SecondaryRole != CardRole.None)
            {
                if (secondary == null || Card(secondary.DefinitionId).Role != recipe.SecondaryRole || secondary.Id == targetId)
                    return SurvivalResult.Fail("还缺" + RoleName(recipe.SecondaryRole) + "，请明确选择第二台设备。");
                if (IsLocked(s, secondary.Id)) return SurvivalResult.Fail("第二台设备正在工作。");
            }
            foreach (ItemState facility in new[] { target, secondary })
            {
                if (facility == null) continue;
                CardRule f = Card(facility.DefinitionId);
                if (f.Structure > 0 && facility.Structure <= 0 && recipe.Kind != RecipeKind.Repair && recipe.Kind != RecipeKind.Demolish)
                    return SurvivalResult.Fail(f.Name + "已损坏，请先维修。");
            }
            if (recipe.RequiresCareMilestone)
            {
                if (target == null || !s.GatheredNodes.Contains(target.Id)) return SurvivalResult.Fail("先完成这个节点的普通采集。");
                if (!s.Milestones.Contains("care_ready")) return SurvivalResult.Fail("先制作一份简易敷料或绷带。");
            }
            if (recipe.ForcedGather && target != null && target.LastForcedDay == Today(s).Day) return SurvivalResult.Fail("这个节点今天已经进行过强行采集。");
            string careWarning = EmergencyCareWarning(s, recipe);
            if (!string.IsNullOrEmpty(traitId) && !def.Traits.Exists(t => t.Id == traitId)) return SurvivalResult.Fail("性状不存在。");

            var wounds = (woundId ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            var task = new TaskState
            {
                Id = NewId(s, "task_"), RecipeId = recipe.Id, TargetId = targetId ?? "", SecondaryId = secondaryId ?? "",
                WoundId = wounds.Count == 0 ? "" : wounds[0], TraitId = traitId ?? "", Human = recipe.Human,
                StartedAt = s.Minute, Remaining = batch, WorkMinutes = recipe.Minutes,
                WorkSourceId = WorkSource(s, target), PreferredItemId = preferredIngredient == null ? "" : preferredIngredient.Id,
                PreferredMaterialId = preferredDefinitionId ?? ""
            };
            if (wounds.Count > 1) task.QueueWounds.AddRange(wounds.Skip(1));
            if (!string.IsNullOrEmpty(task.TraitId) && (recipe.Kind == RecipeKind.Breed || recipe.Kind == RecipeKind.SelectSeed)) task.WorkMinutes += recipe.ExtraTraitMinutes;
            if (target != null) task.LockedItems.Add(target.Id);
            if (secondary != null) task.LockedItems.Add(secondary.Id);

            List<Amount> outputs = SurvivalCopies.Copy(recipe.Outputs);
            SurvivalResult prepare = PrepareTarget(s, task, recipe, target, outputs);
            if (!prepare.Success) return prepare;
            ItemState tool;
            string toolError = FindWorkTool(s, task, recipe, out tool);
            if (toolError != "") return SurvivalResult.Fail(toolError);
            double efficiency = recipe.Human ? TaskEfficiency(s, task, recipe) : (recipe.Environmental ? EnvironmentEfficiency(s, InputOwner(s, target)) : 1);
            double estimate = task.WorkMinutes / Math.Max(0.0000001, efficiency) + recipe.AutomaticMinutes;
            bool regionInputs = recipe.Kind == RecipeKind.Plant || recipe.Kind == RecipeKind.Breed;
            string inputOwner = !recipe.Human || regionInputs ? InputOwner(s, target) : null;
            string inputError = ConsumeIngredients(s, task, recipe.Inputs, recipe, inputOwner, estimate, target);
            if (inputError != "") return SurvivalResult.Fail(inputError);
            if (!string.IsNullOrEmpty(task.TraitId) && (recipe.Kind == RecipeKind.Breed || recipe.Kind == RecipeKind.SelectSeed) && recipe.ExtraTraitCost.Count > 0)
            {
                var extra = recipe.ExtraTraitCost.Select(a => new Ingredient { Alternatives = new List<string> { a.DefinitionId }, Count = a.Count }).ToList();
                inputError = ConsumeIngredients(s, task, extra, recipe, inputOwner, estimate, null);
                if (inputError != "") return SurvivalResult.Fail(inputError);
            }
            if (tool != null && recipe.ToolWear > 0)
            {
                tool.Durability -= recipe.ToolWear;
                if (tool.Durability == 0) tool.Status = string.IsNullOrEmpty(Card(tool.DefinitionId).DepletedStatus) ? "已耗尽" : Card(tool.DefinitionId).DepletedStatus;
            }
            string fuelError = ConsumeFuel(s, task, recipe, target, secondary, outputs);
            if (fuelError != "") return SurvivalResult.Fail(fuelError);
            SurvivalResult special = BeginSpecial(s, task, recipe);
            if (!special.Success) return special;
            // 消耗材料腾出的占地必须可用于本轮产出；特殊种植已读取选定种苗。
            task.LockedItems.RemoveAll(id => { ItemState i = Item(s, id); return i == null || i.Quantity <= 0; });
            if (target != null && target.Quantity <= 0) task.TargetId = "";
            if (secondary != null && secondary.Quantity <= 0) task.SecondaryId = "";
            RemoveEmpty(s);
            if (!s.Reservations.Exists(r => r.TaskId == task.Id))
            {
                ItemState outputFacility = secondary != null && Card(secondary.DefinitionId).Role == CardRole.DryingRack ? secondary : target;
                bool facilityOutput = !task.Human && outputFacility != null && (Card(outputFacility.DefinitionId).Kind == CardKind.Facility || Card(outputFacility.DefinitionId).Kind == CardKind.Area);
                string error = TryReserveOutputs(s, task, outputs, facilityOutput ? outputFacility.Id : "", facilityOutput ? Place.Output : Place.Table);
                if (error != "") return SurvivalResult.Fail(error);
            }
            if (recipe.ForcedGather && target != null) target.LastForcedDay = Today(s).Day;
            foreach (ItemState f in new[] { target, secondary })
                if (f != null && recipe.Kind != RecipeKind.Repair && recipe.Kind != RecipeKind.Demolish)
                {
                    CardRule rule = Card(f.DefinitionId);
                    if (rule.WorkWear > 0) { f.Structure = Math.Max(0, f.Structure - rule.WorkWear); if (f.Structure == 0) f.Fuel = 0; }
                }
            s.Tasks.Add(task);
            RemoveEmpty(s);
            WorkRates(s, recipe, out double foodRate, out double mindRate);
            double humanHours = task.Human ? task.WorkMinutes / Math.Max(0.0000001, efficiency) / 60.0 * batch : 0;
            return new SurvivalResult
            {
                Success = true, Description = DescribeStart(s, task, recipe, batch), EstimatedMinutes = estimate * batch,
                EstimatedFood = foodRate * humanHours, EstimatedMind = mindRate * humanHours, Warning = careWarning
            };
        }

        // 桌面材料堆可能在开工后消失，工作来源不能依赖被扣空的实例ID。
        private string WorkSource(SurvivalState s, ItemState item)
        {
            ItemState root = FindRootItem(s, item);
            return root == null || Card(root.DefinitionId).Kind == CardKind.Material ? "table" : root.Id;
        }

        private bool RecipeKnown(SurvivalState s, RecipeRule recipe)
        {
            return recipe.InitialKnown || (!string.IsNullOrEmpty(recipe.DiscoverMaterial) && recipe.DiscoverMaterial.Split('|').Any(id => s.Discovered.Contains(id))) ||
                (!string.IsNullOrEmpty(recipe.DiscoverMilestone) && s.Milestones.Contains(recipe.DiscoverMilestone));
        }

        private string DescribeStart(SurvivalState s, TaskState task, RecipeRule recipe, int batch)
        {
            var costs = task.Consumed.GroupBy(a => a.DefinitionId).Select(g => Card(g.Key).Name + "×" + (g.Sum(a => a.Count) * batch)).ToList();
            if (recipe.ToolWear > 0) costs.Add("工具耐久×" + (recipe.ToolWear * batch));
            if (recipe.Fuel > 0) costs.Add("燃料×" + (recipe.Fuel * batch));
            string text = "已开始" + recipe.Name + (batch > 1 ? "，共" + batch + "轮。" : "。");
            if (costs.Count > 0) text += (batch > 1 ? "按本轮配方估算总投入：" : "本轮投入：") + string.Join("、", costs) + "。";
            int units = s.Reservations.Where(r => r.TaskId == task.Id && r.Place == Place.Table && string.IsNullOrEmpty(r.StackId)).Sum(r => Card(r.DefinitionId).Footprint);
            if (units > 0) text += "本轮已预留" + units + "单位产出占地。";
            if (batch > 1) text += "每轮重新检查，未开始的轮次尚未扣费。";
            return text;
        }

        private bool TargetMatches(RecipeRule recipe, ItemState target)
        {
            CardRule rule = Card(target.DefinitionId);
            return (recipe.TargetDefinitions.Count == 0 || recipe.TargetDefinitions.Contains(target.DefinitionId)) &&
                (recipe.RequiredRole == CardRole.None || rule.Role == recipe.RequiredRole);
        }

        private string InputOwner(SurvivalState s, ItemState target)
        {
            if (target == null) return "";
            CardKind kind = Card(target.DefinitionId).Kind;
            if (kind == CardKind.Area || kind == CardKind.Facility) return target.Id;
            ItemState root = FindRootItem(s, target);
            return root == null || root == target ? "" : root.Id;
        }

        private static string RoleName(CardRole role)
        {
            switch (role)
            {
                case CardRole.Fire: return "篝火";
                case CardRole.DryingRack: return "晾晒架";
                case CardRole.Garden: return "花圃";
                case CardRole.Pen: return "围栏";
                case CardRole.Compost: return "堆肥区";
                default: return "适用设施";
            }
        }

        private static int UsableCount(ItemState item, double afterMinute, long availableAt = long.MaxValue)
        {
            if (item.Lots.Count == 0) return item.Quantity;
            return item.Lots.Where(l => l.AvailableAt <= availableAt && (l.ExpiresAt == 0 || l.ExpiresAt > afterMinute)).Sum(l => l.Count);
        }

        // 只扣被选定实例的最早批次，不通过同定义搜索偷取其他堆。
        private static void ConsumeExact(ItemState item, int count, long availableAt = long.MaxValue)
        {
            if (count < 0 || item.Quantity < count) throw new InvalidOperationException("材料数量已经变化。");
            int left = count;
            foreach (Lot lot in item.Lots.Where(l => l.AvailableAt <= availableAt && (availableAt == long.MaxValue || l.ExpiresAt == 0 || l.ExpiresAt > availableAt)).OrderBy(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt).ToArray())
            {
                int take = Math.Min(left, lot.Count); lot.Count -= take; left -= take;
                if (left == 0) break;
            }
            if (item.Lots.Count > 0 && left != 0) throw new InvalidOperationException("材料批次尚未完成交付。");
            item.Lots.RemoveAll(l => l.Count <= 0);
            item.Quantity -= count;
        }
    }
}
