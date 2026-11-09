using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        public static string ValidateDefinition(VoyageDefinition definition)
        {
            if (definition == null || definition.Cards == null || definition.Cards.Count == 0 ||
                definition.Recipes == null || definition.Recipes.Count == 0 || definition.AuxiliaryLines == null ||
                definition.StartCards == null || definition.StartCards.Count == 0)
                return "出航规则缺少卡牌、配方或初始牌配置。";
            if (!Finite(definition.HungerPerMinute) || definition.HungerPerMinute < 0 ||
                !Finite(definition.RealSecondsPerMinute) || definition.RealSecondsPerMinute <= 0 ||
                !Finite(definition.BirdStep) || definition.BirdStep < 0 || definition.BirdStep > 1 ||
                !Finite(definition.BirdRange) || definition.BirdRange < 0 || definition.BirdRange > 2 ||
                !Finite(definition.BirdReach) || definition.BirdReach < 0 || definition.BirdReach > definition.BirdRange)
                return "出航规则的时间、水位消耗或生物移动参数超出范围。";

            var cardMap = new Dictionary<string, VoyageCardRule>(StringComparer.Ordinal);
            foreach (VoyageCardRule card in definition.Cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Name) || cardMap.ContainsKey(card.Id))
                    return "卡牌 ID 或名称缺失，或卡牌 ID 重复。";
                cardMap.Add(card.Id, card);
            }
            var recipeMap = new Dictionary<string, VoyageRecipeRule>(StringComparer.Ordinal);
            foreach (VoyageRecipeRule recipe in definition.Recipes)
            {
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name) || recipeMap.ContainsKey(recipe.Id))
                    return "配方 ID 或名称缺失，或配方 ID 重复。";
                recipeMap.Add(recipe.Id, recipe);
            }
            foreach (VoyageCardRule card in definition.Cards)
            {
                if (!Enum.IsDefined(typeof(VoyageCardKind), card.Kind) || card.Durability < 0 || card.Charges < 0 || card.RechargeMinutes < 0 ||
                    card.TimerMinutes < 0 || card.RechargeMinutes > 0 && card.Charges == 0 ||
                    !Finite(card.SpawnHunger) || !Finite(card.SpawnSan) || card.Tags == null || card.GrantsRecipes == null ||
                    card.Tags.Any(string.IsNullOrWhiteSpace) || card.GrantsRecipes.Any(id => !Has(recipeMap, id)))
                    return card.Name + "的耐久、计时、标签或解锁配置有误。";

                if (card.Kind == VoyageCardKind.Body && (card.Durability == 0 || card.TimerMinutes > 0))
                    return card.Name + "的身体卡需要耐久，且不能自动消失。";
                if (!string.IsNullOrEmpty(card.TransformId))
                {
                    if (!Has(cardMap, card.TransformId) || card.TimerMinutes == 0 && card.InjuryLevel != 3)
                        return card.Name + "的转化目标或倒计时无效。";
                    VoyageCardRule transformed = cardMap[card.TransformId];
                    if (transformed.Kind == VoyageCardKind.Body || transformed.InjuryLevel == 3 ||
                        transformed.Kind == VoyageCardKind.Injury && (card.Kind != VoyageCardKind.Injury || transformed.LimbId != card.LimbId))
                        return card.Name + "的转化不能生成未关联的身体或伤势。";
                }
                if (card.Kind == VoyageCardKind.Injury)
                {
                    if (card.InjuryLevel < 1 || card.InjuryLevel > 3 || !Has(cardMap, card.LimbId) || cardMap[card.LimbId].Kind != VoyageCardKind.Body ||
                        card.LimbDamage < 0 || card.InjuryLevel == 3 && (card.LimbDamage == 0 || card.TimerMinutes != 0))
                        return card.Name + "的伤势等级、关联肢体或伤害无效。";
                    if (!string.IsNullOrEmpty(card.NextInjuryId) &&
                        (!Has(cardMap, card.NextInjuryId) || cardMap[card.NextInjuryId].Kind != VoyageCardKind.Injury ||
                         cardMap[card.NextInjuryId].InjuryLevel != card.InjuryLevel + 1 || cardMap[card.NextInjuryId].LimbId != card.LimbId))
                        return card.Name + "的下一等级伤势无效。";
                }
                else if (card.InjuryLevel != 0 || card.LimbDamage != 0 || !string.IsNullOrEmpty(card.NextInjuryId) ||
                    !string.IsNullOrEmpty(card.LimbId) && (card.Kind != VoyageCardKind.Thought || !Has(cardMap, card.LimbId) || cardMap[card.LimbId].Kind != VoyageCardKind.Body))
                    return card.Name + "配置了无效的身体关联。";

                if (!string.IsNullOrEmpty(card.SeekRecipeId) &&
                    (card.Kind != VoyageCardKind.Creature || !Has(recipeMap, card.SeekRecipeId) || !recipeMap[card.SeekRecipeId].Automatic ||
                     definition.BirdStep <= 0 || definition.BirdReach <= 0))
                    return card.Name + "的自动觅食配方或移动参数无效。";
            }
            foreach (VoyageRecipeRule recipe in definition.Recipes)
            {
                if (recipe.Minutes <= 0 || recipe.LocationWear < 0 || recipe.LocationCharges < 0 ||
                    !Finite(recipe.HungerDelta) || !Finite(recipe.SanDelta) || recipe.Inputs == null || recipe.Outputs == null ||
                    !string.IsNullOrEmpty(recipe.DiscoveryCardId) &&
                    (!Has(cardMap, recipe.DiscoveryCardId) || cardMap[recipe.DiscoveryCardId].Kind != VoyageCardKind.Information))
                    return recipe.Name + "的时长、产物或发现配置有误。";

                int slotCount = 0;
                if (!string.IsNullOrEmpty(recipe.LocationId))
                {
                    if (!Has(cardMap, recipe.LocationId)) return recipe.Name + "引用了未知场所。";
                    VoyageCardRule place = cardMap[recipe.LocationId];
                    if (place.Kind != VoyageCardKind.Resource && place.Kind != VoyageCardKind.Facility && place.Kind != VoyageCardKind.Information ||
                        recipe.LocationWear > place.Durability || recipe.LocationCharges > place.Charges)
                        return recipe.Name + "的场所类型、耐久或采集次数有误。";
                    slotCount = 1;
                }
                else if (recipe.LocationWear != 0 || recipe.LocationCharges != 0) return recipe.Name + "配置了无场所的耗损。";

                foreach (VoyageInputRule input in recipe.Inputs)
                {
                    if (input == null || input.Quantity <= 0 || input.Quantity > MaxStageSize || input.DurabilityCost < 0 ||
                        input.DurabilityRestore < 0 || input.ChargesRestore < 0 || string.IsNullOrEmpty(input.CardId) && string.IsNullOrEmpty(input.Tag))
                        return recipe.Name + "有未指定卡牌或数量的输入槽。";
                    VoyageCardRule[] options = definition.Cards.Where(card => RuleMatches(input, card)).ToArray();
                    if (options.Length == 0 || options.All(card => input.DurabilityCost > card.Durability || input.DurabilityRestore > 0 && card.Durability == 0 ||
                        input.ChargesRestore > 0 && card.Charges == 0) || input.IsConsumed &&
                        (options.Any(card => card.Kind == VoyageCardKind.Body) || input.DurabilityCost != 0 || input.DurabilityRestore != 0 || input.ChargesRestore != 0) ||
                        input.MatchLimb && options.All(card => string.IsNullOrEmpty(card.LimbId)))
                        return recipe.Name + "的输入卡牌、保留规则或维护参数有误。";
                    slotCount += input.Quantity;
                    if (slotCount > MaxStageSize) return recipe.Name + "需要的卡牌超过组合槽上限。";
                }
                if (slotCount == 0 || recipe.Automatic && (slotCount != 2 || !string.IsNullOrEmpty(recipe.LocationId)))
                    return recipe.Name + "的配方缺少有效输入，或自动配方不是两牌接触。";
                if (!ValidOutputs(recipe.Outputs, cardMap)) return recipe.Name + "的产物或概率配置有误。";
            }
            var auxiliaryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (VoyageAuxiliaryRule line in definition.AuxiliaryLines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.Id) || !auxiliaryIds.Add(line.Id) || !Has(cardMap, line.CardId) ||
                    cardMap[line.CardId].Kind == VoyageCardKind.Body ||
                    !string.IsNullOrEmpty(line.RequiredInputId) && !Has(cardMap, line.RequiredInputId) || line.DurabilityCost < 0 ||
                    line.DurabilityCost > cardMap[line.CardId].Durability || !Finite(line.OutputMultiplier) || line.OutputMultiplier < 0 ||
                    !Finite(line.SanDelta) || !ValidOutputs(line.Outputs, cardMap))
                    return "副产线 ID、关联卡牌、耐久或产物无效。";
            }
            foreach (VoyageStartCard start in definition.StartCards)
            {
                if (start == null || !Has(cardMap, start.CardId) || !OnDesk(start.X, start.Y) ||
                    cardMap[start.CardId].Kind == VoyageCardKind.Injury || cardMap[start.CardId].Kind == VoyageCardKind.Ending)
                    return "初始牌的 ID、类型或位置无效。";
            }
            return "";
        }

        private static bool ValidOutputs(IReadOnlyList<VoyageOutputRule> output, Dictionary<string, VoyageCardRule> catalog)
        {
            if (output == null || output.Any(row => row == null || !Has(catalog, row.CardId) ||
                catalog[row.CardId].Kind == VoyageCardKind.Body || row.Quantity <= 0 || row.Quantity > 100 ||
                !Finite(row.Chance) || row.Chance < 0 || row.Chance > 1)) return false;
            return output.Where(row => !string.IsNullOrEmpty(row.Group)).GroupBy(row => row.Group).All(group => group.Sum(row => row.Chance) <= 1.0000001);
        }

        private static bool Has<T>(Dictionary<string, T> map, string id) => !string.IsNullOrEmpty(id) && map.ContainsKey(id);

        private string ValidateState(VoyageState s)
        {
            if (s == null || s.Cards == null || s.Jobs == null || s.Staged == null || s.Known == null ||
                s.RandomState == 0 || s.Minute < 0 || s.NextCardId <= 0 || s.NextJobId <= 0 ||
                !Finite(s.Hunger) || !Finite(s.San) || s.Hunger != Level(s.Hunger) || s.San != Level(s.San) ||
                s.Dead != (s.Hunger == 0 || s.San == 0) || (s.Dead || s.Won) && !s.Paused)
                return "本局状态或生存水位无效。";

            var instances = new Dictionary<int, VoyageCardState>();
            foreach (VoyageCardState card in s.Cards)
            {
                if (card == null || card.Id <= 0 || card.Id >= s.NextCardId || instances.ContainsKey(card.Id) || !Has(cards, card.CardId) || !OnDesk(card.X, card.Y))
                    return "桌面有重复、未知或越界的卡牌。";
                VoyageCardRule rule = cards[card.CardId];
                if (rule.InjuryLevel == 3 || card.JobId < 0 || card.Durability < 0 || card.Durability > rule.Durability ||
                    rule.Durability > 0 && card.Durability == 0 && rule.Kind != VoyageCardKind.Body ||
                    card.Charges < 0 || card.Charges > rule.Charges || card.RechargeRemaining < 0 || card.RechargeRemaining > rule.RechargeMinutes ||
                    card.RechargeRemaining > 0 && card.Charges >= rule.Charges ||
                    card.TimerRemaining < 0 || card.TimerRemaining > rule.TimerMinutes || rule.TimerMinutes > 0 && card.TimerRemaining == 0)
                    return rule.Name + "的实例计时、耐久或采集次数无效。";
                instances.Add(card.Id, card);
            }
            foreach (VoyageCardState card in s.Cards)
            {
                VoyageCardRule rule = cards[card.CardId];
                if (card.Limb == 0 && rule.Kind != VoyageCardKind.Injury) continue;
                if (!instances.TryGetValue(card.Limb, out VoyageCardState limb) || string.IsNullOrEmpty(rule.LimbId) || limb.CardId != rule.LimbId)
                    return "有伤势或思绪找不到对应的身体牌。";
            }
            foreach (VoyageCardRule body in rules.Cards.Where(card => card.Kind == VoyageCardKind.Body))
                if (rules.StartCards.Count(start => start.CardId == body.Id) != s.Cards.Count(card => card.CardId == body.Id))
                    return "身体牌实例数量与开局规则不符。";
            if (s.Won != (!s.Dead && s.Cards.Any(card => cards[card.CardId].Kind == VoyageCardKind.Ending))) return "结局状态与桌面不一致。";
            if (s.Known.Any(id => !Has(recipes, id)) || s.Known.Distinct().Count() != s.Known.Count ||
                rules.Recipes.Any(recipe => recipe.InitiallyKnown && !s.Known.Contains(recipe.Id)) ||
                s.Staged.Count > MaxStageSize || s.Staged.Distinct().Count() != s.Staged.Count ||
                s.Staged.Any(id => !instances.TryGetValue(id, out VoyageCardState card) || card.JobId != 0))
                return "已知配方或准备中的组合槽无效。";

            var jobIds = new HashSet<int>();
            var locked = new HashSet<int>();
            foreach (VoyageJobState job in s.Jobs)
            {
                if (job == null || job.Id <= 0 || job.Id >= s.NextJobId || !jobIds.Add(job.Id) || !Has(recipes, job.RecipeId) ||
                    job.Remaining <= 0 || job.Remaining > job.Duration || job.Inputs == null || job.AuxiliaryIds == null)
                    return "进行中的配方记录无效。";
                VoyageRecipeRule recipe = recipes[job.RecipeId];
                if (string.IsNullOrEmpty(recipe.LocationId))
                {
                    if (job.Location != 0) return "进行中的配方占用了错误的场所。";
                }
                else if (!instances.TryGetValue(job.Location, out VoyageCardState location) || location.JobId != job.Id ||
                    !LocationMatches(recipe, location) || !locked.Add(location.Id)) return "进行中的配方找不到原有场所。";

                var slotCounts = new int[recipe.Inputs.Count];
                var main = new List<VoyageCardState>();
                var lines = new List<string>();
                foreach (VoyageJobInput input in job.Inputs)
                {
                    if (input == null || !instances.TryGetValue(input.CardId, out VoyageCardState card) || card.JobId != job.Id || !locked.Add(card.Id))
                        return "同一张卡被重复使用，或任务找不到投入的卡。";
                    if (input.Slot == AttachedSlot)
                    {
                        if (string.IsNullOrEmpty(input.AuxiliaryId))
                        {
                            if (cards[card.CardId].Kind != VoyageCardKind.Injury) return "任务附带的状态牌无效。";
                        }
                        else
                        {
                            if (!Has(auxiliaries, input.AuxiliaryId) || !AppliesToRecipe(auxiliaries[input.AuxiliaryId], recipe) ||
                                !LineCardMatches(auxiliaries[input.AuxiliaryId], card)) return "任务引用的副产线无效。";
                            lines.Add(input.AuxiliaryId);
                        }
                    }
                    else
                    {
                        if (input.Slot < 0 || input.Slot >= slotCounts.Length || !string.IsNullOrEmpty(input.AuxiliaryId) || !InputMatches(recipe.Inputs[input.Slot], card))
                            return "任务中的投入与配方槽位不符。";
                        slotCounts[input.Slot]++;
                        main.Add(card);
                    }
                }
                if (!recipe.Inputs.Select(input => input.Quantity).SequenceEqual(slotCounts) || !lines.SequenceEqual(job.AuxiliaryIds) ||
                    job.Duration != Duration(recipe, lines)) return "任务的投入、耗时或副产线数量不符。";
                var limbs = new HashSet<int>(main.Where(card => cards[card.CardId].Kind == VoyageCardKind.Body).Select(card => card.Id));
                foreach (VoyageJobInput input in job.Inputs)
                {
                    VoyageCardState card = instances[input.CardId];
                    if ((input.Slot == AttachedSlot && card.Limb != 0 || input.Slot >= 0 && recipe.Inputs[input.Slot].MatchLimb) && !limbs.Contains(card.Limb))
                        return "进行中的配方关联了别处的伤势。";
                    if (!string.IsNullOrEmpty(input.AuxiliaryId))
                    {
                        string requiredId = auxiliaries[input.AuxiliaryId].RequiredInputId;
                        if (!string.IsNullOrEmpty(requiredId) && !main.Any(card => card.CardId == requiredId)) return "任务缺少副产线的基础卡牌。";
                    }
                }
                if (s.Cards.Any(card => limbs.Contains(card.Limb) && card.JobId != job.Id && cards[card.CardId].Kind == VoyageCardKind.Injury))
                    return "进行中的身体牌没有锁定关联伤势。";
            }
            return s.Cards.Any(card => card.JobId != 0 && !locked.Contains(card.Id)) ? "有卡牌锁在不存在的任务中。" : "";
        }
    }
}
