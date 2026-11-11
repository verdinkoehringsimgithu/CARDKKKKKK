using System;
using System.Collections.Generic;

namespace CardKKK.IslandSurvival.Core
{
    public enum VoyageCardKind { Body, Resource, Facility, Creature, Material, Food, Tool, Injury, Thought, Information, Boat, Ending }
    public enum VoyageMatch { None, Incomplete, Ready, Unknown }

    [Serializable]
    public sealed class VoyageDefinition
    {
        public VoyageDeskRules Desk = new VoyageDeskRules();
        public List<VoyageCardRule> Cards = new List<VoyageCardRule>();
        public List<VoyageRecipeRule> Recipes = new List<VoyageRecipeRule>();
        public List<VoyageAuxiliaryRule> AuxiliaryLines = new List<VoyageAuxiliaryRule>();
        public List<VoyageStartCard> StartCards = new List<VoyageStartCard>();
        public double HungerPerMinute, RealSecondsPerMinute = .5;
        public int Seed = 1;
        public float BirdStep, BirdRange, BirdReach;
    }

    [Serializable]
    public sealed class VoyageDeskRules
    {
        public int MaxStackSize = 20, StackMode;
        public bool AllowStacking = true, AllowWholePileDrag = true, AllowSingleCardDrag = true;
        public bool AutoStackOnSpawn = true;
        public float StackOffset = 22, SnapDistance = 65;
        public float Width = 1600, Height = 900, CardWidth = 100, CardHeight = 140, Gap = 12;
    }

    // Measured display geometry is supplied at the Unity boundary; it is not a gameplay rule or saved animation state.
    public sealed class VoyageDeskGeometry
    {
        public readonly float Width, Height, CardWidth, CardHeight;
        public VoyageDeskGeometry(float width, float height, float cardWidth, float cardHeight)
        { Width = width; Height = height; CardWidth = cardWidth; CardHeight = cardHeight; }
    }

    [Serializable]
    public sealed class VoyageCardRule
    {
        public string Id = "", Name = "", Description = "", TransformId = "", NextInjuryId = "", LimbId = "", SeekRecipeId = "", Family = "";
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
        public int DeskVersion;
        public List<VoyageWorkspace> Workspaces = new List<VoyageWorkspace>();
        public List<VoyageCardState> Cards = new List<VoyageCardState>();
        public List<VoyageJobState> Jobs = new List<VoyageJobState>();
        public List<int> Staged = new List<int>();
        public int PreparationLocation;
        public List<VoyagePreparation> Preparations = new List<VoyagePreparation>();
        public List<string> Known = new List<string>();
        public int Minute, NextCardId = 1, NextJobId = 1, RandomState;
        public bool Paused = true, Dead, Won;
        public string EndReason = "";
        public double Hunger = 100, San = 100;
    }

    [Serializable]
    public sealed class VoyageWorkspace
    {
        public int Location;
        public List<VoyageSlotState> Slots = new List<VoyageSlotState>();
    }

    [Serializable]
    public sealed class VoyageSlotState
    {
        public int Index, CardId;
    }

    public sealed class VoyageSlotOffer
    {
        public int Location, Index, CardId;
        public string Label = "";
        public bool Locked;
    }

    [Serializable]
    public sealed class VoyagePreparation
    {
        public int Location;
        public List<int> Cards = new List<int>();
    }

    [Serializable]
    public sealed class VoyageCardState
    {
        // 仅供已提交快照的出牌表现使用，不写入存档或规则指纹。
        [NonSerialized] public int ProducedByJobId;
        [NonSerialized] public float ProducedFromX, ProducedFromY;
        public int Id, Durability, Charges, RechargeRemaining, TimerRemaining, Limb, JobId, StackParent;
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
