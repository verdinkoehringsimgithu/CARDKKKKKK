using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        private readonly VoyageDefinition rules;
        private readonly Dictionary<string, VoyageCardRule> cards;
        private readonly Dictionary<string, VoyageRecipeRule> recipes;
        private readonly Dictionary<string, VoyageAuxiliaryRule> auxiliaries;
        private readonly HashSet<int> heldCards = new HashSet<int>();
        private VoyageState state;

        public VoyageState Snapshot => Copy(state);
        public IReadOnlyList<int> PreparedFollowers => state.Cards.Where(card => card.JobId == 0 &&
            IsFollowingPreparedLimb(state, card) && !state.Staged.Contains(card.Id)).Select(card => card.Id).ToArray();

        public VoyageEngine(VoyageDefinition definition)
        {
            string error = ValidateDefinition(definition);
            if (error != "") throw new ArgumentException(error, nameof(definition));
            rules = Copy(definition);
            cards = rules.Cards.ToDictionary(card => card.Id, StringComparer.Ordinal);
            recipes = rules.Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
            auxiliaries = rules.AuxiliaryLines.ToDictionary(line => line.Id, StringComparer.Ordinal);
            state = new VoyageState { RandomState = rules.Seed == 0 ? 19349663 : rules.Seed };
            foreach (VoyageRecipeRule recipe in rules.Recipes.Where(recipe => recipe.InitiallyKnown))
                Learn(state, recipe.Id);
            foreach (VoyageStartCard start in rules.StartCards)
                Spawn(state, start.CardId, start.X, start.Y);
            ResolveEnd(state);
            error = ValidateState(state);
            if (error != "") throw new ArgumentException(error, nameof(definition));
        }

        private VoyageResult Commit(Func<VoyageState, VoyageResult> change)
        {
            if (state.Dead || state.Won) return Fail("这一局已结束。");
            VoyageState candidate = Copy(state);
            VoyageResult result = change(candidate);
            if (!result.Success || !result.Changed) return result;
            ResolveEnd(candidate);
            string error = ValidateState(candidate);
            if (error != "") return Fail(error);
            state = candidate;
            return result;
        }

        public VoyageResult Restore(VoyageState saved)
        {
            string error = ValidateState(saved);
            if (error != "") return Fail(error);
            state = Copy(saved);
            state.Paused = true;
            heldCards.Clear();
            return Ok("已读档，时间暂停。");
        }

        public VoyageResult SetPaused(bool paused)
        {
            if (paused == state.Paused) return Unchanged();
            return Commit(s => { s.Paused = paused; return Ok(); });
        }

        public VoyageResult Stage(int cardId) => Commit(s =>
        {
            VoyageCardState card = Find(s, cardId);
            if (card == null) return Fail("这张牌已经离开桌面。");
            if (card.JobId != 0) return Fail("这张牌正在使用。");
            if (s.Staged.Contains(cardId)) return Unchanged();
            if (s.Staged.Count >= MaxStageSize) return Fail("这个组合放不下更多牌。");
            s.Staged.Add(cardId);
            return Ok();
        });

        public VoyageResult Unstage(int cardId) => Commit(s => s.Staged.Remove(cardId) ? Ok() : Unchanged());

        public VoyageResult ClearStage() => Commit(s =>
        {
            if (s.Staged.Count == 0) return Unchanged();
            s.Staged.Clear();
            return Ok();
        });

        public VoyageResult Move(int cardId, float x, float y) => Commit(s =>
        {
            VoyageCardState card = Find(s, cardId);
            if (!Available(s, card)) return Fail("请先把这张牌移回桌面。");
            if (IsFollowingPreparedLimb(s, card)) return Fail("这张伤势正随身体牌准备配方。");
            if (!OnDesk(x, y)) return Fail("这个位置超出了桌面。");
            if (card.X == x && card.Y == y) return Unchanged();
            card.X = x;
            card.Y = y;
            return Ok();
        });

        public void Hold(int cardId, bool held)
        {
            if (!held) heldCards.Remove(cardId);
            else
            {
                VoyageCardState card = Find(state, cardId);
                if (Available(state, card) && !IsFollowingPreparedLimb(state, card)) heldCards.Add(cardId);
            }
        }

        public VoyageResult Read(int cardId)
        {
            VoyageCardState card = Find(state, cardId);
            if (card == null) return Fail("这张牌已经离开桌面。");
            return Unchanged(cards[card.CardId].Description);
        }

        public VoyageResult Advance(int minutes)
        {
            if (minutes <= 0 || minutes > 240 || state.Minute > int.MaxValue - minutes)
                return Fail("时间推进超出范围。");
            if (state.Paused) return Unchanged();
            return Commit(s =>
            {
                string notice = "";
                for (int i = 0; i < minutes && !s.Dead && !s.Won; i++)
                {
                    s.Minute++;
                    s.Hunger = Level(s.Hunger - rules.HungerPerMinute);
                    ResolveEnd(s);
                    if (s.Dead) break;
                    TickCards(s);
                    ResolveEnd(s);
                    if (s.Dead || s.Won) break;
                    foreach (VoyageJobState job in s.Jobs.ToArray())
                    {
                        job.Remaining--;
                        if (job.Remaining > 0) continue;
                        notice = Settle(s, job);
                        ResolveEnd(s);
                        if (s.Dead || s.Won) break;
                    }
                    if (!s.Dead && !s.Won) TickCreatures(s);
                }
                return Ok(s.Dead || s.Won ? s.EndReason : notice);
            });
        }

        private void TickCards(VoyageState s)
        {
            foreach (VoyageCardState card in s.Cards.ToArray())
            {
                if (card.JobId != 0) continue;
                VoyageCardRule rule = cards[card.CardId];
                if (card.RechargeRemaining > 0 && --card.RechargeRemaining == 0) card.Charges = rule.Charges;
                if (card.TimerRemaining <= 0 || --card.TimerRemaining > 0) continue;
                if (string.IsNullOrEmpty(rule.TransformId)) Remove(s, card);
                else ConfigureCard(s, card, cards[rule.TransformId], card.Limb);
            }
        }

        private void ResolveEnd(VoyageState s)
        {
            if (s.Dead) return;
            if (s.Hunger <= 0 || s.San <= 0)
            {
                s.Dead = true;
                s.Won = false;
                s.Paused = true;
                s.EndReason = s.Hunger <= 0 ? "饥饿水位耗尽。" : "SAN 水位耗尽。";
            }
            else if (!s.Won && s.Cards.Any(card => cards[card.CardId].Kind == VoyageCardKind.Ending))
            {
                s.Won = true;
                s.Paused = true;
                s.EndReason = "出航！";
            }
        }

        private VoyageCardState Spawn(VoyageState s, string id, float x, float y, int limb = 0)
        {
            var card = new VoyageCardState { Id = s.NextCardId++, X = x, Y = y };
            ConfigureCard(s, card, cards[id], limb);
            s.Cards.Add(card);
            return card;
        }

        private void ConfigureCard(VoyageState s, VoyageCardState card, VoyageCardRule rule, int limb)
        {
            card.CardId = rule.Id;
            card.Durability = rule.Durability;
            card.Charges = rule.Charges;
            card.RechargeRemaining = 0;
            card.TimerRemaining = rule.TimerMinutes;
            VoyageCardState body = Find(s, limb);
            card.Limb = body != null && body.CardId == rule.LimbId ? limb : 0;
            if (card.Limb == 0 && rule.Kind == VoyageCardKind.Thought && !string.IsNullOrEmpty(rule.LimbId))
            {
                VoyageCardState[] candidates = s.Cards.Where(item => item.CardId == rule.LimbId).Take(2).ToArray();
                if (candidates.Length == 1) card.Limb = candidates[0].Id;
            }
            s.Hunger = Level(s.Hunger + rule.SpawnHunger);
            s.San = Level(s.San + rule.SpawnSan);
            foreach (string recipe in rule.GrantsRecipes) Learn(s, recipe);
        }

        private static void Learn(VoyageState s, string recipe)
        {
            if (!s.Known.Contains(recipe)) s.Known.Add(recipe);
        }

        private static void Remove(VoyageState s, VoyageCardState card)
        {
            s.Staged.Remove(card.Id);
            s.Cards.Remove(card);
        }

        private static double Roll(VoyageState s)
        {
            uint value = unchecked((uint)s.RandomState);
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            s.RandomState = unchecked((int)value);
            return value / 4294967296.0;
        }

        private static VoyageCardState Find(VoyageState s, int id) => s.Cards.Find(card => card.Id == id);
        private static bool Available(VoyageState s, VoyageCardState card) => card != null && card.JobId == 0 && !s.Staged.Contains(card.Id);
        private bool IsFollowingPreparedLimb(VoyageState s, VoyageCardState card) => card.Limb != 0 &&
            cards[card.CardId].Kind == VoyageCardKind.Injury && s.Staged.Contains(card.Limb);
        private static bool OnDesk(float x, float y) => x >= 0 && x <= 1 && y >= 0 && y <= 1;
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        private static double Level(double value) => Math.Max(0, Math.Min(100, value));
        private static VoyageResult Ok(string message = "") => new VoyageResult(true, message);
        private static VoyageResult Fail(string message) => new VoyageResult(false, message, false);
        private static VoyageResult Unchanged(string message = "") => new VoyageResult(true, message, false);
    }
}
