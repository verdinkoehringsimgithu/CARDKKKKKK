using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private static string CheckData(object value, string path, int depth = 0)
        {
            if (value == null) return "配置或存档字段为空：" + path;
            if (depth > 20) return "数据层级过深。";
            if (value is double d) return Finite(d) ? "" : "数值不是有限数：" + path;
            if (value is float f) return !float.IsNaN(f) && !float.IsInfinity(f) ? "" : "数值不是有限数：" + path;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string) return "";
            if (value is IList list)
            {
                if (list.Count > 10000) return "数据列表超出上限：" + path;
                for (int i = 0; i < list.Count; i++) { string error = CheckData(list[i], path + "[" + i + "]", depth + 1); if (error != "") return error; }
                return "";
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                // 可选字符串允许未填写，容器和对象必须存在。
                object item = field.GetValue(value); if (item == null && field.FieldType == typeof(string)) continue;
                string error = CheckData(item, path + "." + field.Name, depth + 1); if (error != "") return error;
            }
            return "";
        }
        public static string ValidateDefinition(SurvivalDefinition rules)
        {
            string error = CheckData(rules, "规则"); if (error != "") return error;
            if (rules.Version != 1 || rules.Cards.Count == 0 || rules.Recipes.Count == 0) return "规则版本或卡牌/配方目录无效。";
            if (rules.StartCapacity != 24 || rules.Columns != 8 || rules.Rows != 6 || rules.CellWidth <= 0 || rules.CellHeight <= 0 || rules.Width < rules.Columns * rules.CellWidth || rules.Height < rules.Rows * rules.CellHeight) return "占地模板需要24/32/40单位、8列6行候选位置及足够的逻辑尺寸。";
            if (rules.RealSecondsPerMinute <= 0 || rules.MaxMinutesPerAdvance < 1 || rules.StartMinute < 0 || rules.StartMinute >= 1440) return "时间配置无效。";
            var cards = new HashSet<string>(); var recipes = new HashSet<string>(); var traits = new HashSet<string>(); var afflictions = new HashSet<string>();
            foreach (CardRule c in rules.Cards)
            {
                if (string.IsNullOrWhiteSpace(c.Id) || !cards.Add(c.Id)) return "卡牌ID为空或重复：" + c.Id;
                if (string.IsNullOrWhiteSpace(c.Name) || !Enum.IsDefined(typeof(CardKind), c.Kind) || !Enum.IsDefined(typeof(CardRole), c.Role)) return "卡牌显示名或类型无效：" + c.Id;
                if (c.StackLimit < 1 || c.StackLimit > 20 || c.Kind != CardKind.Material && c.StackLimit != 1) return "堆叠上限无效：" + c.Id;
                if (c.Footprint != (c.Role == CardRole.NaturalNode || c.Kind == CardKind.Condition ? 0 : c.Kind == CardKind.Area ? 4 : 1)) return "卡牌占地与类型不符：" + c.Id;
                if (c.Nutrition < 0 || c.ShelfMinutes < 0 || c.FuelValue < 0 || c.OrganicWeight < 0 || c.InitialDurability < 0 || c.Hardness < 0 || c.Structure < 0 || c.DailyWear < 0 || c.WorkWear < 0 || c.FuelCapacity < 0 || c.AshEveryFuel < 0 || c.RepairMinutes < 0 || c.RepairLoss < 0 || c.SpecialtyEfficiency <= 0 || c.CarryingCapacity <= 0 || c.LoadPower <= 0) return "卡牌规则含非法数值：" + c.Id;
                if (c.InputSlots < 0 || c.InputSlots > 6 || c.OutputSlots < 0 || c.OutputSlots > 6 || c.PlantSlots < 0 || c.PlantSlots > 2 || c.AdultSlots < 0 || c.AdultSlots > 2 || c.YoungSlots < 0 || c.YoungSlots > 2 || c.StorageSlots < 0 || c.StorageSlots > 6) return "槽位数量超出界面支持：" + c.Id;
                if (!string.IsNullOrEmpty(c.Ability) && c.Ability != "cut") return "尚未实现的工具能力：" + c.Ability;
            }
            Func<List<Amount>, bool> validAmounts = xs => xs.All(a => cards.Contains(a.DefinitionId) && a.Count > 0 && (string.IsNullOrEmpty(a.TraitId) || rules.Traits.Any(t => t.Id == a.TraitId)));
            foreach (CardRule c in rules.Cards)
                if (!validAmounts(c.BuildingCost) || !validAmounts(c.Salvage) || !string.IsNullOrEmpty(c.DecayId) && !cards.Contains(c.DecayId) || !string.IsNullOrEmpty(c.RepairToolId) && !cards.Contains(c.RepairToolId) || !string.IsNullOrEmpty(c.AshId) && !cards.Contains(c.AshId)) return "卡牌引用无效：" + c.Id;
            foreach (RecipeRule r in rules.Recipes)
            {
                if (string.IsNullOrWhiteSpace(r.Id) || !recipes.Add(r.Id)) return "配方ID为空或重复：" + r.Id;
                if (!Enum.IsDefined(typeof(RecipeKind), r.Kind) || !Enum.IsDefined(typeof(CareKind), r.Care) || r.Minutes <= 0 || r.AutomaticMinutes < 0 || r.ExtraTraitMinutes < 0 || r.Physical < 0 || r.Cognitive < 0 || r.Danger < 0 || r.Fuel < 0 || r.ToolWear < 0 || r.RequiredHardness < 0 || r.Exposure < 0 || r.RepairAmount < 0) return "配方数值无效：" + r.Id;
                if (!validAmounts(r.Outputs) || !validAmounts(r.ExtraTraitCost) || r.TargetDefinitions.Any(id => !cards.Contains(id))) return "配方产出或对象引用无效：" + r.Id;
                if (r.Inputs.Any(i => i.Count < 1 || i.Alternatives.Count == 0 || i.Alternatives.Any(id => !cards.Contains(id)))) return "配方输入引用无效：" + r.Id;
                if (!string.IsNullOrEmpty(r.DiscoverMaterial) && r.DiscoverMaterial.Split('|').Any(id => !cards.Contains(id))) return "发现条件引用不存在：" + r.Id;
                if (!string.IsNullOrEmpty(r.RequiredAbility) && r.RequiredAbility != "cut") return "尚未实现的配方能力：" + r.Id;
                if ((int)r.Tags < 0 || ((int)r.Tags & ~63) != 0) return "尚未实现的劳动标签：" + r.Id;
            }
            foreach (TraitRule t in rules.Traits) if (string.IsNullOrWhiteSpace(t.Id) || !traits.Add(t.Id) || t.DurationMultiplier <= 0 || !validAmounts(t.AddedOutputs)) return "性状配置无效：" + t.Id;
            foreach (NodeRule n in rules.Nodes) if (!cards.Contains(n.CardId) || !validAmounts(n.Initial) || !validAmounts(n.Daily) || !validAmounts(n.Capacity) || n.RefreshMinutes <= 0) return "自然节点配置无效：" + n.CardId;
            if (rules.Nodes.Select(n => n.CardId).Distinct().Count() != rules.Nodes.Count || rules.Biology.Select(b => b.Role).Distinct().Count() != rules.Biology.Count) return "自然节点或生物规则重复。";
            foreach (BiologyRule b in rules.Biology) if (!validAmounts(b.Feed) || !validAmounts(b.Production) || !validAmounts(b.Death) || b.GrowMinutes < 0 || b.ProductionMinutes < 0 || b.MatureHoldMinutes < 0 || b.FeedWindowMinutes <= 0 || b.StoredLimit < 1 || !string.IsNullOrEmpty(b.SeedId) && !cards.Contains(b.SeedId) || !string.IsNullOrEmpty(b.MatureId) && !cards.Contains(b.MatureId) || !string.IsNullOrEmpty(b.ProductionItem) && !cards.Contains(b.ProductionItem)) return "生物规则无效：" + b.Role;
            foreach (AfflictionRule a in rules.Afflictions) if (string.IsNullOrWhiteSpace(a.Id) || !afflictions.Add(a.Id) || a.Growth < 0 || a.Clearance < 0 || a.FunctionExponent <= 0 || a.CurvePower <= 0 || a.CognitiveMultiplier < 0 || !Enum.IsDefined(typeof(AfflictionKind), a.Kind)) return "异常配置无效：" + a.Id;
            if (rules.Afflictions.Any(a => new[] { a.StructureWeight, a.FunctionWeight, a.VitalWeight }.Any(v => v < 0 || v > 1) || new[] { a.StructureThreshold, a.FunctionThreshold, a.VitalThreshold }.Any(v => v < 0 || v >= 1) || a.CognitiveMultiplier < 1 || (int)a.WorkTags < 0 || ((int)a.WorkTags & ~63) != 0)) return "异常权重、阈值或行动标签无效。";
            if (rules.Cards.Count(c => c.Kind == CardKind.Condition && c.WoundCard) != 1 || rules.Afflictions.Any(a => rules.Cards.Count(c => c.Kind == CardKind.Condition && c.ConditionRuleId == a.Id) != 1)) return "每类伤口/异常都需唯一的独立状态牌定义。";
            if (rules.Cards.Any(c => c.Kind == CardKind.Condition && (c.Role != CardRole.None || c.ShelfMinutes != 0 || c.WoundCard && !string.IsNullOrEmpty(c.ConditionRuleId) || !c.WoundCard && !afflictions.Contains(c.ConditionRuleId)))) return "状态牌定义与异常类型不符。";
            if (rules.Recipes.Any(r => r.Inputs.Any(i => i.Alternatives.Any(id => rules.Cards.Find(c => c.Id == id).Kind == CardKind.Condition)) || r.Outputs.Any(a => rules.Cards.Find(c => c.Id == a.DefinitionId).Kind == CardKind.Condition)) || rules.Start.Any(a => rules.Cards.Find(c => c.Id == a.DefinitionId)?.Kind == CardKind.Condition)) return "状态牌只能由真实事件和异常生命周期生成，不能当普通材料生产。";
            if (!validAmounts(rules.FirstTide) || !validAmounts(rules.TideFixed) || !validAmounts(rules.TideRandom) || rules.TidePeriodic.Any(p => p.EveryDays < 1 || !validAmounts(p.Items))) return "潮汐物资配置无效。";
            if (rules.Start.Any(a => !cards.Contains(a.DefinitionId) || a.Count < 1)) return "开局物资配置无效。";
            if (rules.Expansions.Count != 2 || rules.Expansions[0].Capacity != 32 || rules.Expansions[1].Capacity != 40 || rules.Expansions.Any(e => e.Minutes <= 0 || !validAmounts(e.Cost))) return "扩地需依次达到32、40单位。";
            BodyRules body = rules.Body;
            if (typeof(BodyRules).GetFields().Where(f => f.FieldType == typeof(double)).Any(f => (double)f.GetValue(body) < 0) || body.FunctionFloor > 1 || body.ExposureWarning > 1 || body.FoodWarning > 1 || body.VitalWarning > 1 || body.ForcedInjuryChance >= 1 || body.InjuryStructure > 1 || body.InjuryVital > 1) return "身体参数必须为合法非负率、比例或概率。";
            if (body.HealthWeight < 0 || body.HealthWeight > 1 || body.NutritionHalf <= 0 || body.PollutionHalf <= 0 || body.TreatmentHalf <= 0 || body.WorsenHours <= 0 || body.RecoverHours <= 0 || body.EfficiencyMin <= 0 || body.EfficiencyMax < body.EfficiencyMin || body.TreatmentMax <= 0) return "身体曲线分母或范围无效。";
            if (rules.Goal.WindowDays < 1 || rules.Goal.WindowDays > 8 || rules.Goal.MinimumDay < rules.Goal.WindowDays + 1 || !cards.Contains(rules.Goal.BoneKnifeId) || !cards.Contains(rules.Goal.WasteId) || !recipes.Contains(rules.Goal.CompostRecipeId) || !recipes.Contains(rules.Goal.AcceleratedCompostId)) return "目标条件引用或窗口无效。";
            return "";
        }
        private string ValidateState(SurvivalState s)
        {
            string error = CheckData(s, "状态"); if (error != "") return error;
            if (s.Version != def.Version || s.Minute < def.StartMinute || s.NextId < 1 || s.RandomState == 0 || (s.Capacity != 24 && s.Capacity != 32 && s.Capacity != 40) || s.ExpansionLevel < 0 || s.ExpansionLevel > 2 || s.Capacity != (s.ExpansionLevel == 0 ? 24 : def.Expansions[s.ExpansionLevel - 1].Capacity) || (s.Speed != 1 && s.Speed != 3)) return "存档时间、容量或版本无效。";
            var ids = new HashSet<string>(); var locks = new HashSet<string>();
            foreach (ItemState i in s.Items)
            {
                CardRule card = Card(i.DefinitionId);
                if (card == null || string.IsNullOrEmpty(i.Id) || !ids.Add(i.Id)) return "卡牌实例引用或ID无效。";
                if (i.OwnerId == null || i.TraitId == null || i.ConditionId == null || i.RepeatRecipe == null || i.RepeatSecondaryId == null || i.BornAt > s.Minute) return "卡牌归属或出生时刻无效。";
                if (i.Quantity < 0 || i.Quantity > (card.Kind == CardKind.Material ? card.StackLimit : 1) || i.Quantity == 0 && !s.Reservations.Any(r => r.StackId == i.Id)) return "卡牌数量无效：" + i.Id;
                if (i.Lots.Any(l => l.Count < 1 || l.ExpiresAt < 0) || i.Lots.Sum(l => l.Count) != i.Quantity) return "材料批次数量不一致：" + i.Id;
                if (!Enum.IsDefined(typeof(Place), i.Place) || i.Durability < 0 || i.MaxDurability < 0 || i.Durability > i.MaxDurability || i.MaxDurability > card.InitialDurability || i.Structure < 0 || i.Structure > card.Structure || i.Health < 0 || i.Health > 100 || i.Growth < 0 || i.Production < 0 || i.Fuel < 0 || i.Fuel > card.FuelCapacity + .0001 || i.AshRemainder < 0) return "卡牌实例状态无效：" + i.Id;
                if (!string.IsNullOrEmpty(i.TraitId) && !def.Traits.Any(t => t.Id == i.TraitId)) return "性状引用不存在。";
                if (i.Stock.Any(a => Card(a.DefinitionId) == null || a.Count < 0) || i.Reserves.Any(a => Card(a.DefinitionId) == null || a.Count < 0)) return "库存或保留量无效。";
            }
            foreach (ItemState i in s.Items)
            {
                if (Card(i.DefinitionId).Kind == CardKind.Condition)
                {
                    CardRule condition = Card(i.DefinitionId);
                    if (i.Quantity != 1 || i.TraitId != "" || i.Repeat || i.Stock.Count != 0 || i.Reserves.Count != 0 || i.Lots.Count != 1 || i.Lots[0].ExpiresAt != 0) return "独立状态牌实例不能堆叠、腐败或循环生产。";
                    if (i.OwnerId != "" || i.Place != Place.Table || string.IsNullOrEmpty(i.ConditionId) || !Fits(s, i.DefinitionId, i.X, i.Y, i.Id) || s.Items.Any(other => other.Id != i.Id && other.ConditionId == i.ConditionId)) return "独立状态牌归属或实例引用无效。";
                    if (condition.WoundCard ? !s.Body.Injuries.Any(w => w.Id == i.ConditionId && w.Tags != WorkTag.None) : !s.Body.Afflictions.Any(a => a.Id == i.ConditionId && a.RuleId == condition.ConditionRuleId)) return "状态牌对应的伤口或异常不存在。";
                    continue;
                }
                if (!string.IsNullOrEmpty(i.ConditionId)) return "普通卡牌不能引用身体状态。";
                if (i.Place == Place.Equipment) { if (i.OwnerId != "player" || Card(i.DefinitionId).Role != CardRole.Ornament) return "装备归属无效。"; continue; }
                if (Root(i)) { if (Units(i.DefinitionId) > 0 && !Fits(s, i.DefinitionId, i.X, i.Y, i.Id)) return "卡牌位置重叠或越界：" + i.Id; continue; }
                ItemState owner = Item(s, i.OwnerId); if (owner == null || owner == i || FindRootItem(s, i) == null) return "卡牌归属不存在或循环。";
                if (i.Place == Place.Residue) continue;
                if (!Accepts(owner, Card(i.DefinitionId), i.Place) || i.Slot < 0 || i.Slot >= Slots(Card(owner.DefinitionId), i.Place)) return "容器槽位或卡牌类型不兼容：" + i.Id;
                if (s.Items.Any(other => other.Id != i.Id && other.OwnerId == i.OwnerId && other.Slot == i.Slot && (other.Place == i.Place || other.Place == Place.Residue && other.ResidueFrom == i.Place))) return "同一槽位被重复占用。";
            }
            if (s.Items.Count(i => i.Place == Place.Equipment) > 1 || Used(s) > s.Capacity) return "装备或桌面容量超限。";
            if (s.Body.Injuries.Where(w => w.Tags != WorkTag.None).Any(w => s.Items.Count(i => i.ConditionId == w.Id) != 1) || s.Body.Afflictions.Any(a => s.Items.Count(i => i.ConditionId == a.Id) != 1)) return "伤口/异常缺少唯一对应的独立状态牌。";
            if (s.Tasks.Count(t => t.Human && !t.Suspended) > 1 || s.Tasks.Count(t => t.Suspended) > 1) return "只有一个工作人力槽，休息时最多暂放一项工作。";
            foreach (TaskState t in s.Tasks)
            {
                if (string.IsNullOrEmpty(t.Id) || !ids.Add(t.Id) || Recipe(t.RecipeId) == null || t.WorkMinutes <= 0 || t.Progress < 0 || t.Progress > 1.000001 || t.Remaining < 1 || t.Remaining > 1000 || t.Stage < 0 || t.Stage > 1 || t.StartedAt > s.Minute || t.StartedAt < def.StartMinute) return "任务状态无效。";
                if (!string.IsNullOrEmpty(t.TargetId) && Item(s, t.TargetId) == null || !string.IsNullOrEmpty(t.SecondaryId) && Item(s, t.SecondaryId) == null) return "任务目标已不存在。";
                foreach (string id in t.LockedItems.Concat(new[] { t.TargetId, t.SecondaryId }).Where(id => !string.IsNullOrEmpty(id)).Distinct())
                    if (Item(s, id) == null || !locks.Add(id)) return "任务独占对象不存在或被重复使用。";
                if (t.Consumed.Any(a => Card(a.DefinitionId) == null || a.Count < 1) || t.QueueWounds.Any(w => !s.Body.Injuries.Any(i => i.Id == w))) return "任务消耗/护理队列引用无效。";
                if (t.Contacts.Any(c => string.IsNullOrEmpty(c.SourceId) || c.Load <= 0) || t.Contacts.Select(c => c.SourceId).Distinct().Count() != t.Contacts.Count) return "劳动接触来源或负荷无效。";
                if (!string.IsNullOrEmpty(t.WoundId) && !s.Body.Injuries.Any(w => w.Id == t.WoundId && w.Tags != WorkTag.None) || !string.IsNullOrEmpty(t.PreferredMaterialId) && Card(t.PreferredMaterialId)?.Kind != CardKind.Material) return "护理或选定材料引用无效。";
            }
            foreach (Reservation r in s.Reservations)
            {
                if (string.IsNullOrEmpty(r.Id) || !ids.Add(r.Id) || !s.Tasks.Any(t => t.Id == r.TaskId) || Card(r.DefinitionId) == null || r.Count < 1) return "产出预留引用无效。";
                if (r.Count > (Card(r.DefinitionId).Kind == CardKind.Material ? Card(r.DefinitionId).StackLimit : 1) || !Enum.IsDefined(typeof(Place), r.Place)) return "产出预留数量或位置无效。";
                if (r.StackId != "")
                { ItemState stack = Item(s, r.StackId); if (stack == null || stack.DefinitionId != r.DefinitionId || stack.OwnerId != r.OwnerId || stack.Place != r.Place || stack.Quantity + s.Reservations.Where(x => x.StackId == r.StackId).Sum(x => x.Count) > Card(r.DefinitionId).StackLimit) return "预留材料堆无效。"; }
                else if (r.OwnerId == "" && r.Place == Place.Table) { if (!Fits(s, r.DefinitionId, r.X, r.Y, r.Id, r.ReplacesItemId)) return "预留产出位置重叠。"; }
                else if (Item(s, r.OwnerId) == null || r.Slot < 0 || r.Slot >= Slots(Card(Item(s, r.OwnerId).DefinitionId), r.Place)) return "预留区域槽位无效。";
                else if (s.Items.Any(i => i.OwnerId == r.OwnerId && i.Place == r.Place && i.Slot == r.Slot) || s.Reservations.Any(other => other.Id != r.Id && other.StackId == "" && other.OwnerId == r.OwnerId && other.Place == r.Place && other.Slot == r.Slot)) return "产出槽预留重复占用。";
                if (!string.IsNullOrEmpty(r.ReplacesItemId))
                {
                    TaskState replacing = s.Tasks.Find(t => t.Id == r.TaskId); RecipeKind kind = Recipe(replacing.RecipeId).Kind;
                    if (replacing.TargetId != r.ReplacesItemId || Item(s, r.ReplacesItemId) == null || kind != RecipeKind.Salvage && kind != RecipeKind.Demolish && kind != RecipeKind.Destroy) return "原位替换预留无效。";
                }
            }
            BodyState b = s.Body;
            if (new[] { b.Food, b.Mind, b.Integrity, b.Function, b.Vitality }.Any(v => v < -0.000001 || v > 1.000001)) return "身体状态越界。";
            foreach (InjuryState wound in b.Injuries) if (string.IsNullOrEmpty(wound.Id) || !ids.Add(wound.Id) || wound.Deficit < 0 || wound.Cleanliness < 0 || wound.Cleanliness > 1 || wound.DressingStrength < 0 || wound.CareStrength < 0) return "伤口状态无效。";
            if (Math.Abs(b.Injuries.Sum(i => i.Deficit) - (1 - b.Integrity)) > .00001) return "损伤来源份额与结构健康不一致。";
            foreach (AfflictionState a in b.Afflictions) if (string.IsNullOrEmpty(a.Id) || !ids.Add(a.Id) || !def.Afflictions.Any(rule => rule.Id == a.RuleId) || a.Severity < 0 || a.Severity > 1 || a.Input < 0 || !string.IsNullOrEmpty(a.InjuryId) && !b.Injuries.Any(i => i.Id == a.InjuryId)) return "异常与伤口引用无效。";
            if (b.Exposures.Any(e => string.IsNullOrEmpty(e.Source) || e.Accumulated < 0 || e.Hazard < 0 || e.Threshold <= 0) || b.Exposures.Select(e => e.Source).Distinct().Count() != b.Exposures.Count) return "暴露来源或风险进度无效。";
            if (s.History.Any(h => h.Minute < def.StartMinute || h.Minute > s.Minute || Card(h.Key) == null && h.Key != "body:food" && h.Key != "body:mind" && h.Key != "body:vital" && h.Key != "body:health")) return "资源变化记录无效。";
            if (s.Dead && !s.Paused || s.Discovered.Any(id => Card(id) == null) || s.Days.Any(d => d.Day < 1 || d.Food < 0 || d.Compost < 0 || d.Waste < 0) || s.Tide.Any(a => Card(a.DefinitionId) == null || a.Count < 1)) return "发现、结算或每日记录无效。";
            long maximum = 0;
            foreach (string id in ids)
            { int start = id.Length; while (start > 0 && char.IsDigit(id[start - 1])) start--; if (start < id.Length && long.TryParse(id.Substring(start), out long suffix)) maximum = Math.Max(maximum, suffix); }
            if (s.NextId <= maximum) return "实例序号落后于存档。";
            return "";
        }
    }
}
