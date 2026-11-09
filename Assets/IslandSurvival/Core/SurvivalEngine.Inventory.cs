using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class SurvivalEngine
    {
        private int GridColumns(SurvivalState s) => s.Capacity == 24 ? 6 : def.Columns;
        private int GridRows(SurvivalState s) => s.Capacity == 40 ? def.Rows : s.Capacity / GridColumns(s);
        private int Units(string id) => Card(id).Role == CardRole.NaturalNode || Card(id).Kind == CardKind.Condition ? 0 : Card(id).Footprint;
        private int Side(string id) => Units(id) == 4 ? 2 : 1;
        private bool Root(ItemState i) => string.IsNullOrEmpty(i.OwnerId) && (i.Place == Place.Table || i.Place == Place.Residue && i.ResidueFrom == Place.Table);
        private int Used(SurvivalState s)
        {
            int total = s.Items.Where(Root).Sum(i => Units(i.DefinitionId));
            var outputs = s.Reservations.Where(r => r.Place == Place.Table && string.IsNullOrEmpty(r.OwnerId) && string.IsNullOrEmpty(r.StackId)).ToArray();
            total += outputs.Sum(r => Units(r.DefinitionId));
            foreach (var group in outputs.Where(r => !string.IsNullOrEmpty(r.ReplacesItemId)).GroupBy(r => r.ReplacesItemId))
            {
                ItemState target = Item(s, group.Key);
                if (target != null && Root(target)) total -= Math.Min(Units(target.DefinitionId), group.Sum(r => Units(r.DefinitionId)));
            }
            return total;
        }
        private bool Overlap(string a, double x, double y, string b, double bx, double by)
        {
            double gap = 0.01;
            return x + Side(a) * def.CellWidth > bx + gap && bx + Side(b) * def.CellWidth > x + gap &&
                y + Side(a) * def.CellHeight > by + gap && by + Side(b) * def.CellHeight > y + gap;
        }
        private bool Fits(SurvivalState s, string definitionId, double x, double y, string ignore = "", string replacement = "")
        {
            if (Card(definitionId).Kind == CardKind.Condition) return Finite(x) && Finite(y) && x >= 0 && y >= 0 && x + def.CellWidth <= def.Width && y + def.CellHeight <= def.Height;
            if (Units(definitionId) == 0) return true;
            int side = Side(definitionId), cols = GridColumns(s), rows = GridRows(s);
            if (!Finite(x) || !Finite(y) || x < 0 || y < 0 || x + side * def.CellWidth > cols * def.CellWidth + .01 || y + side * def.CellHeight > rows * def.CellHeight + .01) return false;
            foreach (ItemState i in s.Items)
                if (Root(i) && i.Id != ignore && i.Id != replacement && Units(i.DefinitionId) > 0 && Overlap(definitionId, x, y, i.DefinitionId, i.X, i.Y)) return false;
            foreach (Reservation r in s.Reservations)
                if (r.Id != ignore && r.ReplacesItemId != ignore && r.StackId == "" && r.OwnerId == "" && r.Place == Place.Table && Overlap(definitionId, x, y, r.DefinitionId, r.X, r.Y)) return false;
            return true;
        }
        private bool FindPosition(SurvivalState s, string id, out double x, out double y)
        {
            x = y = 0;
            if (Used(s) + Units(id) > s.Capacity) return false;
            for (int row = 0; row < GridRows(s); row++)
                for (int col = 0; col < GridColumns(s); col++)
                { x = col * def.CellWidth; y = row * def.CellHeight; if (Fits(s, id, x, y)) return true; }
            return false;
        }
        private int Slots(CardRule owner, Place place)
        {
            if (owner.Kind != CardKind.Facility && owner.Kind != CardKind.Area) return 0;
            switch (place)
            {
                case Place.Input: return owner.InputSlots;
                case Place.Output: return owner.OutputSlots;
                case Place.Storage: return owner.StorageSlots;
                case Place.Plant: return owner.PlantSlots;
                case Place.Adult: return owner.AdultSlots;
                case Place.Young: return owner.YoungSlots;
                default: return 0;
            }
        }
        private bool Accepts(ItemState owner, CardRule card, Place place)
        {
            CardRule rule = Card(owner.DefinitionId);
            if (rule.Kind != CardKind.Facility && rule.Kind != CardKind.Area) return false;
            if (card.Kind == CardKind.Area || card.Kind == CardKind.Condition || card.Role == CardRole.NaturalNode) return false;
            if (place == Place.Storage) return rule.StorageSlots > 0 && card.Kind == CardKind.Material;
            if (place == Place.Plant) return rule.Role == CardRole.Garden && (card.Role == CardRole.AloePlant || card.Role == CardRole.YamPlant);
            if (place == Place.Adult) return rule.Role == CardRole.Pen && card.Role == CardRole.AdultSnail;
            if (place == Place.Young) return rule.Role == CardRole.Pen && (card.Role == CardRole.YoungSnail || card.Role == CardRole.AdultSnail);
            if (place == Place.Output) return rule.OutputSlots > 0 && card.Kind == CardKind.Material;
            if (place != Place.Input || card.Kind != CardKind.Material) return false;
            // 合法投料取决于配置中的实际用途；不接受无用途库存来扩大区域容量。
            return def.Recipes.Any(r => (r.RequiredRole == rule.Role || r.SecondaryRole == rule.Role) && r.Inputs.Any(a => a.Alternatives.Contains(card.Id))) ||
                def.Biology.Any(b => ((rule.Role == CardRole.Pen && (b.Role == CardRole.AdultSnail || b.Role == CardRole.YoungSnail)) || (rule.Role == CardRole.Garden && (b.Role == CardRole.YamPlant || b.Role == CardRole.AloePlant))) && b.Feed.Any(a => a.DefinitionId == card.Id));
        }
        private int FreeSlot(SurvivalState s, string owner, Place place, string ignore = "", string taskId = "")
        {
            ItemState container = Item(s, owner); if (container == null) return -1;
            int max = Slots(Card(container.DefinitionId), place);
            for (int k = 0; k < max; k++)
                if (!s.Items.Any(i => i.OwnerId == owner && (i.Place == place || i.Place == Place.Residue && i.ResidueFrom == place) && i.Slot == k && i.Id != ignore) &&
                    !s.Reservations.Any(r => r.OwnerId == owner && r.Place == place && r.Slot == k && r.TaskId != taskId && r.StackId == "")) return k;
            return -1;
        }
        private ItemState CreateItem(SurvivalState s, string definitionId, int count, string owner = "", Place place = Place.Table, int slot = -1, double x = -1, double y = -1, string traitId = "")
        {
            CardRule card = Card(definitionId);
            if (card == null || count <= 0 || count > (card.Kind == CardKind.Material ? card.StackLimit : 1)) return null;
            if (owner == "" && place == Place.Table && card.Role != CardRole.NaturalNode && card.Kind != CardKind.Condition)
            {
                if (Used(s) + Units(definitionId) > s.Capacity) return null;
                if (x < 0 || y < 0 || !Fits(s, definitionId, x, y)) { if (!FindPosition(s, definitionId, out x, out y)) return null; }
            }
            else if (owner != "" && place != Place.Residue && place != Place.Equipment)
            {
                ItemState o = Item(s, owner); if (o == null || !Accepts(o, card, place)) return null;
                if (slot < 0) slot = FreeSlot(s, owner, place);
                if (slot < 0 || slot >= Slots(Card(o.DefinitionId), place) || s.Items.Any(i => i.OwnerId == owner && i.Place == place && i.Slot == slot)) return null;
            }
            var result = new ItemState { Id = NewId(s, "i"), DefinitionId = definitionId, Quantity = count, OwnerId = owner ?? "", Place = place,
                Slot = Math.Max(0, slot), X = Math.Max(0, x), Y = Math.Max(0, y), TraitId = traitId ?? "", BornAt = s.Minute,
                Durability = card.InitialDurability, MaxDurability = card.InitialDurability, Structure = card.Structure };
            result.Lots.Add(new Lot { Count = count, AvailableAt = s.Minute + 1, ExpiresAt = card.ShelfMinutes > 0 ? s.Minute + (long)card.ShelfMinutes : 0 });
            NodeRule node = GetNodeRule(definitionId); if (node != null) result.Stock = SurvivalCopies.Copy(node.Initial);
            s.Items.Add(result); InitializeBiology(s, result); return result;
        }
        private int Count(SurvivalState s, string definitionId, string owner = null) => s.Items.Where(i => i.DefinitionId == definitionId && (owner == null || i.OwnerId == owner)).Sum(i => i.Lots.Where(l => l.AvailableAt <= s.Minute).Sum(l => l.Count));
        private bool Take(SurvivalState s, string definitionId, int count, string owner = null, bool keepFresh = false, double minutes = 0)
        {
            var items = s.Items.Where(i => i.DefinitionId == definitionId && (owner == null || i.OwnerId == owner) && !IsLocked(s, i.Id)).ToArray();
            if (items.Sum(i => i.Lots.Where(l => l.AvailableAt <= s.Minute && (!keepFresh || l.ExpiresAt == 0 || l.ExpiresAt > s.Minute + minutes)).Sum(l => l.Count)) < count) return false;
            foreach (ItemState i in items)
            {
                foreach (Lot lot in i.Lots.OrderBy(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt).ToArray())
                { if (lot.AvailableAt > s.Minute || keepFresh && lot.ExpiresAt > 0 && lot.ExpiresAt <= s.Minute + minutes) continue; int n = Math.Min(count, lot.Count); lot.Count -= n; i.Quantity -= n; count -= n; if (count == 0) break; }
                i.Lots.RemoveAll(l => l.Count == 0); if (count == 0) break;
            }
            RemoveEmpty(s); return true;
        }
        private string TryReserveOutputs(SurvivalState s, TaskState task, List<Amount> amounts, string owner = "", Place place = Place.Table)
        {
            RecipeRule taskRule = Recipe(task.RecipeId);
            ItemState replacement = taskRule != null && (taskRule.Kind == RecipeKind.Salvage || taskRule.Kind == RecipeKind.Demolish || taskRule.Kind == RecipeKind.Destroy && taskRule.Permanent) ? Item(s, task.TargetId) : null;
            int replacementIndex = replacement == null ? -1 : s.Items.IndexOf(replacement);
            if (replacement != null) s.Items.Remove(replacement);
            try
            {
            foreach (Amount amount in amounts.GroupBy(a => new { a.DefinitionId, Trait = a.TraitId ?? "" }).Select(g => new Amount(g.Key.DefinitionId, g.Sum(a => a.Count)) { TraitId = g.Key.Trait }))
            {
                CardRule card = Card(amount.DefinitionId); if (card == null || amount.Count <= 0) return "产出配置无效。";
                int remaining = amount.Count;
                // 产出可以加入保质期独立的同类材料堆；预留也占用堆叠上限。
                if (card.Kind == CardKind.Material)
                    foreach (ItemState stack in s.Items.Where(i => i.OwnerId == owner && i.Place == place && i.DefinitionId == card.Id && i.TraitId == (amount.TraitId ?? "") && !IsLocked(s, i.Id)).ToArray())
                    {
                        int available = card.StackLimit - stack.Quantity - s.Reservations.Where(r => r.StackId == stack.Id).Sum(r => r.Count);
                        int n = Math.Min(remaining, available); if (n <= 0) continue;
                        s.Reservations.Add(new Reservation { Id = NewId(s, "o"), TaskId = task.Id, DefinitionId = card.Id, Count = n, OwnerId = owner, Place = place, Slot = stack.Slot, StackId = stack.Id, TraitId = amount.TraitId ?? "" });
                        remaining -= n; if (remaining == 0) break;
                    }
                while (remaining > 0)
                {
                    var r = new Reservation { Id = NewId(s, "o"), TaskId = task.Id, DefinitionId = card.Id, Count = Math.Min(remaining, card.Kind == CardKind.Material ? card.StackLimit : 1), OwnerId = owner, Place = place, TraitId = amount.TraitId ?? "" };
                    r.ReplacesItemId = replacement == null ? "" : replacement.Id;
                    if (owner == "" && place == Place.Table)
                    {
                        if (!FindPosition(s, card.Id, out double x, out double y)) return "桌面没有可预留的产出位置，请整理或腾出空间。";
                        r.X = x; r.Y = y;
                    }
                    else
                    {
                        // 收获/死亡替换在同一个实体位置发生，预留可使用本任务将移除的目标槽。
                        r.Slot = FreeSlot(s, owner, place, place == Place.Plant ? task.TargetId : "");
                        if (r.Slot < 0) return "目标区域的产出槽已满。";
                    }
                    s.Reservations.Add(r); remaining -= r.Count;
                }
            }
            return "";
            }
            finally { if (replacement != null) s.Items.Insert(Math.Min(replacementIndex, s.Items.Count), replacement); }
        }
        private void DeliverReservations(SurvivalState s, TaskState task)
        {
            foreach (Reservation r in s.Reservations.Where(r => r.TaskId == task.Id).ToArray())
            {
                s.Reservations.Remove(r);
                ItemState stack = Item(s, r.StackId);
                if (stack != null)
                { stack.Quantity += r.Count; stack.Lots.Add(new Lot { Count = r.Count, AvailableAt = s.Minute + 1, ExpiresAt = Card(r.DefinitionId).ShelfMinutes > 0 ? s.Minute + (long)Card(r.DefinitionId).ShelfMinutes : 0 }); }
                else if (CreateItem(s, r.DefinitionId, r.Count, r.OwnerId, r.Place, r.Slot, r.X, r.Y, r.TraitId) == null)
                    throw new InvalidOperationException("预留产出位置丢失，结算已撤回。");
                if (!s.Discovered.Contains(r.DefinitionId)) s.Discovered.Add(r.DefinitionId);
                Mark(s, "crafted:" + r.DefinitionId);
            }
        }
        private void RemoveEmpty(SurvivalState s)
        {
            foreach (ItemState empty in s.Items.Where(i => i.Quantity <= 0).ToArray())
            {
                if (s.Reservations.Any(r => r.StackId == empty.Id)) continue;
                var residues = s.Items.Where(i => i.OwnerId == empty.Id && i.Place == Place.Residue).ToArray();
                if (residues.Length > 0)
                {
                    ItemState residue = residues[0]; residue.OwnerId = empty.OwnerId; residue.Place = Place.Residue; residue.ResidueFrom = empty.Place == Place.Residue ? empty.ResidueFrom : empty.Place; residue.Slot = empty.Slot; residue.X = empty.X; residue.Y = empty.Y;
                    foreach (ItemState child in residues.Skip(1)) child.OwnerId = residue.Id;
                }
                s.Items.Remove(empty);
            }
        }
        private void TickDecay(SurvivalState s)
        {
            foreach (ItemState item in s.Items.ToArray())
            {
                int expired = item.Lots.Where(l => l.ExpiresAt > 0 && l.ExpiresAt <= s.Minute).Sum(l => l.Count); if (expired == 0) continue;
                item.Lots.RemoveAll(l => l.ExpiresAt > 0 && l.ExpiresAt <= s.Minute); item.Quantity -= expired;
                string decay = Card(item.DefinitionId).DecayId;
                if (!string.IsNullOrEmpty(decay))
                {
                    // 部分批次腐烂使用原堆的残余位置，不凭空占用已满桌面的新格子；必须单独取出或清理。
                    int remaining = expired;
                    foreach (ItemState residue in s.Items.Where(i => i.OwnerId == item.Id && i.Place == Place.Residue && i.DefinitionId == decay).ToArray())
                    {
                        int n = Math.Min(remaining, Card(decay).StackLimit - residue.Quantity);
                        if (n <= 0) continue;
                        residue.Quantity += n; residue.Lots.Add(new Lot { Count = n, AvailableAt = s.Minute + 1 }); remaining -= n;
                    }
                    while (remaining > 0)
                    { int n = Math.Min(remaining, Card(decay).StackLimit); if (CreateItem(s, decay, n, item.Id, Place.Residue) == null) throw new InvalidOperationException("腐败残余配置无法交付。"); remaining -= n; }
                }
                AddLog(s, Card(item.DefinitionId).Name + "腐败" + expired + "份。");
            }
            RemoveEmpty(s);
        }
        public SurvivalResult Move(string itemId, double x, double y, int count = 0) => Command(s =>
        {
            ItemState item = Item(s, itemId); if (item == null) return SurvivalResult.Fail("卡牌已不存在。");
            if (!Root(item) || Card(item.DefinitionId).Role == CardRole.NaturalNode) return SurvivalResult.Fail("请先取出卡牌；自然节点固定在岸边。");
            if (count < 0 || count > item.Quantity) return SurvivalResult.Fail("移动数量无效。");
            if (count > 0 && count < item.Quantity)
            {
                if (Card(item.DefinitionId).Kind != CardKind.Material || IsLocked(s, item.Id) || s.Reservations.Any(r => r.StackId == item.Id) || s.Items.Any(i => i.OwnerId == item.Id)) return SurvivalResult.Fail("这堆材料正在工作、预留或带有残余，不能拆分。");
                if (!Fits(s, item.DefinitionId, x, y)) return SurvivalResult.Fail("这里放不下一份拆出的材料。");
                ItemState one = CreateItem(s, item.DefinitionId, count, x:x, y:y, traitId:item.TraitId);
                if (one == null) return SurvivalResult.Fail("没有可用的拆分位置。");
                MoveLots(item, one, count, true); return SurvivalResult.Ok();
            }
            if (!Fits(s, item.DefinitionId, x, y, item.Id)) return SurvivalResult.Fail("这里会重叠或越过生产区。");
            item.X = x; item.Y = y; return SurvivalResult.Ok();
        });
        public SurvivalResult Transfer(string itemId, string ownerId = "", Place place = Place.Table, int count = 0) => Command(s =>
        {
            ItemState item = Item(s, itemId); if (item == null) return SurvivalResult.Fail("卡牌已不存在。");
            if (IsLocked(s, item.Id) || s.Reservations.Any(r => r.StackId == item.Id)) return SurvivalResult.Fail("工作或产出预留正在使用这张卡，请先中止。");
            if (Card(item.DefinitionId).Role == CardRole.NaturalNode || Card(item.DefinitionId).Kind == CardKind.Condition || item.Place == Place.Equipment) return SurvivalResult.Fail("该卡不能这样搬动。");
            if (count < 0 || count > item.Quantity) return SurvivalResult.Fail("搬动数量无效。");
            if (count > 0 && count < item.Quantity)
            {
                if (Card(item.DefinitionId).Kind != CardKind.Material || s.Items.Any(i => i.OwnerId == item.Id)) return SurvivalResult.Fail("请先处理残余，再拆分材料。");
                ItemState one = CreateItem(s, item.DefinitionId, count, ownerId, place, traitId:item.TraitId);
                if (one == null) return SurvivalResult.Fail("目标没有可用的拆分位置。");
                MoveLots(item, one, count, true); return SurvivalResult.Ok();
            }
            if (item.OwnerId == ownerId && item.Place == place) return SurvivalResult.Ok();
            if (ownerId == "")
            { if (place != Place.Table || !FindPosition(s, item.DefinitionId, out double x, out double y)) return SurvivalResult.Fail("桌面没有空位。"); item.OwnerId = ""; item.Place = Place.Table; item.X = x; item.Y = y; }
            else
            {
                ItemState owner = Item(s, ownerId); if (owner == null || owner.Id == item.Id || !Accepts(owner, Card(item.DefinitionId), place)) return SurvivalResult.Fail("这里不接收这种卡牌。");
                int slot = FreeSlot(s, ownerId, place); if (slot < 0) return SurvivalResult.Fail("目标槽位已满。");
                item.OwnerId = ownerId; item.Place = place; item.Slot = slot;
            }
            return SurvivalResult.Ok();
        });
        public SurvivalResult Stack(string sourceId, string targetId, int count = int.MaxValue) => Command(s =>
        {
            ItemState source = Item(s, sourceId), target = Item(s, targetId);
            if (source == null || target == null || source == target || source.DefinitionId != target.DefinitionId || source.TraitId != target.TraitId || Card(source.DefinitionId).Kind != CardKind.Material) return SurvivalResult.Fail("只能合并同种、同性状材料。");
            if (IsLocked(s, sourceId) || IsLocked(s, targetId) || s.Reservations.Any(r => r.StackId == sourceId || r.StackId == targetId) || s.Items.Any(i => i.OwnerId == sourceId || i.OwnerId == targetId)) return SurvivalResult.Fail("请先结束工作或取出腐败残余。");
            int n = Math.Min(count, Math.Min(source.Quantity, Card(source.DefinitionId).StackLimit - target.Quantity)); if (n <= 0) return SurvivalResult.Fail("数量无效或该堆已满。");
            MoveLots(source, target, n); RemoveEmpty(s); return SurvivalResult.Ok();
        });
        public SurvivalResult Split(string id, int count = 1) => Command(s =>
        {
            ItemState item = Item(s, id); if (item == null || Card(item.DefinitionId).Kind != CardKind.Material || count < 1 || count >= item.Quantity) return SurvivalResult.Fail("拆分数量无效。");
            if (IsLocked(s, id) || s.Reservations.Any(r => r.StackId == id) || s.Items.Any(i => i.OwnerId == id)) return SurvivalResult.Fail("这堆材料正被使用或带有残余。");
            ItemState added = CreateItem(s, item.DefinitionId, count, traitId: item.TraitId); if (added == null) return SurvivalResult.Fail("桌面没有可用位置。");
            MoveLots(item, added, count, true); return SurvivalResult.Ok();
        });
        private static void MoveLots(ItemState source, ItemState target, int count, bool replace = false)
        {
            if (replace) { target.Lots.Clear(); target.Quantity = 0; }
            foreach (Lot lot in source.Lots.OrderBy(l => l.ExpiresAt == 0 ? long.MaxValue : l.ExpiresAt).ToArray())
            { int n = Math.Min(count, lot.Count); target.Lots.Add(new Lot { Count = n, ExpiresAt = lot.ExpiresAt, AvailableAt = lot.AvailableAt }); source.Quantity -= n; target.Quantity += n; lot.Count -= n; count -= n; if (count == 0) break; }
            source.Lots.RemoveAll(l => l.Count == 0);
        }
        public SurvivalResult Tidy() => Command(s =>
        {
            var roots = s.Items.Where(i => Root(i) && Units(i.DefinitionId) > 0).OrderByDescending(i => Units(i.DefinitionId)).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
            var reserved = s.Reservations.Where(r => r.OwnerId == "" && r.Place == Place.Table && r.StackId == "").OrderByDescending(r => Units(r.DefinitionId)).ToArray();
            s.Items.RemoveAll(i => roots.Contains(i)); s.Reservations.RemoveAll(r => reserved.Contains(r));
            // 大区域先排，小卡补齐，24格使用6×4，其余使用8列模板。
            foreach (ItemState item in roots) { if (!FindPosition(s, item.DefinitionId, out double x, out double y)) return SurvivalResult.Fail("整理失败：配置占地无法排布。"); item.X = x; item.Y = y; s.Items.Add(item); }
            foreach (Reservation r in reserved)
            {
                ItemState replacing = Item(s, r.ReplacesItemId); if (replacing != null) s.Items.Remove(replacing);
                bool found = FindPosition(s, r.DefinitionId, out double x, out double y);
                if (replacing != null) s.Items.Add(replacing);
                if (!found) return SurvivalResult.Fail("预留占地无法排布。"); r.X = x; r.Y = y; s.Reservations.Add(r);
            }
            return SurvivalResult.Ok();
        });
    }
}
