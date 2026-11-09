using System;
using System.Linq;
using System.Collections.Generic;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private double WorkEfficiency(SurvivalState s, RecipeRule recipe)
        {
            BodyState b = s.Body;
            double damage = b.Injuries.Where(w => (w.Tags & recipe.Tags) != 0).Sum(w => w.Deficit);
            double other = 0;
            foreach (AfflictionState a in b.Afflictions)
            {
                AfflictionRule rule = def.Afflictions.Find(r => r.Id == a.RuleId);
                if (rule == null || rule.StructureWeight > 0 || rule.FunctionWeight > 0 || rule.VitalWeight > 0) continue;
                if (rule.WorkTags != WorkTag.None && (rule.WorkTags & recipe.Tags) == 0) continue;
                // A cognitive modifier is paid in the cognitive channel, never a second efficiency penalty.
                if (rule.CognitiveMultiplier == 1) other = 1 - (1 - other) * (1 - Clamp(a.Severity));
            }
            BodyRules p = def.Body;
            double denominator = 1 + p.StructureEfficiency * Math.Pow(Clamp(damage), p.EfficiencyPower)
                + p.FunctionEfficiency * Math.Pow(1 - b.Function, p.EfficiencyPower)
                + p.MindEfficiency * Math.Pow(1 - b.Mind, p.EfficiencyPower) + p.OtherEfficiency * other;
            // Tool speciality and the final boundary are applied once by TaskEfficiency.
            return 1 / denominator;
        }

        private void ApplyWork(SurvivalState s, RecipeRule recipe, double hours, string source)
        {
            if (hours <= 0) return;
            BodyState b = s.Body;
            BodyRules p = def.Body;
            if (recipe.Kind == RecipeKind.Rest || (recipe.Tags & WorkTag.Rest) != 0)
            {
                double condition = 1;
                foreach (ItemState i in s.Items.Where(i => i.Place == Place.Equipment))
                    condition += Math.Max(0, Card(i.DefinitionId).OrnamentRestBonus);
                condition = Clamp(condition, 1, p.EfficiencyMax);
                b.Mind = Clamp(1 - (1 - b.Mind) * Math.Exp(-p.RestRate * condition * hours));
                return;
            }
            WorkRates(s, recipe, out double foodRate, out double mindRate);
            double food = foodRate * hours;
            double mind = mindRate * hours;
            double missing = Math.Max(0, food - b.Food);
            b.Food = Clamp(b.Food - food);
            b.Mind = Clamp(b.Mind - mind);
            if (missing > 0)
            {
                AfflictionState hunger = EnsureAffliction(s, AfflictionKind.Hunger, "", source);
                if (hunger != null) hunger.Input += p.HungerInput * missing * 60;
            }
            TaskState active = s.Tasks.Find(t => t.Human && !t.Suspended && t.RecipeId == recipe.Id);
            if (active != null) { active.SpentFood += food; active.SpentMind += mind; }
            ApplyContact(s, recipe, hours, source);
            if (b.Mind <= 0)
            {
                s.Paused = true;
                AddLog(s, "精神不足，劳动已暂停。休息后可以继续。");
            }
            WarnBody(s);
        }

        private void WorkRates(SurvivalState s, RecipeRule recipe, out double food, out double mind)
        {
            food = mind = 0;
            if (!recipe.Human || recipe.Kind == RecipeKind.Rest) return;
            BodyState b = s.Body; BodyRules p = def.Body;
            double cognitive = 1;
            foreach (AfflictionState a in b.Afflictions)
            {
                AfflictionRule rule = def.Afflictions.Find(r => r.Id == a.RuleId);
                if (rule == null || rule.CognitiveMultiplier == 1) continue;
                InjuryState wound = b.Injuries.Find(w => w.Id == a.InjuryId);
                WorkTag tags = wound == null ? rule.WorkTags : wound.Tags;
                if (tags != WorkTag.None && (tags & recipe.Tags) == 0) continue;
                cognitive += (Math.Max(1, rule.CognitiveMultiplier) - 1) * Clamp(a.Severity);
            }
            cognitive = Clamp(cognitive, 1, p.EfficiencyMax);
            food = p.FoodRate * Math.Pow(Math.Max(0, recipe.Physical), p.PhysicalPower);
            mind = p.MindRate * (Math.Pow(Math.Max(0, recipe.Cognitive), p.CognitivePower)
                + p.DangerMindWeight * Math.Pow(Math.Max(0, recipe.Danger), p.DangerPower)) * cognitive;
        }

        private AfflictionState EnsureAffliction(SurvivalState s, AfflictionKind kind, string injury, string source)
        {
            AfflictionRule rule = def.Afflictions.Find(r => r.Kind == kind);
            if (rule == null) return null;
            AfflictionState result = s.Body.Afflictions.Find(a => a.RuleId == rule.Id && a.InjuryId == injury);
            if (result == null)
            {
                result = new AfflictionState { Id = NewId(s, "a"), RuleId = rule.Id, InjuryId = injury, Source = source };
                s.Body.Afflictions.Add(result);
            }
            return result;
        }

        private void CreateInjury(SurvivalState s, string source, WorkTag tags, double structure, double vital)
        {
            double actual = Math.Min(s.Body.Integrity, Math.Max(0, structure));
            InjuryState wound = new InjuryState { Id = NewId(s, "w"), Source = source, Name = source, Tags = tags, Deficit = actual };
            s.Body.Injuries.Add(wound);
            s.Body.Integrity = Clamp(s.Body.Integrity - actual);
            s.Body.Vitality = Clamp(s.Body.Vitality - Math.Max(0, vital));
            if (vital > 0) s.Body.LastVitalDamageSource = source + "造成的直接损伤";
            AfflictionState bleeding = EnsureAffliction(s, AfflictionKind.Bleeding, wound.Id, source);
            if (bleeding != null) bleeding.Severity = Clamp(actual);
            AfflictionState pain = EnsureAffliction(s, AfflictionKind.Pain, wound.Id, source);
            if (pain != null) pain.Severity = Clamp(actual);
            SyncConditionCards(s);
            s.Paused = true;
            AddLog(s, source + "造成伤口，请查看身体状态。");
        }

        private ExposureState Exposure(SurvivalState s, string source)
        {
            ExposureState exposure = s.Body.Exposures.Find(e => e.Source == source);
            if (exposure == null)
            {
                exposure = new ExposureState { Source = source, Threshold = -Math.Log(Math.Max(double.Epsilon, 1 - Random01(s))) };
                s.Body.Exposures.Add(exposure);
            }
            return exposure;
        }

        private void ApplyContact(SurvivalState s, RecipeRule recipe, double hours, string source)
        {
            BodyRules p = def.Body;
            TaskState task = s.Tasks.Find(t => t.Human && t.RecipeId == recipe.Id);
            ItemState target = task == null ? null : Item(s, task.TargetId);
            ItemState root = FindRootItem(s, target);
            string origin = task != null && task.WorkSourceId != "" ? task.WorkSourceId : root == null ? recipe.Id : root.Id;
            if (recipe.Exposure > 0)
            {
                double held = task == null ? 0 : task.Contacts.Sum(c => c.Load);
                if (held > 0)
                    foreach (WorkContact sourceContact in task.Contacts)
                    {
                        ItemState sourceRegion = Item(s, sourceContact.OwnerId);
                        ApplyPollutionContact(s, recipe, sourceContact.SourceId, sourceRegion == null ? recipe.Name : Card(sourceRegion.DefinitionId).Name,
                            sourceContact.Load + OrganicLoad(s, sourceRegion), hours * sourceContact.Load / held);
                    }
                else ApplyPollutionContact(s, recipe, origin, root == null ? recipe.Name : Card(root.DefinitionId).Name, OrganicLoad(s, root), hours);
            }
            if (recipe.Danger <= 0) return;
            ExposureState danger = Exposure(s, "danger:" + origin);
            if (s.Minute < danger.SafeUntil) return;
            double rate = p.RiskRate * Math.Pow(recipe.Danger, p.DangerPower);
            if (p.ForcedInjuryChance > 0 && p.ForcedInjuryChance < 1)
                rate += -Math.Log(1 - p.ForcedInjuryChance) * recipe.Danger / Math.Max(1.0 / 60, recipe.Minutes / 60);
            danger.Accumulated += recipe.Danger * hours;
            danger.Hazard += rate * hours;
            if (danger.Hazard < danger.Threshold) return;
            danger.Hazard -= danger.Threshold;
            danger.Threshold = -Math.Log(Math.Max(double.Epsilon, 1 - Random01(s)));
            danger.SafeUntil = s.Minute + (long)Math.Ceiling(p.SafeEventMinutes);
            CreateInjury(s, source, recipe.Tags, p.InjuryStructure, p.InjuryVital);
        }

        private void ApplyPollutionContact(SurvivalState s, RecipeRule recipe, string origin, string name, double load, double hours)
        {
            BodyRules p = def.Body;
            double contact = load / (p.PollutionHalf + load) * recipe.Exposure;
            if (contact <= 0 || hours <= 0) return;
            ExposureState exposure = Exposure(s, "contact:" + origin);
            exposure.Accumulated += contact * hours; exposure.Hazard += p.ExposureScale * contact * hours;
            if (!exposure.Warned && exposure.Hazard >= exposure.Threshold * p.ExposureWarning)
            {
                exposure.Warned = true; s.Paused = true;
                AddLog(s, "处理" + name + "的接触风险升高，可先清理积存物、处理伤口或停止作业。");
            }
            if (exposure.Hazard < exposure.Threshold) return;
            AfflictionState irritation = EnsureAffliction(s, AfflictionKind.Irritation, "", name);
            if (irritation != null) irritation.Severity = Clamp(irritation.Severity + p.ExposureScale * contact);
            foreach (InjuryState wound in s.Body.Injuries.Where(w => w.Tags != WorkTag.None && w.Deficit > 0))
            {
                AfflictionState infection = EnsureAffliction(s, AfflictionKind.Infection, wound.Id, name);
                if (infection != null) infection.Severity = Clamp(infection.Severity + p.ExposureScale * contact * (1 - wound.Cleanliness) / Treatment(wound.DressingStrength));
                wound.Cleanliness = Clamp(wound.Cleanliness - contact * p.DirtRate * hours);
            }
            exposure.Hazard -= exposure.Threshold; exposure.Threshold = -Math.Log(Math.Max(double.Epsilon, 1 - Random01(s))); exposure.Warned = false;
        }

        private double Treatment(double strength)
        {
            return 1 + def.Body.TreatmentMax * Math.Max(0, strength) / (def.Body.TreatmentHalf + Math.Max(0, strength));
        }

        private double Burden(SurvivalState s, int channel)
        {
            double remaining = 1;
            foreach (AfflictionState a in s.Body.Afflictions)
            {
                AfflictionRule r = def.Afflictions.Find(x => x.Id == a.RuleId);
                if (r == null) continue;
                double threshold = channel == 0 ? r.StructureThreshold : channel == 1 ? r.FunctionThreshold : r.VitalThreshold;
                double weight = channel == 0 ? r.StructureWeight : channel == 1 ? r.FunctionWeight : r.VitalWeight;
                double curve = Math.Pow(Clamp((a.Severity - threshold) / Math.Max(double.Epsilon, 1 - threshold)), r.CurvePower);
                remaining *= 1 - Clamp(weight) * curve;
            }
            return Clamp(1 - remaining);
        }

        private void TickBody(SurvivalState s)
        {
            const double dt = 1.0 / 60;
            BodyState b = s.Body;
            if (b.Vitality <= 0) { SyncConditionCards(s); return; }
            BodyRules p = def.Body;
            double oldI = b.Integrity, oldR = b.Function, oldV = b.Vitality;
            foreach (InjuryState wound in b.Injuries)
            {
                if (wound.DressingUntil <= s.Minute) { wound.DressingStrength = 0; wound.DressingId = ""; }
                if (wound.CareUntil <= s.Minute) wound.CareStrength = 0;
                if (wound.Tags != WorkTag.None && wound.Deficit > 0 && wound.DressingStrength <= 0)
                    wound.Cleanliness = Clamp(wound.Cleanliness - p.DirtRate * dt);
            }
            // All coupled channels read one snapshot; list traversal cannot change the minute's derivatives.
            double bi = Burden(s, 0), br = Burden(s, 1), bv = Burden(s, 2);
            var structureSources = new Dictionary<AfflictionState, double>();
            foreach (AfflictionState a in b.Afflictions)
            {
                AfflictionRule rule = def.Afflictions.Find(r => r.Id == a.RuleId);
                if (rule == null) continue;
                double curve = Math.Pow(Clamp((a.Severity - rule.StructureThreshold) / Math.Max(double.Epsilon, 1 - rule.StructureThreshold)), rule.CurvePower);
                double weight = Clamp(rule.StructureWeight) * curve;
                if (weight > 0) structureSources.Add(a, weight);
            }
            foreach (AfflictionState a in b.Afflictions.ToArray())
            {
                AfflictionRule r = def.Afflictions.Find(x => x.Id == a.RuleId);
                if (r == null) continue;
                InjuryState wound = b.Injuries.Find(w => w.Id == a.InjuryId);
                double strength = 0;
                if (wound != null)
                {
                    if (r.Kind == AfflictionKind.Bleeding) strength = wound.DressingStrength;
                    if (r.Kind == AfflictionKind.Infection) strength = wound.CareStrength;
                    if (r.Kind == AfflictionKind.Pain) strength = wound.CareStrength + wound.DressingStrength;
                }
                double z = a.Severity;
                double derivative = (Math.Max(0, a.Input) + r.Growth * z) * (1 - z)
                    - r.Clearance * Math.Pow(oldR, r.FunctionExponent) * Treatment(strength) * z;
                a.Severity = Clamp(z + derivative * dt);
                a.Input = 0;
                if (a.Severity < 0.00000001) b.Afflictions.Remove(a);
            }
            double nutrition = (1 + p.NutritionHalf) * b.Food / (p.NutritionHalf + b.Food);
            double care = b.Injuries.Count == 0 ? 1 : b.Injuries.Sum(w => w.Deficit * Treatment(w.CareStrength + w.DressingStrength)) / Math.Max(double.Epsilon, 1 - oldI);
            care = Clamp(care, 1, 1 + p.TreatmentMax);
            double repair = p.RepairRate * nutrition * oldR * care * (1 - oldI) * (1 - bi) * dt;
            double damage = Math.Min(oldI, Math.Max(0, p.StructureDamageRate * bi * oldI * dt));
            double actualRepair = Math.Min(1 - oldI, Math.Max(0, repair));
            double deficitBefore = b.Injuries.Sum(w => w.Deficit);
            if (deficitBefore > 0)
                foreach (InjuryState wound in b.Injuries) wound.Deficit *= Math.Max(0, 1 - actualRepair / deficitBefore);
            if (damage > 0)
            {
                double total = structureSources.Values.Sum();
                foreach (KeyValuePair<AfflictionState, double> source in structureSources)
                {
                    AfflictionState a = source.Key;
                    AfflictionRule r = def.Afflictions.Find(x => x.Id == a.RuleId);
                    if (r == null || total <= 0) continue;
                    InjuryState origin = b.Injuries.Find(w => w.Id == a.InjuryId || w.Source == "affliction:" + a.Id);
                    if (origin == null)
                    {
                        origin = new InjuryState { Id = NewId(s, "w"), Source = "affliction:" + a.Id, Name = r.Name, Tags = WorkTag.None };
                        b.Injuries.Add(origin);
                    }
                    origin.Deficit += damage * source.Value / total;
                }
            }
            b.Integrity = Clamp(1 - b.Injuries.Sum(w => w.Deficit));
            double target = Clamp(Math.Pow(oldI, p.FunctionStructurePower) * (p.FunctionFloor + (1 - p.FunctionFloor) * nutrition) * (1 - br));
            double tau = target < oldR ? p.WorsenHours : p.RecoverHours;
            b.Function = Clamp(target + (oldR - target) * Math.Exp(-dt / tau));
            b.Vitality = Clamp(oldV + (p.VitalRecovery * oldI * oldR * (1 - oldV) * (1 - bv) - p.VitalDamage * bv) * dt);
            if (b.Vitality < oldV)
            {
                AfflictionState main = b.Afflictions.OrderByDescending(a =>
                {
                    AfflictionRule r = def.Afflictions.Find(rule => rule.Id == a.RuleId);
                    return r == null ? 0 : r.VitalWeight * Math.Pow(Clamp((a.Severity - r.VitalThreshold) / Math.Max(double.Epsilon, 1 - r.VitalThreshold)), r.CurvePower);
                }).FirstOrDefault();
                if (main != null) b.LastVitalDamageSource = def.Afflictions.Find(r => r.Id == main.RuleId).Name + (main.Source == "" ? "" : "（" + main.Source + "）");
            }
            b.Injuries.RemoveAll(w => w.Deficit < 0.00000001 && !b.Afflictions.Exists(a => a.InjuryId == w.Id) && w.DressingStrength <= 0 && w.CareStrength <= 0);
            // Repair attribution is exact, including the final floating point residue.
            b.Integrity = Clamp(1 - b.Injuries.Sum(w => w.Deficit));
            WarnBody(s);
            SyncConditionCards(s);
        }

        private void WarnBody(SurvivalState s)
        {
            BodyState b = s.Body;
            if (b.Food > def.Body.FoodWarning) b.FoodAlert = false;
            if (b.Vitality > def.Body.VitalWarning) b.VitalAlert = false;
            if (!b.FoodAlert && b.Food <= def.Body.FoodWarning)
            {
                b.FoodAlert = true; s.Paused = true; AddLog(s, "饱食偏低，请准备食物；继续劳动的缺口会引起衰弱。");
            }
            if (!b.VitalAlert && b.Vitality <= def.Body.VitalWarning)
            {
                b.VitalAlert = true; s.Paused = true; AddLog(s, "生命偏低，请检查伤口和持续异常。");
            }
        }

        public SurvivalResult Equip(string itemId) => Command(s =>
        {
            ItemState item = Item(s, itemId);
            if (item == null || Card(item.DefinitionId).Role != CardRole.Ornament) return SurvivalResult.Fail("请选择可佩戴物品。");
            if (IsLocked(s, item.Id)) return SurvivalResult.Fail("该物品正在使用。");
            if (s.Items.Exists(i => i.Place == Place.Equipment)) return SurvivalResult.Fail("已佩戴物品，请先取下。");
            item.OwnerId = "player"; item.Place = Place.Equipment; item.Slot = 0;
            return SurvivalResult.Ok("已佩戴。");
        });

        public SurvivalResult Unequip(string itemId) => Command(s =>
        {
            ItemState item = Item(s, itemId);
            if (item == null || item.Place != Place.Equipment) return SurvivalResult.Fail("该物品没有佩戴。");
            TaskState reservation = new TaskState { Id = NewId(s, "slot") };
            string error = TryReserveOutputs(s, reservation, new System.Collections.Generic.List<Amount> { new Amount(item.DefinitionId, item.Quantity) });
            if (!string.IsNullOrEmpty(error)) return SurvivalResult.Fail(error);
            // Use the same allocation path as production, then preserve the original instance identity.
            Reservation slot = s.Reservations.Find(r => r.TaskId == reservation.Id);
            if (slot == null) return SurvivalResult.Fail("桌面没有可用位置。");
            item.OwnerId = ""; item.Place = Place.Table; item.Slot = slot.Slot; item.X = slot.X; item.Y = slot.Y;
            s.Reservations.RemoveAll(r => r.TaskId == reservation.Id);
            return SurvivalResult.Ok("已取下。");
        });
    }
}
