using System;
using System.Collections.Generic;

namespace CardKKK.Spellcraft.Core
{
    public enum ForgePhase { Workshop, Battle, Reward, Victory, Defeat }
    public enum ForgeModuleKind { Trigger, Effect, Modifier }
    public enum ForgeTrigger { Manual, TurnStart, HitTaken, Blocked, SparkSpent, BurnTick }
    public enum ForgeEffect { Bolt, Barrier, Charge, Ignite, Frost, Drain }
    public enum ForgeModifier { None, Fork, Echo, Overload }
    public enum ForgeCardKind { Strike, Guard, Charge, Ignite, Discharge }
    public enum ForgeIntentKind { Attack, DoubleAttack, Guard, DrainSpark, Charge }

    [Serializable]
    public sealed class ForgeDefinition
    {
        public int MaxHealth = 40;
        public int EnergyPerTurn = 3;
        public int StartingSpark = 2;
        public int SparkCap = 12;
        public int HandSize = 5;
        public int MaxCircuitActivations = 2;
        public int MaxEventsPerCommand = 128;
        public int HealAfterBattle = 6;
        public List<ForgeModule> Modules = new List<ForgeModule>();
        public List<ForgeCard> Cards = new List<ForgeCard>();
        public List<string> StartingDeck = new List<string>();
        public List<string> StartingUnlocked = new List<string>();
        public List<ForgeEncounter> Encounters = new List<ForgeEncounter>();
        public List<ForgeCircuit> StartingCircuits = new List<ForgeCircuit>();
    }

    [Serializable]
    public sealed class ForgeModule
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public ForgeModuleKind Kind;
        public int Value;
        public int SparkCost = 1;
        public int SpriteIndex;
        public ForgeTrigger Trigger;
        public ForgeEffect Effect;
        public ForgeModifier Modifier;
    }

    [Serializable]
    public sealed class ForgeCard
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public int Cost;
        public int SparkCost;
        public int Power;
        public int SpriteIndex;
        public ForgeCardKind Kind;
    }

    [Serializable]
    public sealed class ForgeIntent
    {
        public ForgeIntentKind Kind;
        public int Power;
        public string Name = "";
        public string Description = "";
    }

    [Serializable]
    public sealed class ForgeEnemyTemplate
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public int MaxHealth;
        public int ReflectDamage;
        public int SpriteIndex;
        public List<ForgeIntent> Intents = new List<ForgeIntent>();
    }

    [Serializable]
    public sealed class ForgeEncounter
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public List<ForgeEnemyTemplate> Enemies = new List<ForgeEnemyTemplate>();
        public List<string> RewardModuleIds = new List<string>();
    }

    [Serializable]
    public sealed class ForgeCircuit
    {
        public string TriggerId = "";
        public string EffectId = "";
        public string ModifierId = "";
    }

    [Serializable]
    public sealed class ForgeEnemyState
    {
        public string DefinitionId = "";
        public int Health;
        public int Block;
        public int Burn;
        public int Frost;
        public int IntentIndex;
        // 蓄力只增强下一次攻击；双击的两段共享本次增幅。
        public int ChargedPower;
    }

    [Serializable]
    public sealed class ForgeState
    {
        public ForgePhase Phase;
        public int EncounterIndex;
        public int Turn;
        public int PlayerHealth;
        public int PlayerBlock;
        public int Energy;
        public int Spark;
        public int SelectedEnemy = -1;
        public int Seed;
        // xorshift32 的完整状态，按位保存为 int；不依赖不可恢复的 System.Random。
        public int RandomState;
        public List<ForgeEnemyState> Enemies = new List<ForgeEnemyState>();
        public List<ForgeCircuit> Circuits = new List<ForgeCircuit>();
        public List<string> UnlockedModuleIds = new List<string>();
        public List<string> Hand = new List<string>();
        public List<string> DrawPile = new List<string>();
        public List<string> DiscardPile = new List<string>();
        public List<int> CircuitActivations = new List<int>();
    }

    [Serializable]
    public sealed class ForgeEvent
    {
        public string Kind = "";
        public string Text = "";
        public int EnemyIndex = -1;
        public int CircuitIndex = -1;
        public int Value;
    }

    [Serializable]
    public sealed class ForgeResult
    {
        public bool Success;
        public string Error = "";
        public List<ForgeEvent> Events = new List<ForgeEvent>();
    }

    [Serializable]
    public sealed class ForgePreview
    {
        public bool Success;
        public string Error = "";
        public ForgeState State;
        public List<ForgeEvent> Events = new List<ForgeEvent>();
    }
}
