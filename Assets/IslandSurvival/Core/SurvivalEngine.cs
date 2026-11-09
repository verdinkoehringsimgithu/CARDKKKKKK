using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    /// <summary>所有状态命令在候选副本校验后一次提交；动画和Unity帧不拥有规则状态。</summary>
    public sealed partial class SurvivalEngine
    {
        private readonly SurvivalDefinition def;
        private SurvivalState state;
        public event Action Changed;
        public SurvivalState Snapshot => SurvivalCopies.Copy(state);
        public SurvivalDefinition Rules => SurvivalCopies.Copy(def);
        public bool Paused => state.Paused;
        public bool Dead => state.Dead;
        public int Speed => state.Speed;
        public double Health => 100 * Math.Pow(state.Body.Integrity, def.Body.HealthWeight) * Math.Pow(state.Body.Function, 1 - def.Body.HealthWeight);

        public SurvivalEngine(SurvivalDefinition definition, uint seed = 19349663)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            def = SurvivalCopies.Copy(definition);
            string error = ValidateDefinition(def);
            if (!string.IsNullOrEmpty(error)) throw new ArgumentException(error);
            state = new SurvivalState { Capacity = def.StartCapacity, Minute = def.StartMinute, RandomState = seed == 0 ? 19349663u : seed };
            foreach (StartItem start in def.Start)
                if (CreateItem(state, start.DefinitionId, start.Count, "", Place.Table, -1, start.X, start.Y) == null)
                    throw new ArgumentException("初始桌面没有合法位置：" + start.DefinitionId);
            foreach (ItemState item in state.Items) foreach (Lot lot in item.Lots) lot.AvailableAt = state.Minute;
            foreach (ItemState item in state.Items) { item.BornAt = state.Minute - 1; if (!state.Discovered.Contains(item.DefinitionId)) state.Discovered.Add(item.DefinitionId); }
            RefreshDay(state, true);
            SyncConditionCards(state);
            AddLog(state, "营地已准备，时间暂停。");
            error = ValidateState(state);
            if (error != "") throw new ArgumentException(error);
        }

        private SurvivalResult Command(Func<SurvivalState, SurvivalResult> command, bool preview = false, bool allowEnded = false, bool trackResources = true)
        {
            if (!allowEnded && (state.Dead || state.Completed && !state.ContinueAfterWin)) return SurvivalResult.Fail("本局已结束，请先选择继续经营、读取或重新开始。");
            SurvivalState candidate = SurvivalCopies.Copy(state);
            SurvivalResult result;
            try { result = command(candidate); }
            catch (InvalidOperationException exception) { return SurvivalResult.Fail(exception.Message); }
            if (result == null || !result.Success) return result ?? SurvivalResult.Fail("操作未能完成。");
            if (trackResources) RecordResourceChanges(candidate, CaptureResources(state));
            string error = ValidateState(candidate);
            if (!string.IsNullOrEmpty(error)) return SurvivalResult.Fail(error);
            result.State = SurvivalCopies.Copy(candidate);
            if (preview) return result;
            candidate.Revision = state.Revision + 1;
            state = candidate;
            result.Changed = true;
            if (Changed != null)
                foreach (Action observer in Changed.GetInvocationList())
                    try { observer(); } catch { /* 显示观察者不能撤销已提交的规则。 */ }
            return result;
        }
        public SurvivalResult Restore(SurvivalState saved)
        {
            if (saved == null) return SurvivalResult.Fail("存档没有状态。");
            SurvivalState candidate = SurvivalCopies.Copy(saved);
            string error = ValidateState(candidate);
            if (error != "") return SurvivalResult.Fail(error);
            state = candidate;
            state.Paused = true;
            if (Changed != null) foreach (Action observer in Changed.GetInvocationList()) try { observer(); } catch { }
            return SurvivalResult.Ok("已读取，时间暂停。");
        }
        public SurvivalResult SetPaused(bool paused)
        {
            bool value = paused || state.Dead || state.Completed && !state.ContinueAfterWin;
            if (state.Paused == value) return SurvivalResult.Ok();
            return Command(s => { s.Paused = value; return SurvivalResult.Ok(); }, allowEnded: true);
        }
        public SurvivalResult SetSpeed(int speed) => Command(s =>
        {
            if (speed != 1 && speed != 3) return SurvivalResult.Fail("只支持1倍和3倍速度。");
            s.Speed = speed; return SurvivalResult.Ok();
        }, allowEnded: true);
        public SurvivalResult Continue() => Command(s => { if (!s.Completed || s.Dead) return SurvivalResult.Fail("当前不能继续经营。"); s.ContinueAfterWin = true; s.Paused = true; return SurvivalResult.Ok(); }, allowEnded: true);
        public SurvivalResult Advance(int minutes)
        {
            if (minutes <= 0 || minutes > def.MaxMinutesPerAdvance) return SurvivalResult.Fail("推进时间超出允许范围。");
            return Command(s =>
            {
                if (s.Paused || s.Dead || (s.Completed && !s.ContinueAfterWin)) return SurvivalResult.Fail("时间已暂停。");
                for (int i = 0; i < minutes && !s.Paused && !s.Dead; i++)
                {
                    Dictionary<string, double> before = CaptureResources(s);
                    s.Minute++;
                    TickDecay(s);
                    TickBiologyExpire(s);
                    TickTaskWork(s);
                    TickBody(s);
                    if (s.Body.Vitality <= 0)
                    {
                        s.Dead = true; s.Paused = true; s.FailureReason = DeathDescription(s);
                        RecordResourceChanges(s, before);
                        AddLog(s, s.FailureReason); break;
                    }
                    TickTasks(s);
                    TickBiology(s);
                    RefreshNodes(s);
                    if ((s.Minute - def.StartMinute) % 1440 == 0) RefreshDay(s, false);
                    CheckMilestonesAndGoals(s);
                    SyncConditionCards(s);
                    RemoveEmpty(s);
                    CheckFoodSources(s);
                    RecordResourceChanges(s, before);
                }
                return SurvivalResult.Ok();
            }, trackResources: false);
        }

        private CardRule Card(string id) => def.Cards.Find(c => c.Id == id);
        private RecipeRule Recipe(string id) => def.Recipes.Find(r => r.Id == id);
        private ItemState Item(SurvivalState s, string id) => s.Items.Find(i => i.Id == id);
        private string NewId(SurvivalState s, string prefix) => prefix + (s.NextId++).ToString();
        private static double Clamp(double value, double min = 0, double max = 1) => Math.Max(min, Math.Min(max, value));
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private double Random01(SurvivalState s)
        {
            uint x = s.RandomState;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            s.RandomState = x == 0 ? 19349663u : x;
            return s.RandomState / 4294967296.0;
        }
        private void AddLog(SurvivalState s, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            s.Log.Add(message); while (s.Log.Count > 48) s.Log.RemoveAt(0);
            s.Notice = message;
        }
        private void Mark(SurvivalState s, string key) { if (!string.IsNullOrEmpty(key) && !s.Milestones.Contains(key)) s.Milestones.Add(key); }
        private bool IsLocked(SurvivalState s, string id) => s.Tasks.Exists(t => t.TargetId == id || t.SecondaryId == id || t.LockedItems.Contains(id));
        private NodeRule GetNodeRule(string id) => def.Nodes.Find(n => n.CardId == id);
        private ItemState FindRootItem(SurvivalState s, ItemState item)
        {
            int budget = s.Items.Count + 1;
            while (item != null && !string.IsNullOrEmpty(item.OwnerId) && item.OwnerId != "player" && budget-- > 0) item = Item(s, item.OwnerId);
            return budget <= 0 ? null : item;
        }
        private DailyRecord Today(SurvivalState s)
        {
            int day = (int)((s.Minute - def.StartMinute) / 1440) + 1;
            DailyRecord record = s.Days.Find(d => d.Day == day);
            if (record == null) { record = new DailyRecord { Day = day }; s.Days.Add(record); }
            return record;
        }
        private void RecordFood(SurvivalState s, double amount, string source)
        {
            if (amount <= 0) return;
            DailyRecord d = Today(s); d.Food += amount;
            if (!string.IsNullOrEmpty(source) && !d.Sources.Contains(source)) d.Sources.Add(source);
        }
        private void RefreshNodes(SurvivalState s)
        {
            foreach (ItemState item in s.Items.ToArray())
            {
                NodeRule rule = GetNodeRule(item.DefinitionId);
                if (rule == null || rule.Tide || rule.RefreshMinutes <= 0 || (s.Minute - def.StartMinute) <= 0) continue;
                if (item.CooldownUntil > s.Minute) continue;
                if ((s.Minute - def.StartMinute) % (long)Math.Max(1, rule.RefreshMinutes) != 0) continue;
                foreach (Amount a in rule.Daily)
                {
                    Amount stock = item.Stock.Find(x => x.DefinitionId == a.DefinitionId);
                    if (stock == null) { stock = new Amount(a.DefinitionId, 0); item.Stock.Add(stock); }
                    Amount cap = rule.Capacity.Find(x => x.DefinitionId == a.DefinitionId);
                    stock.Count = Math.Min(cap == null ? a.Count : cap.Count, stock.Count + a.Count);
                }
            }
        }
        private void RefreshDay(SurvivalState s, bool first)
        {
            int day = (int)((s.Minute - def.StartMinute) / 1440) + 1;
            if (!first)
            {
                DailyRecord yesterday = s.Days.Find(d => d.Day == day - 1);
                if (yesterday != null) yesterday.Waste = s.Items.Where(i => i.DefinitionId == def.Goal.WasteId).Sum(i => i.Quantity);
                foreach (ItemState item in s.Items)
                {
                    CardRule rule = Card(item.DefinitionId);
                    if (rule.DailyWear > 0) item.Structure = Math.Max(0, item.Structure - rule.DailyWear);
                }
                OnDailyBiology(s);
            }
            s.TideDay = day;
            s.Tide = SurvivalCopies.Copy(def.TideFixed);
            if (first) s.Tide.AddRange(SurvivalCopies.Copy(def.FirstTide));
            foreach (PeriodicDrop drop in def.TidePeriodic)
                if (drop.EveryDays > 0 && day % drop.EveryDays == 0) s.Tide.AddRange(SurvivalCopies.Copy(drop.Items));
            if (def.TideRandom.Count > 0) s.Tide.Add(SurvivalCopies.Copy(def.TideRandom[(int)(Random01(s) * def.TideRandom.Count)]));
            Today(s);
            while (s.Days.Count > 10) s.Days.RemoveAt(0);
        }
        private void CheckMilestonesAndGoals(SurvivalState s)
        {
            foreach (ItemState item in s.Items) if (!s.Discovered.Contains(item.DefinitionId)) s.Discovered.Add(item.DefinitionId);
            if (s.Completed || s.Dead || (s.Minute - def.StartMinute) % 1440 != 0) return;
            int day = Today(s).Day;
            if (day < def.Goal.MinimumDay) return;
            BodyState b = s.Body;
            double h = Math.Pow(b.Integrity, def.Body.HealthWeight) * Math.Pow(b.Function, 1 - def.Body.HealthWeight);
            if (h < def.Goal.MinimumHealth || b.Vitality < def.Goal.MinimumVital || b.Mind < def.Goal.MinimumMind) return;
            var sources = new HashSet<string>(); int compost = 0;
            for (int d = day - def.Goal.WindowDays; d < day; d++)
            {
                DailyRecord record = s.Days.Find(r => r.Day == d);
                if (record == null || record.Food < def.Goal.DailyFood || record.Waste > def.Goal.MaxWaste) return;
                compost += record.Compost; foreach (string source in record.Sources) sources.Add(source);
            }
            if (sources.Count < def.Goal.SourceCount || compost < def.Goal.CompostCount || !s.Milestones.Contains("crafted:" + def.Goal.BoneKnifeId)) return;
            double stored = 0;
            foreach (ItemState item in s.Items)
            {
                CardRule card = Card(item.DefinitionId);
                if (!card.CountsFood || card.Nutrition <= 0) continue;
                foreach (Lot lot in item.Lots) if (lot.ExpiresAt == 0 || lot.ExpiresAt - s.Minute >= def.Goal.MinimumShelfMinutes) stored += lot.Count * card.Nutrition;
            }
            if (stored < def.Goal.StoredFood) return;
            s.Completed = true; s.Paused = true; AddLog(s, "营地已稳定。");
        }
    }
}
