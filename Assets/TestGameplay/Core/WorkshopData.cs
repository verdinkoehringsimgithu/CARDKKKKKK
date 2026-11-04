using System;
using System.Collections.Generic;

namespace CardKKK.TestGameplay.Core
{
    [Serializable]
    public sealed class ItemAmount
    {
        public string ItemId;
        public int Quantity;
        public ItemAmount() { }
        public ItemAmount(string itemId, int quantity) { ItemId = itemId; Quantity = quantity; }
    }

    [Serializable]
    public sealed class ItemDefinitionData
    {
        public string Id;
        public string Name;
    }

    [Serializable]
    public sealed class RecipeDefinitionData
    {
        public string Id;
        public string Name;
        public string FacilityId;
        public string UnlockItemId;
        public string RequiredToolId;
        public List<ItemAmount> Inputs = new List<ItemAmount>();
        public List<ItemAmount> Outputs = new List<ItemAmount>();
        public List<ItemAmount> Byproducts = new List<ItemAmount>();
        public int PreparationMinutes;
        public int ProcessMinutes;
        public bool InstallLining;
        public int LinedCharcoalCost = -1;
        public bool UniqueOutput;
    }

    [Serializable]
    public sealed class GatherDefinitionData
    {
        public string Id;
        public string Name;
        public string SiteId;
        public int Minutes;
        public int MinutesWithAxe = -1;
        public List<ItemAmount> Outputs = new List<ItemAmount>();
    }

    [Serializable]
    public sealed class ScenarioData
    {
        public string Id;
        public string Name;
        public List<ItemAmount> InitialInventory = new List<ItemAmount>();
        public List<string> KnownRecipeIds = new List<string>();
        // 空列表表示全部工艺；灰盒场景显式列出五条工艺。
        public List<string> AvailableRecipeIds = new List<string>();
        public string GoalItemId;
        public int GoalQuantity;
        public bool RequireAxeGather;
    }

    [Serializable]
    public sealed class WorkshopDefinition
    {
        public string ContentVersion;
        public List<ItemDefinitionData> Items = new List<ItemDefinitionData>();
        public List<RecipeDefinitionData> Recipes = new List<RecipeDefinitionData>();
        public List<GatherDefinitionData> Gathers = new List<GatherDefinitionData>();
        public List<ScenarioData> Scenarios = new List<ScenarioData>();
    }

    [Serializable]
    public sealed class WorkshopJob
    {
        public string BatchId;
        public string RecipeId;
        public string FacilityId;
        public int StartedMinute;
        public int FinishMinute;
    }

    [Serializable]
    public sealed class WorkshopSnapshot
    {
        public int Version = 1;
        public string ContentVersion;
        public string ScenarioId;
        public int Minutes;
        public int Revision;
        public bool KilnLined;
        public List<ItemAmount> Inventory = new List<ItemAmount>();
        public List<string> EverObtained = new List<string>();
        public List<WorkshopJob> Jobs = new List<WorkshopJob>();
        public int WoodSpent;
        public int AxeGatherCount;
        public bool GoalComplete;
        public int CompletionMinutes;
        public int CompletionWoodSpent;
    }

    public enum WorkshopEventKind
    {
        RecipeStarted,
        RecipeCompleted,
        GatherCompleted,
        LiningInstalled,
        GoalCompleted
    }

    [Serializable]
    public sealed class WorkshopEvent
    {
        public WorkshopEventKind Kind;
        public string RecipeId;
        public string FacilityId;
        public int Minute;
        public List<ItemAmount> Items = new List<ItemAmount>();
    }

    public sealed class RecipeOffer
    {
        public string RecipeId { get; internal set; }
        public bool IsKnown { get; internal set; }
        public bool CanStart { get; internal set; }
        public string Reason { get; internal set; }
        public List<ItemAmount> EffectiveInputs { get; internal set; }
        public int PreparationMinutes { get; internal set; }
        public int ProcessMinutes { get; internal set; }
    }

    public sealed class WorkshopResult
    {
        public bool Success { get; }
        public string Error { get; }
        public bool GoalJustCompleted { get; }
        public IReadOnlyList<WorkshopEvent> Events { get; }

