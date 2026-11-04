using System;
using System.Collections.Generic;
using System.Text;

namespace CardKKK.TestGameplay.Core
{
    /// <summary>只管理测试玩法。所有命令在候选副本上结算，校验后一次提交。</summary>
    public sealed partial class WorkshopEngine
    {
        private const string WoodId = "wood";
        private const string CharcoalId = "charcoal";
        private const string AxeId = "copper_axe";
        private readonly WorkshopDefinition definition;
        private readonly Dictionary<string, ItemDefinitionData> items = new Dictionary<string, ItemDefinitionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeDefinitionData> recipes = new Dictionary<string, RecipeDefinitionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GatherDefinitionData> gathers = new Dictionary<string, GatherDefinitionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ScenarioData> scenarios = new Dictionary<string, ScenarioData>(StringComparer.Ordinal);
        private readonly HashSet<string> uniqueItems = new HashSet<string>(StringComparer.Ordinal);
        private WorkshopSnapshot state;
        private bool executing;

        public WorkshopDefinition Definition => WorkshopCopy.Definition(definition);
        public WorkshopSnapshot Snapshot => WorkshopCopy.Snapshot(state);
        public event Action<WorkshopSnapshot> Changed;
        // 观察者失败不能将已经提交的操作伪装成失败。Unity 层可订阅后记录。
        public event Action<Exception> ObserverFailed;

        public WorkshopEngine(WorkshopDefinition definition, string scenarioId)
        {
            this.definition = WorkshopCopy.Definition(definition);
            ValidateDefinition();
            if (scenarioId == null || !scenarios.ContainsKey(scenarioId))
                throw new ArgumentException("找不到测试玩法的开局。", nameof(scenarioId));
            state = CreateInitialState(scenarios[scenarioId]);
        }

        public int InventoryCount(string itemId) => Count(state, itemId);

        public bool IsRecipeAvailable(string recipeId)
        {
            return recipeId != null && recipes.ContainsKey(recipeId) &&
                Available(scenarios[state.ScenarioId], recipeId);
        }

        public int GetGatherMinutes(string gatherId)
        {
            if (gatherId == null || !gathers.TryGetValue(gatherId, out GatherDefinitionData gather)) return 0;
            return GatherMinutes(state, gather);
        }

        public RecipeOffer GetOffer(string recipeId) => Offer(state, recipeId);

        public WorkshopResult StartRecipe(string recipeId)
        {
            return Execute((candidate, events) =>
            {
                RecipeOffer offer = Offer(candidate, recipeId);
                if (!offer.CanStart) throw new Rejected(offer.Reason);
                RecipeDefinitionData recipe = recipes[recipeId];
                int start = candidate.Minutes;
                foreach (ItemAmount input in offer.EffectiveInputs)
                {
                    Change(candidate, input.ItemId, -input.Quantity);
                    if (input.ItemId == WoodId) candidate.WoodSpent = checked(candidate.WoodSpent + input.Quantity);
                }
                events.Add(new WorkshopEvent
                {
                    Kind = WorkshopEventKind.RecipeStarted, RecipeId = recipe.Id,
                    FacilityId = recipe.FacilityId, Minute = start,
                    Items = WorkshopCopy.Amounts(offer.EffectiveInputs)
                });
                // 命令独占且预先检查了工位；准备中没有其他命令能插入同一工位。
                AdvanceTo(candidate, checked(start + recipe.PreparationMinutes), events);
                if (recipe.ProcessMinutes > 0)
                {
                    candidate.Jobs.Add(new WorkshopJob
                    {
                        BatchId = Guid.NewGuid().ToString("N"), RecipeId = recipe.Id,
                        FacilityId = recipe.FacilityId, StartedMinute = candidate.Minutes,
                        FinishMinute = checked(candidate.Minutes + recipe.ProcessMinutes)
                    });
                }
                else
                {
                    CompleteRecipe(candidate, recipe, events);
                }
                return candidate;
            });
        }

