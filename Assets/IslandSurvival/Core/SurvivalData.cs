using System;
using System.Collections.Generic;

namespace CardKKK.IslandSurvival.Core
{
    public enum CardKind { Material, Tool, Facility, Area, Creature, Condition }
    public enum CardRole { None, NaturalNode, Fire, Garden, Pen, Compost, Storage, DryingRack, AloePlant, YamPlant, AdultSnail, YoungSnail, Ornament }
    public enum Place { Table, Input, Output, Plant, Adult, Young, Storage, Equipment, Residue }
    public enum RecipeKind { Craft, Gather, Destroy, Repair, Salvage, Demolish, Rest, Plant, Harvest, Divide, Breed, SelectSeed, Care, Clean, Expand }
    [Flags] public enum WorkTag { None = 0, Hand = 1, Heavy = 2, Collect = 4, Cut = 8, Care = 16, Rest = 32 }
    public enum CareKind { None, Dressing, Gel, Leaf, Clean }
    public enum AfflictionKind { Bleeding, Infection, Pain, Irritation, Hunger }

    [Serializable] public sealed class Amount
    {
        public string DefinitionId;
        public int Count = 1;
        public string TraitId;
        public Amount() { }
        public Amount(string id, int count) { DefinitionId = id; Count = count; }
    }
    [Serializable] public sealed class Ingredient
    {
        public List<string> Alternatives = new List<string>();
        public int Count = 1;
        public bool Consumed = true;
    }
    [Serializable] public sealed class CardRule
    {
        public string Id, Name, Description, Family;
        public int SortRank;
        public CardKind Kind;
        public CardRole Role;
        public int StackLimit = 20, Footprint = 1;
        public double Nutrition, ShelfMinutes, FuelValue, OrganicWeight;
        public string FoodSource, DecayId;
        public bool CountsFood;
        public string Ability;
        public string ConditionRuleId;
        public bool WoundCard;
        public string DepletedStatus;
        public int Hardness, InitialDurability;
        public bool Repairable;
        public int RepairLoss;
        public double RepairMinutes;
        public string RepairToolId;
        public List<Amount> Salvage = new List<Amount>();
        public List<Amount> BuildingCost = new List<Amount>();
        public int Structure, DailyWear, WorkWear, InputSlots = 1, OutputSlots = 4, PlantSlots, AdultSlots, YoungSlots, StorageSlots;
        public double FuelCapacity, AshEveryFuel, CarryingCapacity = 8, LoadPower = 2;
        public string AshId;
        public double SpecialtyEfficiency = 1;
        public WorkTag SpecialtyTags;
        public double OrnamentRestBonus;
        public double CareStrength, CareDuration;
    }
    [Serializable] public sealed class RecipeRule
    {
        public string Id, Name, Description;
        public RecipeKind Kind;
        public List<Ingredient> Inputs = new List<Ingredient>();
        public List<Amount> Outputs = new List<Amount>();
        public List<string> TargetDefinitions = new List<string>();
        public CardRole RequiredRole, SecondaryRole;
        public string RequiredAbility;
        public int RequiredHardness, ToolWear;
        public double Minutes = 60, Physical = 1, Cognitive = 0.3, Danger, Fuel, Exposure;
        public double AutomaticMinutes, ExtraTraitMinutes;
        public WorkTag Tags = WorkTag.Hand;
        public bool Human = true, Environmental, InitialKnown = true, TargetRequired, Permanent, RequiresCareMilestone;
        public bool AllowPartialInputs;
        public bool ForcedGather;
        public double RepairAmount = 10;
        public string DiscoverMaterial, DiscoverMilestone;
        public CareKind Care;
        public double CareStrength, CareDuration;
        public List<Amount> ExtraTraitCost = new List<Amount>();
    }
    [Serializable] public sealed class NodeRule
    {
        public string CardId;
        public List<Amount> Initial = new List<Amount>(), Daily = new List<Amount>(), Capacity = new List<Amount>();
        public double RefreshMinutes = 1440;
        public bool Tide;
    }
    [Serializable] public sealed class PeriodicDrop
    {
        public int EveryDays = 1;
        public List<Amount> Items = new List<Amount>();
    }
    [Serializable] public sealed class TraitRule
    {
        public string Id, Name;
        public CardRole Species;
        public double DurationMultiplier = 1;
        public List<Amount> AddedOutputs = new List<Amount>();
    }
    [Serializable] public sealed class BiologyRule
    {
        public CardRole Role;
        public string MatureId, SeedId;
        public double GrowMinutes, ProductionMinutes, MatureHoldMinutes, FeedWindowMinutes = 1440, DivisionCooldownMinutes;
        public List<Amount> Feed = new List<Amount>(), Production = new List<Amount>(), Death = new List<Amount>();
        public double HealthGain = 10, HealthLoss = 25;
        public double BreedMinimumHealth = 60;
        public int StoredLimit = 2;
        public string ProductionItem;
    }
    [Serializable] public sealed class AfflictionRule
    {
        public string Id, Name, Description;
        public AfflictionKind Kind;
        public double Growth, Clearance, FunctionExponent = 1;
        public double StructureWeight, FunctionWeight, VitalWeight;
        public double StructureThreshold, FunctionThreshold, VitalThreshold;
        public double CurvePower = 2, CognitiveMultiplier = 1;
        public bool WoundRelated;
        public WorkTag WorkTags;
    }
    [Serializable] public sealed class BodyRules
    {
        public double HealthWeight = 0.5, FoodRate = 0.035, MindRate = 0.015;
        public double PhysicalPower = 1.2, CognitivePower = 1.1, DangerPower = 1.2, DangerMindWeight = 0.3;
        public double StructureEfficiency = 1, FunctionEfficiency = 0.7, MindEfficiency = 0.6, OtherEfficiency = 0.4;
        public double EfficiencyMin = 0.2, EfficiencyMax = 2, EfficiencyPower = 2;
        public double RepairRate = 0.025, StructureDamageRate = 0.035, NutritionHalf = 0.3;
        public double FunctionFloor = 0.5, FunctionStructurePower = 1, WorsenHours = 8, RecoverHours = 12;
        public double VitalRecovery = 0.02, VitalDamage = 0.09, RestRate = 0.35;
        public double HungerInput = 0.8, PollutionHalf = 8, ExposureScale = 0.02;
        public double RiskRate = 0.03, ForcedInjuryChance = 0.12, InjuryStructure = 0.08, InjuryVital = 0.02;
        public double GelStrength = 2, LeafStrength = 1.2, CleanAmount = 0.65, DirtRate = 0.01;
        public double SafeEventMinutes = 360, FoodWarning = 0.25, VitalWarning = 0.3;
        public double TreatmentMax = 4, TreatmentHalf = 2, ExposureWarning = 0.5;
    }
    [Serializable] public sealed class GoalRules
    {
        public int MinimumDay = 31, WindowDays = 3, CompostCount = 3;
        public double MinimumHealth = 0.6, MinimumVital = 0.6, MinimumMind = 0.4;
        public double DailyFood = 100, StoredFood = 200, MinimumShelfMinutes = 720;
        public int SourceCount = 2, MaxWaste = 8;
        public string CompostRecipeId = "R18", AcceleratedCompostId = "R19", BoneKnifeId = "bone_knife", WasteId = "organic";
    }
    [Serializable] public sealed class StartItem
    {
        public string DefinitionId;
        public int Count = 1;
        public double X, Y;
    }
    [Serializable] public sealed class ExpansionRule
    {
        public string Milestone;
        public int Capacity;
        public List<Amount> Cost = new List<Amount>();
        public double Minutes = 120;
    }
    [Serializable] public sealed class SurvivalDefinition
    {
        public int Version = 1;
        public bool TemporaryBalance = true;
        public double RealSecondsPerMinute = 0.5;
        public int StartMinute = 360, StartCapacity = 24, MaxMinutesPerAdvance = 120;
        public int Columns = 8, Rows = 6;
        public double Width = 1536, Height = 600, CellWidth = 180, CellHeight = 100;
        public List<CardRule> Cards = new List<CardRule>();
        public List<RecipeRule> Recipes = new List<RecipeRule>();
        public List<NodeRule> Nodes = new List<NodeRule>();
        public List<BiologyRule> Biology = new List<BiologyRule>();
        public List<TraitRule> Traits = new List<TraitRule>();
        public List<AfflictionRule> Afflictions = new List<AfflictionRule>();
        public List<StartItem> Start = new List<StartItem>();
        public List<Amount> FirstTide = new List<Amount>(), TideFixed = new List<Amount>(), TideRandom = new List<Amount>();
        public List<PeriodicDrop> TidePeriodic = new List<PeriodicDrop>();
        public List<ExpansionRule> Expansions = new List<ExpansionRule>();
        public BodyRules Body = new BodyRules();
        public GoalRules Goal = new GoalRules();
    }
    [Serializable] public sealed class Lot { public int Count; public long ExpiresAt, AvailableAt; }
    [Serializable] public sealed class ItemState
    {
        public string Id, DefinitionId, OwnerId = "", TraitId = "";
        public string ConditionId = "";
        public Place Place;
        public Place ResidueFrom;
        public int Quantity = 1, Slot;
        public double X, Y;
        public List<Lot> Lots = new List<Lot>();
        public int Durability, MaxDurability, Structure;
        public double Fuel, AshRemainder, Health = 100, Growth, Production;
        public bool Mature, Fed, Repeat;
        public string RepeatRecipe = "", Status = "";
        public string RepeatSecondaryId = "";
        public long BornAt, MatureAt, CooldownUntil, FeedWindowAt, LastProductionWindow = -1;
        public int LastForcedDay = -1;
        public List<Amount> Stock = new List<Amount>(), Reserves = new List<Amount>();
    }
    [Serializable] public sealed class Reservation
    {
        public string Id, TaskId, DefinitionId, OwnerId = "", TraitId = "";
        public string StackId = "";
        public string ReplacesItemId = "";
        public Place Place;
        public int Count, Slot;
        public double X, Y;
    }
    [Serializable] public sealed class TaskState
    {
        public string Id, RecipeId, TargetId = "", SecondaryId = "", WoundId = "", TraitId = "";
        public bool Human;
        public bool Suspended;
        public double Progress, WorkMinutes, SpentFood, SpentMind, ConsumedNutrition;
        public long StartedAt;
        public int Remaining = 1;
        public int Stage;
        public List<string> LockedItems = new List<string>();
        public List<Amount> Consumed = new List<Amount>();
        public List<string> QueueWounds = new List<string>();
        public string WorkSourceId = "";
        public string PreferredItemId = "", PreferredMaterialId = "";
        public List<WorkContact> Contacts = new List<WorkContact>();
    }
    [Serializable] public sealed class WorkContact { public string SourceId = "", OwnerId = ""; public double Load; }
    [Serializable] public sealed class InjuryState
    {
        public string Id, Source, Name;
        public WorkTag Tags;
        public double Deficit, Cleanliness = 1, DressingStrength, CareStrength;
        public long DressingUntil, CareUntil;
        public string DressingId = "";
    }
    [Serializable] public sealed class AfflictionState
    {
        public string Id, RuleId, InjuryId = "", Source = "";
        public double Severity, Input;
    }
    [Serializable] public sealed class ExposureState
    {
        public string Source;
        public double Accumulated, Hazard, Threshold;
        public long SafeUntil;
        public bool Warned;
    }
    [Serializable] public sealed class BodyState
    {
        public string LastVitalDamageSource = "";
        public double Food = 1, Mind = 1, Integrity = 1, Function = 1, Vitality = 1;
        public bool FoodAlert, VitalAlert;
        public List<InjuryState> Injuries = new List<InjuryState>();
        public List<AfflictionState> Afflictions = new List<AfflictionState>();
        public List<ExposureState> Exposures = new List<ExposureState>();
    }
    [Serializable] public sealed class DailyRecord
    {
        public int Day, Compost, Waste;
        public double Food;
        public List<string> Sources = new List<string>();
    }
    [Serializable] public sealed class SurvivalState
    {
        public int Version = 1, Revision, Capacity, ExpansionLevel;
        public long Minute, NextId = 1;
        public uint RandomState;
        public bool Paused = true, Dead, Completed, ContinueAfterWin;
        public int Speed = 1;
        public bool FoodSourceAlert;
        public List<ResourceChange> History = new List<ResourceChange>();
        public BodyState Body = new BodyState();
        public List<ItemState> Items = new List<ItemState>();
        public List<TaskState> Tasks = new List<TaskState>();
        public List<Reservation> Reservations = new List<Reservation>();
        public List<string> Discovered = new List<string>(), Milestones = new List<string>(), GatheredNodes = new List<string>();
        public List<DailyRecord> Days = new List<DailyRecord>();
        public List<Amount> Tide = new List<Amount>();
        public int TideDay = -1, TideTakenDay = -1, ForcedDay = -1;
        public string FailureReason = "", Notice = "";
        public List<string> Log = new List<string>();
    }
    public sealed class SurvivalResult
    {
        public bool Success;
        public bool Changed;
        public string Error = "", Description = "";
        public string Warning = "";
        public double EstimatedMinutes;
        public double EstimatedFood, EstimatedMind;
        public SurvivalState State;
        public static SurvivalResult Fail(string message) { return new SurvivalResult { Error = message }; }
        public static SurvivalResult Ok(string message = "") { return new SurvivalResult { Success = true, Description = message }; }
    }
    [Serializable] public sealed class ResourceChange { public long Minute; public string Key = ""; public double Delta; }
}