        internal WorkshopResult(bool success, string error, bool goalJustCompleted,
            List<WorkshopEvent> events)
        {
            Success = success;
            Error = error ?? "";
            GoalJustCompleted = goalJustCompleted;
            Events = (events ?? new List<WorkshopEvent>()).AsReadOnly();
        }

        internal static WorkshopResult Fail(string error)
        { return new WorkshopResult(false, error, false, null); }
    }

    internal static class WorkshopCopy
    {
        internal static List<ItemAmount> Amounts(List<ItemAmount> values)
        {
            if (values == null) return null;
            var result = new List<ItemAmount>(values.Count);
            foreach (ItemAmount value in values)
                result.Add(value == null ? null : new ItemAmount(value.ItemId, value.Quantity));
            return result;
        }

        internal static WorkshopDefinition Definition(WorkshopDefinition source)
        {
            if (source == null) return null;
            var result = new WorkshopDefinition { ContentVersion = source.ContentVersion };
            if (source.Items == null) result.Items = null;
            else foreach (ItemDefinitionData item in source.Items)
                result.Items.Add(item == null ? null : new ItemDefinitionData { Id = item.Id, Name = item.Name });
            if (source.Recipes == null) result.Recipes = null;
            else foreach (RecipeDefinitionData recipe in source.Recipes)
                result.Recipes.Add(recipe == null ? null : new RecipeDefinitionData
                {
                    Id = recipe.Id, Name = recipe.Name, FacilityId = recipe.FacilityId,
                    UnlockItemId = recipe.UnlockItemId, RequiredToolId = recipe.RequiredToolId,
                    Inputs = Amounts(recipe.Inputs), Outputs = Amounts(recipe.Outputs),
                    Byproducts = Amounts(recipe.Byproducts), PreparationMinutes = recipe.PreparationMinutes,
                    ProcessMinutes = recipe.ProcessMinutes, InstallLining = recipe.InstallLining,
                    LinedCharcoalCost = recipe.LinedCharcoalCost, UniqueOutput = recipe.UniqueOutput
                });
            if (source.Gathers == null) result.Gathers = null;
            else foreach (GatherDefinitionData gather in source.Gathers)
                result.Gathers.Add(gather == null ? null : new GatherDefinitionData
                {
                    Id = gather.Id, Name = gather.Name, SiteId = gather.SiteId,
                    Minutes = gather.Minutes, MinutesWithAxe = gather.MinutesWithAxe,
                    Outputs = Amounts(gather.Outputs)
                });
            if (source.Scenarios == null) result.Scenarios = null;
            else foreach (ScenarioData scenario in source.Scenarios)
                result.Scenarios.Add(scenario == null ? null : new ScenarioData
                {
                    Id = scenario.Id, Name = scenario.Name, InitialInventory = Amounts(scenario.InitialInventory),
                    KnownRecipeIds = scenario.KnownRecipeIds == null ? null : new List<string>(scenario.KnownRecipeIds),
                    AvailableRecipeIds = scenario.AvailableRecipeIds == null ? null : new List<string>(scenario.AvailableRecipeIds),
                    GoalItemId = scenario.GoalItemId, GoalQuantity = scenario.GoalQuantity,
                    RequireAxeGather = scenario.RequireAxeGather
                });
            return result;
        }

        internal static WorkshopSnapshot Snapshot(WorkshopSnapshot source)
        {
            if (source == null) return null;
            var result = new WorkshopSnapshot
            {
                Version = source.Version, ContentVersion = source.ContentVersion, ScenarioId = source.ScenarioId,
                Minutes = source.Minutes, Revision = source.Revision, KilnLined = source.KilnLined,
                Inventory = Amounts(source.Inventory),
                EverObtained = source.EverObtained == null ? null : new List<string>(source.EverObtained),
                WoodSpent = source.WoodSpent, AxeGatherCount = source.AxeGatherCount,
                GoalComplete = source.GoalComplete, CompletionMinutes = source.CompletionMinutes,
                CompletionWoodSpent = source.CompletionWoodSpent
            };
            if (source.Jobs == null) result.Jobs = null;
            else foreach (WorkshopJob job in source.Jobs)
                result.Jobs.Add(job == null ? null : new WorkshopJob
                {
                    BatchId = job.BatchId, RecipeId = job.RecipeId, FacilityId = job.FacilityId,
                    StartedMinute = job.StartedMinute, FinishMinute = job.FinishMinute
                });
            return result;
        }
    }
}
