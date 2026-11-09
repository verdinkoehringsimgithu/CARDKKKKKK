using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        public double ProductionWidth => GridColumns(state) * def.CellWidth;
        public double ProductionHeight => GridRows(state) * def.CellHeight;
        public List<string> KnownRecipeIds(string targetId = null)
        {
            ItemState target = targetId == null ? null : Item(state, targetId);
            return def.Recipes.Where(r => RecipeKnown(state, r) && (targetId == null || target != null && QueryMatches(r, target))).Select(r => r.Id).ToList();
        }
        public List<ItemState> RecipeTargets(string recipeId)
        {
            RecipeRule recipe = Recipe(recipeId);
            return recipe == null ? new List<ItemState>() : SurvivalCopies.Copy(state.Items.Where(i => QueryMatches(recipe, i)).ToList());
        }
        private bool QueryMatches(RecipeRule recipe, ItemState target)
        {
            if (Card(target.DefinitionId).Kind == CardKind.Condition && recipe.Kind != RecipeKind.Care && recipe.Kind != RecipeKind.Clean) return false;
            if (!recipe.TargetRequired && recipe.TargetDefinitions.Count == 0 && recipe.RequiredRole == CardRole.None)
                return recipe.Inputs.Any(i => i.Alternatives.Contains(target.DefinitionId)) || !string.IsNullOrEmpty(recipe.RequiredAbility) && recipe.RequiredAbility == Card(target.DefinitionId).Ability;
            return TargetMatches(recipe, target);
        }
        public double ProductionFactor(string itemId) => RegionFactor(state, Item(state, itemId));
        public List<ItemState> SupplyCandidates(string ownerId)
        {
            ItemState owner = Item(state, ownerId);
            return owner == null ? new List<ItemState>() : SurvivalCopies.Copy(state.Items.Where(i => i.OwnerId != ownerId && i.Quantity > 0 && !IsLocked(state, i.Id) && Accepts(owner, Card(i.DefinitionId), Place.Input)).ToList());
        }
        public double CurrentEfficiency(string recipeId)
        { RecipeRule recipe = Recipe(recipeId); return recipe == null ? 1 : WorkEfficiency(state, recipe); }
        public double RemainingMinutes(string taskId)
        {
            TaskState task = state.Tasks.Find(t => t.Id == taskId); if (task == null) return 0;
            RecipeRule recipe = Recipe(task.RecipeId);
            double factor = task.Human ? recipe.Kind == RecipeKind.Rest ? 1 : TaskEfficiency(state, task, recipe) : recipe.Environmental ? EnvironmentEfficiency(state, InputOwner(state, Item(state, task.TargetId))) : 1;
            return Math.Max(0, (1 - task.Progress) * task.WorkMinutes / Math.Max(.000001, factor)) + (task.Stage == 0 ? recipe.AutomaticMinutes : 0);
        }
    }
}
