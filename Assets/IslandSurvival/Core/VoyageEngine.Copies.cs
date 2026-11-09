using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        private static VoyageState Copy(VoyageState source) => new VoyageState
        {
            Minute = source.Minute, NextCardId = source.NextCardId, NextJobId = source.NextJobId, RandomState = source.RandomState,
            Paused = source.Paused, Dead = source.Dead, Won = source.Won, EndReason = source.EndReason,
            Hunger = source.Hunger, San = source.San, Known = new List<string>(source.Known), Staged = new List<int>(source.Staged),
            Cards = source.Cards.Select(card => new VoyageCardState
            {
                Id = card.Id, CardId = card.CardId, Durability = card.Durability, Charges = card.Charges, RechargeRemaining = card.RechargeRemaining,
                TimerRemaining = card.TimerRemaining, Limb = card.Limb, JobId = card.JobId, X = card.X, Y = card.Y,
                ProducedByJobId = card.ProducedByJobId, ProducedFromX = card.ProducedFromX, ProducedFromY = card.ProducedFromY
            }).ToList(),
            Jobs = source.Jobs.Select(job => new VoyageJobState
            {
                Id = job.Id, RecipeId = job.RecipeId, Remaining = job.Remaining, Duration = job.Duration, Location = job.Location,
                AuxiliaryIds = new List<string>(job.AuxiliaryIds), Inputs = job.Inputs.Select(input => new VoyageJobInput
                {
                    CardId = input.CardId, Slot = input.Slot, AuxiliaryId = input.AuxiliaryId
                }).ToList()
            }).ToList()
        };

        private static VoyageDefinition Copy(VoyageDefinition source) => new VoyageDefinition
        {
            HungerPerMinute = source.HungerPerMinute, RealSecondsPerMinute = source.RealSecondsPerMinute, Seed = source.Seed,
            BirdStep = source.BirdStep, BirdRange = source.BirdRange, BirdReach = source.BirdReach,
            Cards = source.Cards.Select(card => new VoyageCardRule
            {
                Id = card.Id, Name = card.Name, Description = card.Description, SortRank = card.SortRank, Kind = card.Kind,
                Durability = card.Durability, Charges = card.Charges, RechargeMinutes = card.RechargeMinutes,
                TimerMinutes = card.TimerMinutes, TransformId = card.TransformId, SeekRecipeId = card.SeekRecipeId,
                InjuryLevel = card.InjuryLevel, NextInjuryId = card.NextInjuryId, LimbId = card.LimbId, LimbDamage = card.LimbDamage,
                SpawnHunger = card.SpawnHunger, SpawnSan = card.SpawnSan,
                Tags = new List<string>(card.Tags), GrantsRecipes = new List<string>(card.GrantsRecipes)
            }).ToList(),
            Recipes = source.Recipes.Select(recipe => new VoyageRecipeRule
            {
                Id = recipe.Id, Name = recipe.Name, Tag = recipe.Tag, LocationId = recipe.LocationId, LocationWear = recipe.LocationWear,
                LocationCharges = recipe.LocationCharges, Minutes = recipe.Minutes, DiscoveryCardId = recipe.DiscoveryCardId,
                InitiallyKnown = recipe.InitiallyKnown, Automatic = recipe.Automatic, HungerDelta = recipe.HungerDelta, SanDelta = recipe.SanDelta,
                Inputs = recipe.Inputs.Select(input => new VoyageInputRule
                {
                    CardId = input.CardId, Tag = input.Tag, Quantity = input.Quantity, IsConsumed = input.IsConsumed,
                    MatchLimb = input.MatchLimb, DurabilityCost = input.DurabilityCost, DurabilityRestore = input.DurabilityRestore,
                    ChargesRestore = input.ChargesRestore
                }).ToList(),
                Outputs = recipe.Outputs.Select(Copy).ToList()
            }).ToList(),
            AuxiliaryLines = source.AuxiliaryLines.Select(line => new VoyageAuxiliaryRule
            {
                Id = line.Id, CardId = line.CardId, RecipeTag = line.RecipeTag, RequiredInputId = line.RequiredInputId,
                DurabilityCost = line.DurabilityCost, MinutesDelta = line.MinutesDelta, OutputMultiplier = line.OutputMultiplier,
                SanDelta = line.SanDelta, Outputs = line.Outputs.Select(Copy).ToList()
            }).ToList(),
            StartCards = source.StartCards.Select(start => new VoyageStartCard { CardId = start.CardId, X = start.X, Y = start.Y }).ToList()
        };

        private static VoyageOutputRule Copy(VoyageOutputRule output) => new VoyageOutputRule
        {
            CardId = output.CardId, Group = output.Group, Chance = output.Chance, Quantity = output.Quantity, Unique = output.Unique
        };
    }
}