        public WorkshopResult Gather(string gatherId)
        {
            return Execute((candidate, events) =>
            {
                if (gatherId == null || !gathers.TryGetValue(gatherId, out GatherDefinitionData gather))
                    throw new Rejected("找不到这项采集操作。");
                bool usesAxe = Count(candidate, AxeId) > 0 && Contains(gather.Outputs, WoodId);
                int duration = GatherMinutes(candidate, gather);
                AdvanceTo(candidate, checked(candidate.Minutes + duration), events);
                Grant(candidate, gather.Outputs);
                if (usesAxe) candidate.AxeGatherCount = checked(candidate.AxeGatherCount + 1);
                events.Add(new WorkshopEvent
                {
                    Kind = WorkshopEventKind.GatherCompleted, RecipeId = gather.Id,
                    FacilityId = gather.SiteId, Minute = candidate.Minutes,
                    Items = WorkshopCopy.Amounts(gather.Outputs)
                });
                CheckGoal(candidate, events);
                return candidate;
            });
        }

        public WorkshopResult WaitForNextCompletion()
        {
            return Execute((candidate, events) =>
            {
                if (candidate.Jobs.Count == 0) throw new Rejected("没有正在烧制的工艺。");
                int finish = int.MaxValue;
                foreach (WorkshopJob job in candidate.Jobs) finish = Math.Min(finish, job.FinishMinute);
                AdvanceTo(candidate, finish, events);
                return candidate;
            });
        }

        public WorkshopResult Restart(string scenarioId)
        {
            return Execute((candidate, events) =>
            {
                if (scenarioId == null || !scenarios.TryGetValue(scenarioId, out ScenarioData scenario))
                    throw new Rejected("找不到这个开局。");
                return CreateInitialState(scenario);
            }, false);
        }

        public WorkshopResult Restore(WorkshopSnapshot snapshot)
        {
            return Execute((candidate, events) =>
            {
                if (snapshot == null) throw new Rejected("存档内容为空。");
                return WorkshopCopy.Snapshot(snapshot);
            }, false);
        }

        private WorkshopResult Execute(
            Func<WorkshopSnapshot, List<WorkshopEvent>, WorkshopSnapshot> operation, bool reportNewGoal = true)
        {
            if (executing) return WorkshopResult.Fail("上一项操作正在结算。");
            executing = true;
            try
            {
                var events = new List<WorkshopEvent>();
                WorkshopSnapshot candidate = operation(WorkshopCopy.Snapshot(state), events);
                string error = ValidateState(candidate);
                if (error != null) return WorkshopResult.Fail(error);
                candidate.Revision = checked(state.Revision + 1);
                bool goalJustCompleted = reportNewGoal && !state.GoalComplete && candidate.GoalComplete;
                state = candidate;
                NotifyChanged();
                return new WorkshopResult(true, "", goalJustCompleted, events);
            }
            catch (Rejected exception) { return WorkshopResult.Fail(exception.Message); }
            catch (OverflowException) { return WorkshopResult.Fail("数量或时间超过可保存范围，操作未执行。"); }
            finally { executing = false; }
        }

        private RecipeOffer Offer(WorkshopSnapshot snapshot, string recipeId)
        {
            var offer = new RecipeOffer
            {
                RecipeId = recipeId, Reason = "", EffectiveInputs = new List<ItemAmount>()
            };
            if (recipeId == null || !recipes.TryGetValue(recipeId, out RecipeDefinitionData recipe))
            { offer.Reason = "找不到这项工艺。"; return offer; }
            offer.PreparationMinutes = recipe.PreparationMinutes;
            offer.ProcessMinutes = recipe.ProcessMinutes;
            offer.EffectiveInputs = EffectiveInputs(snapshot, recipe);
            ScenarioData scenario = scenarios[snapshot.ScenarioId];
            if (!Available(scenario, recipeId))
            { offer.Reason = "这个开局没有开放这项工艺。"; return offer; }
            offer.IsKnown = Known(snapshot, scenario, recipe);
            if (!offer.IsKnown)
            {
                offer.Reason = string.IsNullOrWhiteSpace(recipe.UnlockItemId)
                    ? "这项工艺尚未开放。"
                    : "先获得" + ItemName(recipe.UnlockItemId);
                return offer;
            }
            if (recipe.InstallLining && snapshot.KilnLined)
            { offer.Reason = "已经装好炉衬。"; return offer; }
            if (recipe.UniqueOutput)
            {
                foreach (ItemAmount output in recipe.Outputs)
                {
                    if (Count(snapshot, output.ItemId) > 0 || IsBeingProduced(snapshot, output.ItemId))
                    { offer.Reason = "已有或正在制作" + ItemName(output.ItemId); return offer; }
                }
            }
            foreach (WorkshopJob job in snapshot.Jobs)
                if (job.FacilityId == recipe.FacilityId)
                { offer.Reason = "工位正在烧制，还需 " + (job.FinishMinute - snapshot.Minutes) + " 分钟"; return offer; }
            if (!string.IsNullOrWhiteSpace(recipe.RequiredToolId) && Count(snapshot, recipe.RequiredToolId) < 1)
            { offer.Reason = "需要" + ItemName(recipe.RequiredToolId); return offer; }
            var missing = new StringBuilder();
            foreach (ItemAmount input in offer.EffectiveInputs)
            {
                int lack = input.Quantity - Count(snapshot, input.ItemId);
                if (lack <= 0) continue;
                if (missing.Length > 0) missing.Append("、");
                missing.Append(ItemName(input.ItemId)).Append(' ').Append(lack);
            }
            if (missing.Length > 0)
            { offer.Reason = "还缺" + missing; return offer; }
            offer.CanStart = true;
            return offer;
        }

