using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        private const int MaxStageSize = 24;
        private const int AttachedSlot = -1;

        private sealed class MatchPlan
        {
            public readonly List<VoyageJobInput> Inputs = new List<VoyageJobInput>();
            public int Location;
        }

        public VoyageOffer Preview(int additionalCardId = 0)
        {
            var selected = new List<int>(state.Staged);
            if (additionalCardId != 0 && !selected.Contains(additionalCardId))
            {
                if (!Available(state, Find(state, additionalCardId)) || selected.Count >= MaxStageSize)
                    return new VoyageOffer { Detail = "这张牌现在不能加入组合。" };
                selected.Add(additionalCardId);
            }
            if (selected.Count == 0 || state.Dead || state.Won) return new VoyageOffer();

            VoyageRecipeRule ready = FindExact(state, selected, out _, knownOnly: true);
            if (ready != null) return new VoyageOffer { Match = VoyageMatch.Ready, Name = ready.Name, Detail = "可以开始。" };

            var related = new HashSet<int>();
            VoyageRecipeRule first = null;
            List<string> missing = null;
            foreach (VoyageRecipeRule recipe in rules.Recipes)
            {
                if (recipe.Automatic || !state.Known.Contains(recipe.Id) || !TryPlan(state, recipe, selected, false, out MatchPlan partial)) continue;
                if (first == null)
                {
                    first = recipe;
                    missing = MissingInputs(recipe, partial).ToList();
                }
                foreach (VoyageCardState card in state.Cards)
                {
                    if (related.Contains(card.Id) || selected.Count >= MaxStageSize || selected.Contains(card.Id) || !Available(state, card)) continue;
                    if (!CouldAdd(recipe, card, partial)) continue;
                    selected.Add(card.Id);
                    if (TryPlan(state, recipe, selected, false, out _)) related.Add(card.Id);
                    selected.RemoveAt(selected.Count - 1);
                }
            }
            if (first == null) return new VoyageOffer { Match = VoyageMatch.Unknown, Detail = "没有已知的配方，可以试一试。" };
            return new VoyageOffer
            {
                Match = VoyageMatch.Incomplete,
                Name = first.Name,
                Detail = "还缺 " + string.Join("、", missing.Take(4)) + (missing.Count > 4 ? " 等" : ""),
                RelatedCardIds = related.OrderBy(id => id).ToList()
            };
        }

        public VoyageResult Start() => Commit(s =>
        {
            if (s.Staged.Count == 0) return Fail("先把牌放进组合槽。");
            VoyageRecipeRule recipe = FindExact(s, s.Staged, out MatchPlan plan);
            if (recipe == null) return Fail("当前组合无法开始。检查卡牌、耐久与剩余采集次数。");
            bool known = s.Known.Contains(recipe.Id);
            Begin(s, recipe, plan);
            s.Staged.Clear();
            return Ok(known ? recipe.Name + "已开始。" : "正在尝试这个组合。");
        });

        public VoyageResult Cancel(int jobId) => Commit(s =>
        {
            VoyageJobState job = s.Jobs.Find(task => task.Id == jobId);
            if (job == null) return Fail("这项组合已经结束。");
            Release(s, job);
            return Ok("已中止，卡牌已退回。");
        });

        private VoyageRecipeRule FindExact(VoyageState s, IReadOnlyList<int> selected, out MatchPlan plan, bool knownOnly = false)
        {
            VoyageRecipeRule unknown = null;
            plan = null;
            foreach (VoyageRecipeRule recipe in rules.Recipes)
            {
                bool known = s.Known.Contains(recipe.Id);
                if (recipe.Automatic || !known && (knownOnly || unknown != null) || !TryPlan(s, recipe, selected, true, out MatchPlan candidate)) continue;
                if (known)
                {
                    plan = candidate;
                    return recipe;
                }
                unknown = recipe;
                plan = candidate;
            }
            return unknown;
        }

        // 预览与开始使用同一个槽匹配。固定 ID 与标签重叠时，回溯保证每个物理实例只填一个槽。
        private bool TryPlan(VoyageState s, VoyageRecipeRule recipe, IReadOnlyList<int> ids, bool exact, out MatchPlan plan)
        {
            plan = null;
            int slotCount = recipe.Inputs.Sum(slot => slot.Quantity) + (string.IsNullOrEmpty(recipe.LocationId) ? 0 : 1);
            if (ids.Count == 0 || exact && ids.Count < slotCount) return false;
            VoyageCardState[] selected = ids.Select(id => Find(s, id)).ToArray();
            if (selected.Any(card => card == null || card.JobId != 0)) return false;

            VoyageAuxiliaryRule[] lines = rules.AuxiliaryLines.Where(line => AppliesToRecipe(line, recipe)).ToArray();
            var candidate = new MatchPlan();
            int[] remaining = recipe.Inputs.Select(input => input.Quantity).ToArray();
            if (!Assign(0)) return false;
            plan = candidate;
            return true;

            bool Assign(int index)
            {
                if (index >= selected.Length) return FinishPlan();
                VoyageCardState card = selected[index];
                if (candidate.Location == 0 && LocationMatches(recipe, card))
                {
                    candidate.Location = card.Id;
                    if (Assign(index + 1)) return true;
                    candidate.Location = 0;
                }
                for (int slot = 0; slot < remaining.Length; slot++)
                {
                    if (remaining[slot] == 0 || !InputMatches(recipe.Inputs[slot], card)) continue;
                    remaining[slot]--;
                    candidate.Inputs.Add(new VoyageJobInput { CardId = card.Id, Slot = slot });
                    if (Assign(index + 1)) return true;
                    candidate.Inputs.RemoveAt(candidate.Inputs.Count - 1);
                    remaining[slot]++;
                }
                foreach (VoyageAuxiliaryRule line in lines)
                {
                    if (!LineCardMatches(line, card)) continue;
                    candidate.Inputs.Add(new VoyageJobInput { CardId = card.Id, Slot = AttachedSlot, AuxiliaryId = line.Id });
                    if (Assign(index + 1)) return true;
                    candidate.Inputs.RemoveAt(candidate.Inputs.Count - 1);
                }
                // 同一肢体的伤势即使没有额外效果，也必须随其一起锁定。
                if (cards[card.CardId].Kind == VoyageCardKind.Injury && card.Limb != 0)
                {
                    candidate.Inputs.Add(new VoyageJobInput { CardId = card.Id, Slot = AttachedSlot });
                    if (Assign(index + 1)) return true;
                    candidate.Inputs.RemoveAt(candidate.Inputs.Count - 1);
                }
                return false;
            }

            bool FinishPlan()
            {
                bool complete = remaining.All(count => count == 0) && (candidate.Location != 0 || string.IsNullOrEmpty(recipe.LocationId));
                if (exact && !complete) return false;
                var main = candidate.Inputs.Where(input => input.Slot >= 0).Select(input => Find(s, input.CardId)).ToList();
                var limbs = new HashSet<int>(main.Where(card => cards[card.CardId].Kind == VoyageCardKind.Body).Select(card => card.Id));
                foreach (VoyageJobInput input in candidate.Inputs)
                {
                    VoyageCardState card = Find(s, input.CardId);
                    bool needsLimb = input.Slot == AttachedSlot && card.Limb != 0 || input.Slot >= 0 && recipe.Inputs[input.Slot].MatchLimb;
                    if (needsLimb && !limbs.Contains(card.Limb) && (complete || !HasPendingLimb(card.Limb))) return false;
                    if (!string.IsNullOrEmpty(input.AuxiliaryId))
                    {
                        string requiredId = auxiliaries[input.AuxiliaryId].RequiredInputId;
                        if (!string.IsNullOrEmpty(requiredId) && !main.Any(item => item.CardId == requiredId) &&
                            (complete || !recipe.Inputs.Where((_, index) => remaining[index] > 0).Any(slot => RuleMatches(slot, cards[requiredId])))) return false;
                    }
                }
                if (!exact) return true;

                var extra = new List<VoyageJobInput>();
                foreach (VoyageCardState attached in s.Cards)
                {
                    if (!limbs.Contains(attached.Limb) || candidate.Inputs.Any(input => input.CardId == attached.Id)) continue;
                    VoyageAuxiliaryRule line = lines.FirstOrDefault(item => LineCardMatches(item, attached) &&
                        (string.IsNullOrEmpty(item.RequiredInputId) || main.Any(card => card.CardId == item.RequiredInputId)));
                    if (line == null && cards[attached.CardId].Kind != VoyageCardKind.Injury) continue;
                    if (ReservedElsewhere(s, attached.Id) || attached.JobId != 0 || s.Staged.Contains(attached.Id) && !ids.Contains(attached.Id)) return false;
                    extra.Add(new VoyageJobInput { CardId = attached.Id, Slot = AttachedSlot, AuxiliaryId = line?.Id ?? "" });
                }
                candidate.Inputs.AddRange(extra);
                return true;
            }

            bool HasPendingLimb(int id)
            {
                VoyageCardState limb = Find(s, id);
                return limb != null && limb.JobId == 0 && !selected.Contains(limb) &&
                    recipe.Inputs.Where((_, index) => remaining[index] > 0).Any(slot => InputMatches(slot, limb));
            }
        }

        private bool CouldAdd(VoyageRecipeRule recipe, VoyageCardState card, MatchPlan partial)
        {
            if (partial.Location == 0 && LocationMatches(recipe, card)) return true;
            return recipe.Inputs.Any(slot => InputMatches(slot, card));
        }

        private IEnumerable<string> MissingInputs(VoyageRecipeRule recipe, MatchPlan partial)
        {
            if (!string.IsNullOrEmpty(recipe.LocationId) && partial.Location == 0) yield return cards[recipe.LocationId].Name;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                VoyageInputRule slot = recipe.Inputs[i];
                int missing = slot.Quantity - partial.Inputs.Count(input => input.Slot == i);
                if (missing <= 0) continue;
                string name = string.IsNullOrEmpty(slot.CardId)
                    ? string.Join("或", rules.Cards.Where(card => RuleMatches(slot, card)).Select(card => card.Name).Distinct().Take(3))
                    : cards[slot.CardId].Name;
                yield return missing > 1 ? name + "×" + missing : name;
            }
        }

        private static bool AppliesToRecipe(VoyageAuxiliaryRule line, VoyageRecipeRule recipe) =>
            string.IsNullOrEmpty(line.RecipeTag) || line.RecipeTag == recipe.Tag;

        private bool LineCardMatches(VoyageAuxiliaryRule line, VoyageCardState card) =>
            line.CardId == card.CardId && card.Durability >= line.DurabilityCost &&
            (cards[card.CardId].Durability == 0 || card.Durability > 0);

        private bool LocationMatches(VoyageRecipeRule recipe, VoyageCardState card) =>
            recipe.LocationId == card.CardId && card.Durability >= recipe.LocationWear && card.Charges >= recipe.LocationCharges &&
            (cards[card.CardId].Durability == 0 || card.Durability > 0);

        private bool InputMatches(VoyageInputRule slot, VoyageCardState card)
        {
            VoyageCardRule rule = cards[card.CardId];
            return RuleMatches(slot, rule) && card.Durability >= slot.DurabilityCost &&
                (rule.Durability == 0 || card.Durability > 0 || slot.DurabilityRestore > 0) &&
                (slot.DurabilityRestore == 0 || rule.Durability > 0) && (slot.ChargesRestore == 0 || rule.Charges > 0);
        }

        private static bool RuleMatches(VoyageInputRule slot, VoyageCardRule rule) =>
            (string.IsNullOrEmpty(slot.CardId) || rule.Id == slot.CardId) &&
            (string.IsNullOrEmpty(slot.Tag) || rule.Tags.Contains(slot.Tag));
    }
}
