using System;
using System.Collections.Generic;

namespace CardKKK.IslandSurvival.Core
{
    public enum VoyageCardKind { Body, Resource, Facility, Creature, Material, Food, Tool, Injury, Thought, Information, Boat, Ending }
    public enum VoyageMatch { None, Incomplete, Ready, Unknown }

    [Serializable]
    public sealed class VoyageDefinition
    {
        public List<VoyageCardRule> Cards = new List<VoyageCardRule>();
        public List<VoyageRecipeRule> Recipes = new List<VoyageRecipeRule>();
        public List<VoyageAuxiliaryRule> AuxiliaryLines = new List<VoyageAuxiliaryRule>();
        public List<VoyageStartCard> StartCards = new List<VoyageStartCard>();
        public double HungerPerMinute, RealSecondsPerMinute = .5;
        public int Seed = 1;
        public float BirdStep, BirdRange, BirdReach;
    }

    [Serializable]
    public sealed class VoyageCardRule
    {
        public string Id = "", Name = "", Description = "", TransformId = "", NextInjuryId = "", LimbId = "", SeekRecipeId = "";
        public VoyageCardKind Kind;
        public int SortRank, Durability, Charges, RechargeMinutes, TimerMinutes, InjuryLevel, LimbDamage;
        public List<string> Tags = new List<string>();
        public List<string> GrantsRecipes = new List<string>();
        public double SpawnHunger, SpawnSan;
    }

    [Serializable]
    public sealed class VoyageStartCard
    {
        public string CardId = "";
        public float X, Y;
    }

    [Serializable]
    public sealed class VoyageRecipeRule
    {
        public string Id = "", Name = "", LocationId = "", DiscoveryCardId = "", Tag = "";
        public int LocationWear, LocationCharges, Minutes = 1;
        public bool InitiallyKnown, Automatic;
        public List<VoyageInputRule> Inputs = new List<VoyageInputRule>();
        public List<VoyageOutputRule> Outputs = new List<VoyageOutputRule>();
        public double HungerDelta, SanDelta;
    }

    [Serializable]
    public sealed class VoyageInputRule
    {
        public string CardId = "", Tag = "";
        public int Quantity = 1, DurabilityCost, DurabilityRestore, ChargesRestore;
        public bool IsConsumed = true, MatchLimb;
    }

    [Serializable]
    public sealed class VoyageOutputRule
    {
        public string CardId = "", Group = "";
        public int Quantity = 1;
        public double Chance = 1;
        public bool Unique;
    }

    [Serializable]
    public sealed class VoyageAuxiliaryRule
    {
        public string Id = "", CardId = "", RecipeTag = "", RequiredInputId = "";
        public int DurabilityCost, MinutesDelta;
        public double OutputMultiplier = 1, SanDelta;
        public List<VoyageOutputRule> Outputs = new List<VoyageOutputRule>();
    }

    [Serializable]
    public sealed class VoyageState
    {
        public List<VoyageCardState> Cards = new List<VoyageCardState>();
        public List<VoyageJobState> Jobs = new List<VoyageJobState>();
        public List<int> Staged = new List<int>();
        public List<string> Known = new List<string>();
        public int Minute, NextCardId = 1, NextJobId = 1, RandomState;
        public bool Paused = true, Dead, Won;
        public string EndReason = "";
        public double Hunger = 100, San = 100;
    }

    [Serializable]
    public sealed class VoyageCardState
    {
        // 仅供已提交快照的出牌表现使用，不写入存档或规则指纹。
        [NonSerialized] public int ProducedByJobId;
        [NonSerialized] public float ProducedFromX, ProducedFromY;
        public int Id, Durability, Charges, RechargeRemaining, TimerRemaining, Limb, JobId;
        public string CardId = "";
        public float X, Y;
    }

    [Serializable]
    public sealed class VoyageJobState
    {
        public int Id, Remaining, Duration, Location;
        public string RecipeId = "";
        public List<VoyageJobInput> Inputs = new List<VoyageJobInput>();
        public List<string> AuxiliaryIds = new List<string>();
    }

    [Serializable]
    public sealed class VoyageJobInput
    {
        public int CardId, Slot;
        public string AuxiliaryId = "";
    }

    public sealed class VoyageOffer
    {
        public VoyageMatch Match;
        public string Name = "", Detail = "";
        public List<int> RelatedCardIds = new List<int>();
    }

    public sealed class VoyageResult
    {
        public bool Success { get; }
        public bool Changed { get; }
        public string Message { get; }

        public VoyageResult(bool success, string message = "", bool changed = true)
        {
            Success = success;
            Changed = success && changed;
            Message = message;
        }
    }
}
