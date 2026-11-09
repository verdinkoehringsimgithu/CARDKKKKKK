using System.Collections.Generic;

namespace CardKKK.Island.Core
{
    internal static class IslandCopy
    {
        internal static List<NumberValue> Values(List<NumberValue> source)
        {
            var result = new List<NumberValue>();
            foreach (var value in source)
                result.Add(new NumberValue { Key = value.Key, Value = value.Value });
            return result;
        }

        private static List<CardAmount> Amounts(List<CardAmount> source)
        {
            var result = new List<CardAmount>();
            foreach (var value in source)
                result.Add(new CardAmount { DefinitionId = value.DefinitionId, Quantity = value.Quantity });
            return result;
        }

        internal static IslandDefinition Definition(IslandDefinition source)
        {
            var result = new IslandDefinition
            { ContentVersion = source.ContentVersion, MaximumInstances = source.MaximumInstances,
                MaximumEventBatchesPerAdvance = source.MaximumEventBatchesPerAdvance };
            foreach (var card in source.Cards)
                result.Cards.Add(new CardDefinition
                {
                    Id = card.Id, Name = card.Name, Kind = card.Kind,
                    Tags = new List<string>(card.Tags), InitialValues = Values(card.InitialValues),
                    StackLimit = card.StackLimit, Capacity = card.Capacity,
                    LifetimeMinutes = card.LifetimeMinutes, ExpiryOutputs = Amounts(card.ExpiryOutputs)
                });
            foreach (var recipe in source.Recipes)
            {
                var copy = new RecipeDefinition
                {
                    Id = recipe.Id, Name = recipe.Name, DurationMinutes = recipe.DurationMinutes,
                    Outputs = Amounts(recipe.Outputs)
                };
                foreach (var slot in recipe.Ingredients)
                    copy.Ingredients.Add(new Ingredient
                    {
                        SlotId = slot.SlotId, DefinitionId = slot.DefinitionId,
                        RequiredTags = new List<string>(slot.RequiredTags),
                        MinimumValues = Values(slot.MinimumValues), ValueChanges = Values(slot.ValueChanges),
                        Quantity = slot.Quantity, Consume = slot.Consume
                    });
                result.Recipes.Add(copy);
            }
            return result;
        }

        internal static IslandSnapshot Snapshot(IslandSnapshot source)
        {
            var result = new IslandSnapshot
            {
                SchemaVersion = source.SchemaVersion, ContentVersion = source.ContentVersion,
                Minute = source.Minute, Revision = source.Revision
            };
            foreach (var card in source.Cards)
                result.Cards.Add(new CardInstance
                {
                    Id = card.Id, DefinitionId = card.DefinitionId, RegionId = card.RegionId,
                    Quantity = card.Quantity, X = card.X, Y = card.Y,
                    ExpiresAt = card.ExpiresAt, Values = Values(card.Values)
                });
            foreach (var job in source.Jobs)
                result.Jobs.Add(new RecipeJob
                {
                    Id = job.Id, RecipeId = job.RecipeId, RegionId = job.RegionId,
                    FinishesAt = job.FinishesAt, LockedIds = new List<string>(job.LockedIds)
                });
            return result;
        }
    }
}
