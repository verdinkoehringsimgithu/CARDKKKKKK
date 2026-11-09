using System;
using System.Collections.Generic;

namespace CardKKK.Island.Core
{
    public enum CardKind { Material, Tool, Facility, Region, Organism }

    [Serializable]
    public sealed class NumberValue
    {
        public string Key;
        public int Value;
    }

    [Serializable]
    public sealed class CardDefinition
    {
        public string Id;
        public string Name;
        public CardKind Kind;
        public List<string> Tags = new List<string>();
        public List<NumberValue> InitialValues = new List<NumberValue>();
        public int StackLimit = 1;
        public int Capacity;
        public int LifetimeMinutes;
        public List<CardAmount> ExpiryOutputs = new List<CardAmount>();
    }

    [Serializable]
    public sealed class CardAmount
    {
        public string DefinitionId;
        public int Quantity = 1;
    }

    [Serializable]
    public sealed class Ingredient
    {
        public string SlotId;
        public string DefinitionId;
        public List<string> RequiredTags = new List<string>();
        public List<NumberValue> MinimumValues = new List<NumberValue>();
        public List<NumberValue> ValueChanges = new List<NumberValue>();
        public int Quantity = 1;
        public bool Consume;
    }

    [Serializable]
    public sealed class RecipeDefinition
    {
        public string Id;
        public string Name;
        public int DurationMinutes;
        public List<Ingredient> Ingredients = new List<Ingredient>();
        public List<CardAmount> Outputs = new List<CardAmount>();
    }

    [Serializable]
    public sealed class IslandDefinition
    {
        public string ContentVersion;
        public int MaximumInstances = 1000;
        public int MaximumEventBatchesPerAdvance = 10000;
        public List<CardDefinition> Cards = new List<CardDefinition>();
        public List<RecipeDefinition> Recipes = new List<RecipeDefinition>();
    }

    // Empty RegionId means the tabletop. Regions cannot contain other regions.
    [Serializable]
    public sealed class CardInstance
    {
        public string Id;
        public string DefinitionId;
        public string RegionId;
        public int Quantity = 1;
        public float X;
        public float Y;
        public long ExpiresAt;
        public List<NumberValue> Values = new List<NumberValue>();
    }

    [Serializable]
    public sealed class SlotBinding
    {
        public string SlotId;
        public string InstanceId;
    }

    [Serializable]
    public sealed class RecipeJob
    {
        public string Id;
        public string RecipeId;
        public string RegionId;
        public long FinishesAt;
        public List<string> LockedIds = new List<string>();
    }

    [Serializable]
    public sealed class IslandSnapshot
    {
        public int SchemaVersion = 1;
        public string ContentVersion;
        public long Minute;
        public int Revision;
        public List<CardInstance> Cards = new List<CardInstance>();
        public List<RecipeJob> Jobs = new List<RecipeJob>();
    }

    public sealed class IslandResult
    {
        public bool Success { get; }
        public string Error { get; }
        internal IslandResult(bool success, string error = "")
        { Success = success; Error = error; }
    }
}
