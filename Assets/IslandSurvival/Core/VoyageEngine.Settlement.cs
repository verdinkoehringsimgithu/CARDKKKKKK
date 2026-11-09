using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        private void Begin(VoyageState s, VoyageRecipeRule recipe, MatchPlan plan)
        {
            List<string> lines = plan.Inputs.Where(input => !string.IsNullOrEmpty(input.AuxiliaryId)).Select(input => input.AuxiliaryId).ToList();
            int duration = Duration(recipe, lines);
            var job = new VoyageJobState
            {
                Id = s.NextJobId++, RecipeId = recipe.Id, Location = plan.Location, Inputs = plan.Inputs,
                AuxiliaryIds = lines, Duration = duration, Remaining = duration
            };
            foreach (VoyageJobInput input in job.Inputs) Find(s, input.CardId).JobId = job.Id;
            if (job.Location != 0) Find(s, job.Location).JobId = job.Id;
            s.Jobs.Add(job);
        }

        private int Duration(VoyageRecipeRule recipe, IEnumerable<string> lines) =>
            (int)Math.Max(1, Math.Min(int.MaxValue, recipe.Minutes + lines.Sum(id => (long)auxiliaries[id].MinutesDelta)));

        private static void Release(VoyageState s, VoyageJobState job)
        {
            foreach (VoyageJobInput input in job.Inputs) Find(s, input.CardId).JobId = 0;
            if (job.Location != 0) Find(s, job.Location).JobId = 0;
            s.Jobs.Remove(job);
        }

        private string Settle(VoyageState s, VoyageJobState job)
        {
            int firstProducedId = s.NextCardId;
            VoyageRecipeRule recipe = recipes[job.RecipeId];
            VoyageCardState location = job.Location == 0 ? null : Find(s, job.Location);
            VoyageCardState anchor = location ?? Find(s, job.Inputs[0].CardId);
            List<VoyageCardState> bodies = job.Inputs.Where(input => input.Slot >= 0).Select(input => Find(s, input.CardId))
                .Where(card => cards[card.CardId].Kind == VoyageCardKind.Body).OrderBy(card => card.Id).ToList();
            VoyageJobInput care = job.Inputs.FirstOrDefault(input => input.Slot >= 0 && recipe.Inputs[input.Slot].MatchLimb);
            if (care != null)
            {
                VoyageCardState affected = Find(s, Find(s, care.CardId).Limb);
                bodies.Remove(affected);
                bodies.Insert(0, affected);
            }
            Release(s, job);

            foreach (VoyageJobInput input in job.Inputs)
            {
                VoyageCardState card = Find(s, input.CardId);
                if (input.Slot == AttachedSlot)
                {
                    if (!string.IsNullOrEmpty(input.AuxiliaryId)) Wear(s, card, auxiliaries[input.AuxiliaryId].DurabilityCost);
                    continue;
                }
                VoyageInputRule slot = recipe.Inputs[input.Slot];
                if (slot.IsConsumed) Remove(s, card);
                else
                {
                    Wear(s, card, slot.DurabilityCost, slot.DurabilityRestore);
                    RestoreCharges(card, slot.ChargesRestore);
                }
            }

            if (location != null)
            {
                VoyageCardRule rule = cards[location.CardId];
                location.Charges -= recipe.LocationCharges;
                if (recipe.LocationCharges > 0 && location.Charges == 0) location.RechargeRemaining = rule.RechargeMinutes;
                Wear(s, location, recipe.LocationWear);
            }

            s.Hunger = Level(s.Hunger + recipe.HungerDelta);
            s.San = Level(s.San + recipe.SanDelta);
            double multiplier = 1;
            foreach (string id in job.AuxiliaryIds)
            {
                VoyageAuxiliaryRule line = auxiliaries[id];
                s.San = Level(s.San + line.SanDelta);
                multiplier = Math.Min(1000, multiplier * line.OutputMultiplier);
            }
            Emit(s, recipe.Outputs, anchor, bodies, multiplier);
            foreach (string id in job.AuxiliaryIds) Emit(s, auxiliaries[id].Outputs, anchor, bodies, 1);

            bool discovered = !recipe.Automatic && !s.Known.Contains(recipe.Id);
            if (!recipe.Automatic) Learn(s, recipe.Id);
            if (discovered && !string.IsNullOrEmpty(recipe.DiscoveryCardId) && !s.Cards.Any(card => card.CardId == recipe.DiscoveryCardId))
                EmitCard(s, recipe.DiscoveryCardId, anchor, bodies);
            foreach (VoyageCardState produced in s.Cards.Where(card => card.Id >= firstProducedId))
            {
                produced.ProducedByJobId = job.Id;
                produced.ProducedFromX = anchor.X;
                produced.ProducedFromY = anchor.Y;
            }
            return recipe.Name + "完成。";
        }

        private void Wear(VoyageState s, VoyageCardState card, int cost, int restore = 0)
        {
            VoyageCardRule rule = cards[card.CardId];
            if (rule.Durability == 0) return;
            card.Durability = (int)Math.Min(rule.Durability, Math.Max(0L, (long)card.Durability - cost + restore));
            if (card.Durability == 0 && rule.Kind != VoyageCardKind.Body) Remove(s, card);
        }

        private void RestoreCharges(VoyageCardState card, int restore)
        {
            if (restore == 0) return;
            VoyageCardRule rule = cards[card.CardId];
            card.Charges = (int)Math.Min(rule.Charges, (long)card.Charges + restore);
            if (card.Charges == rule.Charges) card.RechargeRemaining = 0;
        }

        private void Emit(VoyageState s, IReadOnlyList<VoyageOutputRule> output, VoyageCardState anchor, IReadOnlyList<VoyageCardState> bodies, double multiplier)
        {
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (VoyageOutputRule row in output)
            {
                if (string.IsNullOrEmpty(row.Group))
                {
                    double chance = row.Chance * multiplier;
                    if (chance >= 1 || chance > 0 && Roll(s) < chance) EmitRow(row);
                    continue;
                }
                if (!groups.Add(row.Group) || multiplier <= 0) continue;
                VoyageOutputRule[] choices = output.Where(option => option.Group == row.Group).ToArray();
                double total = choices.Sum(choice => choice.Chance) * multiplier;
                if (total <= 0) continue;
                double selection = Roll(s) * Math.Max(1, total);
                foreach (VoyageOutputRule choice in choices)
                {
                    selection -= choice.Chance * multiplier;
                    if (selection >= 0) continue;
                    EmitRow(choice);
                    break;
                }
            }

            void EmitRow(VoyageOutputRule row)
            {
                for (int i = 0; i < row.Quantity; i++)
                {
                    if (row.Unique && s.Cards.Any(card => card.CardId == row.CardId)) break;
                    EmitCard(s, row.CardId, anchor, bodies);
                }
            }
        }

        private void EmitCard(VoyageState s, string id, VoyageCardState anchor, IReadOnlyList<VoyageCardState> bodies)
        {
            VoyageCardRule rule = cards[id];
            VoyageCardState limb = string.IsNullOrEmpty(rule.LimbId) ? null : bodies.FirstOrDefault(body => body.CardId == rule.LimbId);
            if (rule.Kind == VoyageCardKind.Injury && limb == null) return;
            DropPoint(s, anchor.X, anchor.Y, out float x, out float y);
            if (rule.InjuryLevel == 3) ApplySevereInjury(s, rule, limb, x, y);
            else Spawn(s, id, x, y, limb?.Id ?? 0);
        }

        public VoyageResult Merge(int sourceId, int targetId) => Commit(s =>
        {
            VoyageCardState source = Find(s, sourceId);
            VoyageCardState target = Find(s, targetId);
            if (sourceId == targetId || !Available(s, source) || !Available(s, target)) return Fail("请把两张伤势牌放回桌面再合并。");
            VoyageCardRule rule = cards[source.CardId];
            if (rule.Kind != VoyageCardKind.Injury || rule.Id != target.CardId || string.IsNullOrEmpty(rule.NextInjuryId))
                return Fail("只有同种、同级的伤势可以合并。");
            if (source.Limb != target.Limb) return Fail("这两处伤势关联不同的身体牌。");
            VoyageCardState limb = Find(s, source.Limb);
            if (s.Staged.Contains(limb.Id)) return Fail("请先把关联的身体牌移回桌面。");
            VoyageCardRule next = cards[rule.NextInjuryId];
            Remove(s, source);
            Remove(s, target);
            if (next.InjuryLevel == 3) ApplySevereInjury(s, next, limb, target.X, target.Y);
            else Spawn(s, next.Id, target.X, target.Y, limb.Id);
            return Ok(next.Name + (next.InjuryLevel == 3 ? "损伤了" + cards[limb.CardId].Name + "。" : "已合并。"));
        });

        private void ApplySevereInjury(VoyageState s, VoyageCardRule injury, VoyageCardState limb, float x, float y)
        {
            Wear(s, limb, injury.LimbDamage);
            s.Hunger = Level(s.Hunger + injury.SpawnHunger);
            s.San = Level(s.San + injury.SpawnSan);
            if (!string.IsNullOrEmpty(injury.TransformId)) Spawn(s, injury.TransformId, x, y, limb.Id);
        }

        private void TickCreatures(VoyageState s)
        {
            foreach (VoyageCardState creature in s.Cards.ToArray())
            {
                string seek = cards[creature.CardId].SeekRecipeId;
                if (string.IsNullOrEmpty(seek) || !Available(s, creature) || heldCards.Contains(creature.Id)) continue;
                VoyageRecipeRule recipe = recipes[seek];
                var pair = new[] { creature.Id, 0 };
                float nearest = rules.BirdRange * rules.BirdRange;
                VoyageCardState target = null;
                MatchPlan plan = null;
                foreach (VoyageCardState card in s.Cards)
                {
                    if (card.Id == creature.Id || !Available(s, card) || IsFollowingPreparedLimb(s, card) || heldCards.Contains(card.Id)) continue;
                    float dx = card.X - creature.X, dy = card.Y - creature.Y;
                    float distance = dx * dx + dy * dy;
                    if (distance > nearest || distance == nearest && target != null) continue;
                    pair[1] = card.Id;
                    if (!TryPlan(s, recipe, pair, true, out MatchPlan candidate)) continue;
                    nearest = distance;
                    target = card;
                    plan = candidate;
                }
                if (target == null) continue;
                double length = Math.Sqrt(nearest);
                if (length > rules.BirdReach)
                {
                    double step = Math.Min(rules.BirdStep, length) / length;
                    creature.X = Math.Min(1, Math.Max(0, creature.X + (float)((target.X - creature.X) * step)));
                    creature.Y = Math.Min(1, Math.Max(0, creature.Y + (float)((target.Y - creature.Y) * step)));
                    length -= Math.Min(rules.BirdStep, length);
                }
                if (length <= rules.BirdReach) Begin(s, recipe, plan);
            }
        }

        private static void DropPoint(VoyageState s, float x, float y, out float resultX, out float resultY)
        {
            // 优先在产出来源周围落牌，拥挤时允许重叠，物品始终可以手动整理。
            resultX = Math.Min(1, Math.Max(0, x + 0.012f * (1 + s.NextCardId % 6)));
            resultY = Math.Min(1, Math.Max(0, y + 0.010f * (1 + s.NextCardId % 7)));
            float nearest = float.MaxValue;
            const int columns = 8, rows = 15;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float cx = (column + 0.5f) / columns, cy = (row + 0.5f) / rows;
                float dx = cx - x, dy = cy - y;
                float distance = dx * dx + dy * dy;
                if (distance >= nearest || s.Cards.Any(card => Math.Abs(card.X - cx) < 1f / columns && Math.Abs(card.Y - cy) < 1f / rows)) continue;
                nearest = distance;
                resultX = cx;
                resultY = cy;
            }
        }
    }
}