        private List<ItemAmount> EffectiveInputs(WorkshopSnapshot snapshot, RecipeDefinitionData recipe)
        {
            List<ItemAmount> result = WorkshopCopy.Amounts(recipe.Inputs);
            if (snapshot.KilnLined && recipe.LinedCharcoalCost >= 0)
                foreach (ItemAmount amount in result)
                    if (amount.ItemId == CharcoalId) amount.Quantity = recipe.LinedCharcoalCost;
            return result;
        }

        private int GatherMinutes(WorkshopSnapshot snapshot, GatherDefinitionData gather)
        {
            return gather.MinutesWithAxe > 0 && Count(snapshot, AxeId) > 0
                ? gather.MinutesWithAxe : gather.Minutes;
        }

        private void AdvanceTo(WorkshopSnapshot snapshot, int target, List<WorkshopEvent> events)
        {
            if (target < snapshot.Minutes) throw new Rejected("不能把时间倒退。");
            while (true)
            {
                WorkshopJob next = null;
                foreach (WorkshopJob job in snapshot.Jobs)
                    if (job.FinishMinute <= target &&
                        (next == null || job.FinishMinute < next.FinishMinute ||
                        (job.FinishMinute == next.FinishMinute && string.CompareOrdinal(job.BatchId, next.BatchId) < 0)))
                        next = job;
                if (next == null) break;
                snapshot.Minutes = next.FinishMinute;
                snapshot.Jobs.Remove(next);
                CompleteRecipe(snapshot, recipes[next.RecipeId], events);
            }
            snapshot.Minutes = target;
        }

        private void CompleteRecipe(WorkshopSnapshot snapshot, RecipeDefinitionData recipe,
            List<WorkshopEvent> events)
        {
            if (recipe.InstallLining)
            {
                if (snapshot.KilnLined) throw new Rejected("炉衬不能重复安装。");
                snapshot.KilnLined = true;
                events.Add(new WorkshopEvent
                {
                    Kind = WorkshopEventKind.LiningInstalled, RecipeId = recipe.Id,
                    FacilityId = recipe.FacilityId, Minute = snapshot.Minutes
                });
            }
            Grant(snapshot, recipe.Outputs);
            Grant(snapshot, recipe.Byproducts);
            var produced = WorkshopCopy.Amounts(recipe.Outputs);
            produced.AddRange(WorkshopCopy.Amounts(recipe.Byproducts));
            events.Add(new WorkshopEvent
            {
                Kind = WorkshopEventKind.RecipeCompleted, RecipeId = recipe.Id,
                FacilityId = recipe.FacilityId, Minute = snapshot.Minutes, Items = produced
            });
            CheckGoal(snapshot, events);
        }

        private void Grant(WorkshopSnapshot snapshot, List<ItemAmount> amounts)
        {
            foreach (ItemAmount amount in amounts)
            {
                Change(snapshot, amount.ItemId, amount.Quantity);
                if (!snapshot.EverObtained.Contains(amount.ItemId)) snapshot.EverObtained.Add(amount.ItemId);
            }
        }

