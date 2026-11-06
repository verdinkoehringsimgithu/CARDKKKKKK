using System.Collections.Generic;

namespace CardKKK.Spellcraft.Core
{
    internal static class ForgeCopies
    {
        internal static ForgeCircuit Circuit(ForgeCircuit value)
        {
            return new ForgeCircuit { TriggerId = value.TriggerId, EffectId = value.EffectId, ModifierId = value.ModifierId };
        }

        internal static ForgeDefinition Definition(ForgeDefinition value)
        {
            var result = new ForgeDefinition
            {
                MaxHealth = value.MaxHealth, EnergyPerTurn = value.EnergyPerTurn,
                StartingSpark = value.StartingSpark, SparkCap = value.SparkCap,
                HandSize = value.HandSize, MaxCircuitActivations = value.MaxCircuitActivations,
                MaxEventsPerCommand = value.MaxEventsPerCommand, HealAfterBattle = value.HealAfterBattle,
                StartingDeck = new List<string>(value.StartingDeck),
                StartingUnlocked = new List<string>(value.StartingUnlocked)
            };
            foreach (ForgeModule item in value.Modules)
                result.Modules.Add(new ForgeModule
                {
                    Id = item.Id, Name = item.Name, Description = item.Description, Kind = item.Kind,
                    Value = item.Value, SparkCost = item.SparkCost, SpriteIndex = item.SpriteIndex,
                    Trigger = item.Trigger, Effect = item.Effect, Modifier = item.Modifier
                });
            foreach (ForgeCard item in value.Cards)
                result.Cards.Add(new ForgeCard
                {
                    Id = item.Id, Name = item.Name, Description = item.Description, Cost = item.Cost,
                    SparkCost = item.SparkCost, Power = item.Power, SpriteIndex = item.SpriteIndex, Kind = item.Kind
                });
            foreach (ForgeCircuit item in value.StartingCircuits) result.StartingCircuits.Add(Circuit(item));
            foreach (ForgeEncounter item in value.Encounters)
            {
                var encounter = new ForgeEncounter
                {
                    Id = item.Id, Name = item.Name, Description = item.Description,
                    RewardModuleIds = new List<string>(item.RewardModuleIds)
                };
                foreach (ForgeEnemyTemplate enemy in item.Enemies)
                {
                    var template = new ForgeEnemyTemplate
                    {
                        Id = enemy.Id, Name = enemy.Name, Description = enemy.Description,
                        MaxHealth = enemy.MaxHealth, ReflectDamage = enemy.ReflectDamage, SpriteIndex = enemy.SpriteIndex
                    };
                    foreach (ForgeIntent intent in enemy.Intents)
                        template.Intents.Add(new ForgeIntent
                        {
                            Kind = intent.Kind, Power = intent.Power, Name = intent.Name, Description = intent.Description
                        });
                    encounter.Enemies.Add(template);
                }
                result.Encounters.Add(encounter);
            }
            return result;
        }

        internal static ForgeState State(ForgeState value)
        {
            var result = new ForgeState
            {
                Phase = value.Phase, EncounterIndex = value.EncounterIndex, Turn = value.Turn,
                PlayerHealth = value.PlayerHealth, PlayerBlock = value.PlayerBlock, Energy = value.Energy,
                Spark = value.Spark, SelectedEnemy = value.SelectedEnemy, Seed = value.Seed, RandomState = value.RandomState,
                UnlockedModuleIds = new List<string>(value.UnlockedModuleIds), Hand = new List<string>(value.Hand),
                DrawPile = new List<string>(value.DrawPile), DiscardPile = new List<string>(value.DiscardPile),
                CircuitActivations = new List<int>(value.CircuitActivations)
            };
            foreach (ForgeCircuit item in value.Circuits) result.Circuits.Add(Circuit(item));
            foreach (ForgeEnemyState item in value.Enemies)
                result.Enemies.Add(new ForgeEnemyState
                {
                    DefinitionId = item.DefinitionId, Health = item.Health, Block = item.Block,
                    Burn = item.Burn, Frost = item.Frost, IntentIndex = item.IntentIndex, ChargedPower = item.ChargedPower
                });
            return result;
        }
    }
}
