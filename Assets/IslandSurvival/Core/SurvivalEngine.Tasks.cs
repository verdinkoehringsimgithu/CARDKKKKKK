using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        // 先计已参与的劳动，再演化身体、判断死亡；完成交付仍在TickTasks阶段。
        private void TickTaskWork(SurvivalState s)
        {
            foreach (string id in s.Tasks.Where(t => t.Human && !t.Suspended).OrderBy(t => t.Id, StringComparer.Ordinal).Select(t => t.Id).ToArray())
            {
                TaskState task = s.Tasks.Find(t => t.Id == id);
                if (task == null || task.StartedAt >= s.Minute || task.Progress >= 1) continue;
                RecipeRule recipe = Recipe(task.RecipeId);
                if (recipe.Kind != RecipeKind.Rest && s.Body.Mind <= 0) { s.Paused = true; continue; }
                double efficiency = recipe.Kind == RecipeKind.Rest ? 1 : TaskEfficiency(s, task, recipe);
                double minutes = Math.Min(1, Math.Max(0, (1 - task.Progress) * task.WorkMinutes / efficiency));
                task.Progress = Math.Min(1, task.Progress + efficiency * minutes / task.WorkMinutes);
                ApplyWork(s, recipe, minutes / 60.0, recipe.Name);
            }
        }

        private void TickTasks(SurvivalState s)
        {
            foreach (TaskState queued in s.Tasks.Where(t => t.QueueWounds.Any(w => !s.Body.Injuries.Any(i => i.Id == w))))
            {
                queued.QueueWounds.Clear();
                AddLog(s, "护理队列中有伤口已经恢复，未开始的队列已停止；当前处理继续。");
            }
            // ID快照避免完成后的下一周期在本分钟再推进；人工先于自动完成。
            string[] ids = s.Tasks.OrderByDescending(t => t.Human).ThenBy(t => t.Id, StringComparer.Ordinal).Select(t => t.Id).ToArray();
            foreach (string id in ids)
            {
                TaskState task = s.Tasks.Find(t => t.Id == id);
                if (task == null || task.Suspended || task.StartedAt >= s.Minute) continue;
                RecipeRule recipe = Recipe(task.RecipeId);
                if (!task.Human)
                {
                    double factor = recipe.Environmental ? EnvironmentEfficiency(s, InputOwner(s, Item(s, task.TargetId))) : 1;
                    task.Progress = Math.Min(1, task.Progress + factor / task.WorkMinutes);
                }
                if (task.Progress >= 1 - 0.0000000001) FinishTask(s, task);
            }

            foreach (string id in s.Items.Where(i => i.Repeat && !string.IsNullOrEmpty(i.RepeatRecipe)).OrderBy(i => i.Id, StringComparer.Ordinal).Select(i => i.Id).ToArray())
            {
                ItemState owner = Item(s, id);
                if (owner == null || IsLocked(s, id)) continue;
                RecipeRule recipe = Recipe(owner.RepeatRecipe);
                if (recipe == null || recipe.Human) continue;
                SurvivalResult result = BeginRecipe(s, recipe.Id, id, owner.RepeatSecondaryId);
                owner = Item(s, id);
                if (owner != null) owner.Status = result.Success ? "" : result.Error;
            }
        }

        private void FinishTask(SurvivalState s, TaskState task)
        {
            RecipeRule recipe = Recipe(task.RecipeId);
            if (recipe.AutomaticMinutes > 0 && task.Stage == 0)
            {
                task.Stage = 1; task.Human = false; task.WorkMinutes = recipe.AutomaticMinutes;
                task.Progress = 0; task.StartedAt = s.Minute;
                AddLog(s, recipe.Name + "的设置已完成，后续自动进行。");
                return;
            }
            ItemState target = Item(s, task.TargetId);
            CardRule targetRule = target == null ? null : Card(target.DefinitionId);
            if (recipe.Kind == RecipeKind.Repair && target != null)
            {
                if (targetRule.Kind == CardKind.Tool)
                {
                    target.MaxDurability -= targetRule.RepairLoss;
                    target.Durability = target.MaxDurability; target.Status = "";
                }
                else { target.Structure = Math.Min(targetRule.Structure, target.Structure + (int)Math.Round(recipe.RepairAmount)); target.Status = ""; }
            }
            if (recipe.Kind == RecipeKind.Expand)
            {
                ExpansionRule expansion = def.Expansions[s.ExpansionLevel];
                s.Capacity = expansion.Capacity; s.ExpansionLevel++;
            }
            if (recipe.Kind == RecipeKind.Gather && target != null && !recipe.ForcedGather && !s.GatheredNodes.Contains(target.Id))
                s.GatheredNodes.Add(target.Id);
            if ((recipe.Kind == RecipeKind.Salvage || recipe.Kind == RecipeKind.Demolish || (recipe.Kind == RecipeKind.Destroy && recipe.Permanent)) && target != null)
                s.Items.Remove(target);

            Reservation[] produced = s.Reservations.Where(r => r.TaskId == task.Id).ToArray();
            double nutrition = produced.Where(r => Card(r.DefinitionId).CountsFood).Sum(r => Card(r.DefinitionId).Nutrition * r.Count);
            double added = Math.Max(0, nutrition - task.ConsumedNutrition);
            if (added > 0 && nutrition > 0)
            {
                foreach (var group in produced.Where(r => Card(r.DefinitionId).CountsFood && Card(r.DefinitionId).Nutrition > 0).GroupBy(r => Card(r.DefinitionId).FoodSource))
                {
                    double fraction = group.Sum(r => Card(r.DefinitionId).Nutrition * r.Count) / nutrition;
                    RecordFood(s, added * fraction, group.Key);
                }
            }
            bool dressingProduced = produced.Any(r => def.Recipes.Any(c => c.Care == CareKind.Dressing && c.Inputs.Any(i => i.Alternatives.Contains(r.DefinitionId))));
            if (dressingProduced) Mark(s, "care_ready");
            if (recipe.Id == def.Goal.CompostRecipeId || recipe.Id == def.Goal.AcceleratedCompostId) Today(s).Compost++;
            DeliverReservations(s, task);
            FinishSpecial(s, task, recipe);
            s.Tasks.Remove(task);
            if (recipe.Kind == RecipeKind.Rest) foreach (TaskState paused in s.Tasks.Where(t => t.Suspended)) paused.Suspended = false;
            RemoveEmpty(s);
            AddLog(s, recipe.Name + "完成。");
            ItemState preferred = Item(s, task.PreferredItemId);
            string preferredId = preferred != null && preferred.Quantity > 0 && preferred.DefinitionId == task.PreferredMaterialId ? preferred.Id : "";

            if (task.QueueWounds.Count > 0)
            {
                string queue = string.Join(",", task.QueueWounds);
                string nextWound = task.QueueWounds[0];
                ItemState nextCard = s.Items.Find(i => Card(i.DefinitionId).Kind == CardKind.Condition && Card(i.DefinitionId).WoundCard && i.ConditionId == nextWound);
                SurvivalResult result = nextCard == null ? SurvivalResult.Fail("下一处伤口已无对应状态卡。") :
                    BeginRecipe(s, recipe.Id, nextCard.Id, task.SecondaryId, queue, 1, task.TraitId, preferredId, task.PreferredMaterialId);
                if (!result.Success) AddLog(s, "护理队列停在当前伤口：" + result.Error);
            }
            else if (task.Remaining > 1)
            {
                SurvivalResult result = BeginRecipe(s, recipe.Id, task.TargetId, task.SecondaryId, task.WoundId, task.Remaining - 1, task.TraitId, preferredId, task.PreferredMaterialId);
                if (!result.Success) AddLog(s, "后续批次已停止：" + result.Error);
            }
        }

        private void CancelStartedTask(SurvivalState s, TaskState task)
        {
            s.Reservations.RemoveAll(r => r.TaskId == task.Id);
            s.Tasks.Remove(task);
            if (Recipe(task.RecipeId).Kind == RecipeKind.Rest) foreach (TaskState paused in s.Tasks.Where(t => t.Suspended)) paused.Suspended = false;
            RemoveEmpty(s);
        }
    }
}