        private static void Change(WorkshopSnapshot snapshot, string itemId, int delta)
        {
            for (int index = 0; index < snapshot.Inventory.Count; index++)
            {
                ItemAmount entry = snapshot.Inventory[index];
                if (entry.ItemId != itemId) continue;
                int result = checked(entry.Quantity + delta);
                if (result < 0) throw new Rejected("库存不足，操作未执行。");
                if (result == 0) snapshot.Inventory.RemoveAt(index);
                else entry.Quantity = result;
                return;
            }
            if (delta < 0) throw new Rejected("库存不足，操作未执行。");
            if (delta > 0) snapshot.Inventory.Add(new ItemAmount(itemId, delta));
        }

        private void CheckGoal(WorkshopSnapshot snapshot, List<WorkshopEvent> events)
        {
            ScenarioData scenario = scenarios[snapshot.ScenarioId];
            if (snapshot.GoalComplete || Count(snapshot, scenario.GoalItemId) < scenario.GoalQuantity ||
                (scenario.RequireAxeGather && snapshot.AxeGatherCount == 0)) return;
            snapshot.GoalComplete = true;
            snapshot.CompletionMinutes = snapshot.Minutes;
            snapshot.CompletionWoodSpent = snapshot.WoodSpent;
            events.Add(new WorkshopEvent { Kind = WorkshopEventKind.GoalCompleted, Minute = snapshot.Minutes });
        }

        private WorkshopSnapshot CreateInitialState(ScenarioData scenario)
        {
            var result = new WorkshopSnapshot
            {
                ContentVersion = definition.ContentVersion, ScenarioId = scenario.Id,
                Inventory = WorkshopCopy.Amounts(scenario.InitialInventory)
            };
            foreach (ItemAmount entry in result.Inventory) result.EverObtained.Add(entry.ItemId);
            CheckGoal(result, new List<WorkshopEvent>());
            string error = ValidateState(result);
            if (error != null) throw new ArgumentException(error, nameof(scenario));
            return result;
        }

        private static int Count(WorkshopSnapshot snapshot, string itemId)
        {
            if (itemId == null) return 0;
            foreach (ItemAmount amount in snapshot.Inventory)
                if (amount.ItemId == itemId) return amount.Quantity;
            return 0;
        }

        private bool IsBeingProduced(WorkshopSnapshot snapshot, string itemId)
        {
            foreach (WorkshopJob job in snapshot.Jobs)
                if (Contains(recipes[job.RecipeId].Outputs, itemId) ||
                    Contains(recipes[job.RecipeId].Byproducts, itemId)) return true;
            return false;
        }

        private static bool Contains(List<ItemAmount> amounts, string itemId)
        {
            foreach (ItemAmount amount in amounts) if (amount.ItemId == itemId) return true;
            return false;
        }

        private static bool Available(ScenarioData scenario, string recipeId)
        { return scenario.AvailableRecipeIds.Count == 0 || scenario.AvailableRecipeIds.Contains(recipeId); }

        private static bool Known(WorkshopSnapshot snapshot, ScenarioData scenario, RecipeDefinitionData recipe)
        {
            return scenario.KnownRecipeIds.Contains(recipe.Id) ||
                (!string.IsNullOrWhiteSpace(recipe.UnlockItemId) && snapshot.EverObtained.Contains(recipe.UnlockItemId));
        }

        private string ItemName(string itemId)
        { return items.TryGetValue(itemId, out ItemDefinitionData item) ? item.Name : itemId; }

        private void NotifyChanged()
        {
            Action<WorkshopSnapshot> observers = Changed;
            if (observers == null) return;
            foreach (Action<WorkshopSnapshot> observer in observers.GetInvocationList())
            {
                try { observer(WorkshopCopy.Snapshot(state)); }
                catch (Exception exception)
                {
                    Action<Exception> diagnostics = ObserverFailed;
                    if (diagnostics == null) continue;
                    foreach (Action<Exception> diagnostic in diagnostics.GetInvocationList())
                        try { diagnostic(exception); } catch (Exception) { }
                }
            }
        }

        private sealed class Rejected : Exception
        { internal Rejected(string message) : base(message) { } }
    }
}
