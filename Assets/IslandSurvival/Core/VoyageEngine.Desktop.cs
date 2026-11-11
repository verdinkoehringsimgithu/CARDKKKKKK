using System;
using System.Collections.Generic;
using System.Linq;

namespace CardKKK.IslandSurvival.Core
{
    public sealed partial class VoyageEngine
    {
        // Workspaces/Slots are authoritative. Staged/Preparations are read-only projections for recipe matching and v1 migration.
        public IReadOnlyList<VoyageSlotOffer> Slots(int location)
        {
            VoyageWorkspace workspace = Workspace(state, location);
            int count = workspace == null ? SlotCapacity(location) : workspace.Slots.Count;
            bool locked = location != 0 && (Find(state, location) == null || Find(state, location).JobId != 0);
            return Enumerable.Range(0, count).Select(index => new VoyageSlotOffer
            {
                Location = location, Index = index, CardId = workspace?.Slots[index].CardId ?? 0,
                Label = "投入", Locked = locked || state.Dead || state.Won
            }).ToArray();
        }

        public bool CanPut(int cardId, int location, int slot, out string reason) => CanPut(state, cardId, location, slot, out reason);

        public int FindAcceptingSlot(int cardId, int location)
        {
            foreach (VoyageSlotOffer slot in Slots(location))
                if (slot.CardId == 0 && CanPut(cardId, location, slot.Index, out _)) return slot.Index;
            return -1;
        }

        public bool IsInSlot(int id) => state.Workspaces.Any(workspace => workspace.Slots.Any(slot => slot.CardId == id));

        public bool IsLocation(int id) => IsLocation(state, id);

        private bool IsLocation(VoyageState s, int id)
        {
            VoyageCardState card = Find(s, id);
            return card != null && (cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility ||
                rules.Recipes.Any(recipe => !recipe.Automatic && recipe.LocationId == card.CardId));
        }

        private static bool InSlot(VoyageState s, int id) => s.Workspaces.Any(workspace => workspace.Slots.Any(slot => slot.CardId == id));

        public bool CanDrag(int id, bool single, out string reason) => CanDrag(state, id, single, out reason);

        private bool CanDrag(VoyageState s, int id, bool single, out string reason)
        {
            reason = "";
            VoyageCardState card = Find(s, id);
            if (card == null || s.Dead || s.Won || card.JobId != 0 || IsFollowingPreparedLimb(s, card))
            { reason = "这张牌现在不能移动。"; return false; }
            if (single && !rules.Desk.AllowSingleCardDrag || !single && !rules.Desk.AllowWholePileDrag && Child(s, id) != null)
            { reason = "当前牌桌规则不允许这样拖动。"; return false; }
            return true;
        }

        public IReadOnlyList<int> DragCards(int id, bool single) => single ? new[] { id } : Tail(state, id).Select(card => card.Id).ToArray();

        public bool CanStack(int id, int targetId, bool single, out string reason) => PreviewStack(id, targetId, single, out _, out _, out _, out reason);

        public bool PreviewStack(int id, int targetId, bool single, out float x, out float y, out int depth, out string reason)
        {
            x = y = 0; depth = 0;
            if (!CanStack(state, id, targetId, single, out reason)) return false;
            VoyageState candidate = Copy(state);
            VoyageResult result = PlanStack(candidate, id, targetId, single);
            if (!result.Success) { reason = result.Message; return false; }
            NormalizeDesktop(candidate);
            reason = ValidateState(candidate);
            if (reason != "") return false;
            VoyageCardState root = Find(candidate, Root(candidate, id));
            x = root.X; y = root.Y;
            VoyageCardState member = Find(candidate, id);
            while (member.StackParent != 0) { depth++; member = Find(candidate, member.StackParent); }
            return true;
        }

        public bool CanStagePair(int first, int second)
        {
            VoyageState candidate = Copy(state);
            candidate.PreparationLocation = 0;
            EnsureWorkspace(candidate, 0);
            return PutAutomatically(candidate, first, 0).Success && PutAutomatically(candidate, second, 0).Success;
        }

        public bool PreviewDeskDrop(int id, float x, float y, bool single, out float settledX, out float settledY, out string reason)
        {
            VoyageState candidate = Copy(state);
            VoyageResult result = PlanDeskDrop(candidate, id, x, y, single);
            VoyageCardState card = Find(candidate, id);
            settledX = card?.X ?? x; settledY = card?.Y ?? y;
            reason = result.Message;
            return result.Success;
        }

        public VoyageResult Put(int id, int location, int slot) => Commit(s => PutIntoSlot(s, id, location, slot));

        public VoyageResult Stack(int id, int targetId, bool single) => Commit(s => PlanStack(s, id, targetId, single));

        private VoyageResult PlanStack(VoyageState s, int id, int targetId, bool single)
        {
            if (!CanStack(s, id, targetId, single, out string reason)) return Fail(reason);
            DetachStack(s, id, single);
            RemoveFromSlots(s, id);
            ReleaseEmptyWorkspace(s, id, 0);
            VoyageCardState top = Tail(s, targetId).Last();
            Find(s, id).StackParent = top.Id;
            if (!ResolveDeskOverlap(s, Root(s, targetId))) return Fail("这里没有足够的堆叠空间。");
            return Ok();
        }

        // Dragging is a visual preview. Ownership changes only when this command commits.
        public VoyageResult DropOnDesk(int id, float x, float y, bool single) => Commit(s => PlanDeskDrop(s, id, x, y, single));

        private VoyageResult PlanDeskDrop(VoyageState s, int id, float x, float y, bool single)
        {
            if (!CanDrag(s, id, single, out string reason)) return Fail(reason);
            VoyageCardState card = Find(s, id);
            if (card == null || card.JobId != 0 || IsFollowingPreparedLimb(s, card)) return Fail("这张牌正在使用中。");
            if (!OnDesk(x, y)) return Fail("这个位置超出了桌面。");
            List<VoyageCardState> moving = single ? new List<VoyageCardState> { card } : Tail(s, id).ToList();
            if (moving.Any(item => item.JobId != 0)) return Fail("牌堆中有正在使用的牌。");
            DetachStack(s, id, single);
            RemoveFromSlots(s, id);
            float dx = x - card.X, dy = y - card.Y;
            foreach (VoyageCardState member in moving)
            {
                member.X = Clamp01(member.X + dx);
                member.Y = Clamp01(member.Y + dy);
            }
            if (!ResolveDeskOverlap(s, id)) return Fail("这里没有足够的落牌空间。");
            return Ok();
        }

        private bool ResolveDeskOverlap(VoyageState s, int movedId)
        {
            var roots = s.Cards.Where(card => card.StackParent == 0 && !InSlot(s, card.Id) && !IsFollowingPreparedLimb(s, card) &&
                (card.JobId == 0 || cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility)).ToArray();
            float width = (geometry.CardWidth + rules.Desk.Gap) / geometry.Width;
            float half = (geometry.CardHeight + rules.Desk.Gap) / (2 * geometry.Height);
            float step = rules.Desk.StackOffset / geometry.Height;
            var queue = new Queue<VoyageCardState>();
            var queued = new HashSet<int>();
            VoyageCardState moved = Find(s, movedId);
            queue.Enqueue(moved); queued.Add(moved.Id);
            int budget = Math.Min(4096, Math.Max(32, roots.Length * 12));
            while (queue.Count != 0 && budget-- > 0)
            {
                VoyageCardState current = queue.Dequeue(); queued.Remove(current.Id);
                float currentTail = (Tail(s, current.Id).Count() - 1) * step;
                if (half * 2 + currentTail > 1) return false;
                current.X = Math.Max(width / 2, Math.Min(1 - width / 2, current.X));
                current.Y = Math.Max(half, Math.Min(1 - half - currentTail, current.Y));
                foreach (VoyageCardState other in roots)
                {
                    if (other.Id == current.Id) continue;
                    float otherTail = (Tail(s, other.Id).Count() - 1) * step;
                    float overlapX = width - Math.Abs(other.X - current.X);
                    float overlapY = Math.Min(current.Y + half + currentTail, other.Y + half + otherTail) - Math.Max(current.Y - half, other.Y - half);
                    if (overlapX <= .00001f || overlapY <= .00001f) continue;
                    bool fixedOther = cards[other.CardId].Kind == VoyageCardKind.Resource || cards[other.CardId].Kind == VoyageCardKind.Facility || heldCards.Contains(other.Id);
                    VoyageCardState pushed = fixedOther ? current : other;
                    VoyageCardState anchor = fixedOther ? other : current;
                    float tail = fixedOther ? currentTail : otherTail;
                    float oldX = pushed.X, oldY = pushed.Y;
                    if (overlapX <= overlapY)
                        pushed.X = Math.Max(width / 2, Math.Min(1 - width / 2, pushed.X + (pushed.X >= anchor.X ? 1 : -1) * (overlapX + .0001f)));
                    else
                        pushed.Y = Math.Max(half, Math.Min(1 - half - tail, pushed.Y + (pushed.Y >= anchor.Y ? 1 : -1) * (overlapY + .0001f)));
                    if (pushed.X == oldX && pushed.Y == oldY)
                    {
                        // Try the other axis when the nearest escape is against the desk boundary.
                        if (overlapX <= overlapY)
                            pushed.Y = Math.Max(half, Math.Min(1 - half - tail, pushed.Y + (pushed.Y > .5f ? -1 : 1) * (overlapY + .0001f)));
                        else pushed.X = Math.Max(width / 2, Math.Min(1 - width / 2, pushed.X + (pushed.X > .5f ? -1 : 1) * (overlapX + .0001f)));
                    }
                    if (queued.Add(pushed.Id)) queue.Enqueue(pushed);
                }
            }
            return queue.Count == 0;
        }

        private bool CanPut(VoyageState s, int id, int locationId, int index, out string reason)
        {
            reason = "";
            VoyageCardState card = Find(s, id), location = Find(s, locationId);
            if (s.Dead || s.Won || card == null || card.JobId != 0)
            { reason = "这张牌正在使用中。"; return false; }
            if (IsFollowingPreparedLimb(s, card) && !(Workspace(s, locationId)?.Slots.Any(slot => slot.CardId == card.Limb) ?? false))
            { reason = "这张伤势正跟随别处的身体牌。"; return false; }
            if (id == locationId || cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility)
            { reason = "场所本体留在桌面上。"; return false; }
            if (Workspace(s, id)?.Slots.Any(slot => slot.CardId != 0) ?? false)
            { reason = "请先退回这张牌所带场所中的投入。"; return false; }
            if (locationId != 0 && (location == null || location.JobId != 0 || !IsLocation(s, locationId)))
            { reason = "场所正在使用或已经离开桌面。"; return false; }
            VoyageWorkspace workspace = Workspace(s, locationId);
            int capacity = workspace == null ? SlotCapacity(locationId, s) : workspace.Slots.Count;
            if (index < 0 || index >= capacity) { reason = "这个槽位不存在。"; return false; }
            var selected = new List<int>();
            if (locationId != 0) selected.Add(locationId);
            if (workspace != null)
                selected.AddRange(workspace.Slots.Where(slot => slot.Index != index && slot.CardId != 0 && slot.CardId != id).Select(slot => slot.CardId));
            selected.Add(id);
            // Slot order is physical placement, not recipe argument order. TryPlan assigns distinct instances to recipe roles.
            foreach (VoyageRecipeRule recipe in WorkspaceRecipes(s, locationId))
                if (TryPlan(s, recipe, selected, false, out _)) return true;
            reason = "这张牌不符合这里的投入条件。";
            return false;
        }

        private VoyageResult PutIntoSlot(VoyageState s, int id, int location, int index)
        {
            if (!CanPut(s, id, location, index, out string reason)) return Fail(reason);
            VoyageWorkspace workspace = EnsureWorkspace(s, location);
            VoyageSlotState slot = workspace.Slots[index];
            if (slot.CardId == id) return Unchanged();
            // Displace to a committed free desk position before the view animates the return.
            int displaced = slot.CardId;
            DetachStack(s, id, true);
            RemoveFromSlots(s, id);
            ReleaseEmptyWorkspace(s, id, location);
            slot.CardId = id;
            if (displaced != 0) PlaceReturned(s, Find(s, displaced));
            return Ok();
        }

        private IEnumerable<VoyageRecipeRule> WorkspaceRecipes(VoyageState s, int location)
        {
            string definition = location == 0 ? "" : Find(s, location)?.CardId;
            return rules.Recipes.Where(recipe => !recipe.Automatic && recipe.LocationId == definition);
        }

        private int SlotCapacity(int location, VoyageState s = null)
        {
            s = s ?? state;
            VoyageRecipeRule[] possible = WorkspaceRecipes(s, location).ToArray();
            int capacity = possible.Select(recipe => recipe.Inputs.Sum(input => input.Quantity) +
                rules.AuxiliaryLines.Count(line => AppliesToRecipe(line, recipe))).DefaultIfEmpty(1).Max();
            return Math.Max(1, Math.Min(MaxStageSize - (location == 0 ? 0 : 1), capacity));
        }

        private static VoyageWorkspace Workspace(VoyageState s, int location) => s.Workspaces.Find(item => item.Location == location);

        private VoyageWorkspace EnsureWorkspace(VoyageState s, int location)
        {
            VoyageWorkspace workspace = Workspace(s, location);
            if (workspace != null) return workspace;
            workspace = new VoyageWorkspace { Location = location };
            for (int i = 0; i < SlotCapacity(location, s); i++) workspace.Slots.Add(new VoyageSlotState { Index = i });
            s.Workspaces.Add(workspace);
            return workspace;
        }

        private void MigrateDesktop(VoyageState s)
        {
            s.Workspaces = new List<VoyageWorkspace>();
            var drafts = new List<VoyagePreparation>(s.Preparations ?? new List<VoyagePreparation>());
            drafts.Add(new VoyagePreparation { Location = s.PreparationLocation, Cards = new List<int>(s.Staged) });
            int oldActive = s.PreparationLocation;
            foreach (VoyagePreparation draft in drafts)
            {
                int location = draft.Location;
                if (location == 0)
                {
                    VoyageCardState embedded = draft.Cards.Select(id => Find(s, id)).FirstOrDefault(card => card != null &&
                        (cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility));
                    if (embedded != null)
                    {
                        location = embedded.Id;
                        if (oldActive == draft.Location) s.PreparationLocation = location;
                    }
                    else if (FindExact(s, draft.Cards, out MatchPlan legacyPlan) != null && legacyPlan.Location != 0)
                    {
                        location = legacyPlan.Location;
                        if (oldActive == draft.Location) s.PreparationLocation = location;
                    }
                }
                VoyageWorkspace workspace = EnsureWorkspace(s, location);
                int next = 0;
                foreach (int id in draft.Cards.Where(id => id != location))
                {
                    VoyageCardState card = Find(s, id);
                    if (cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility)
                    { EnsureWorkspace(s, id); continue; }
                    // Preserve valid legacy reservations even if their old combination is not a recipe.
                    while (next < workspace.Slots.Count && workspace.Slots[next].CardId != 0) next++;
                    if (next >= workspace.Slots.Count) workspace.Slots.Add(new VoyageSlotState { Index = next });
                    workspace.Slots[next++].CardId = id;
                }
            }
            s.DeskVersion = 1;
            SyncPreparationProjection(s);
        }

        private static void RemoveFromSlots(VoyageState s, int id)
        {
            foreach (VoyageWorkspace workspace in s.Workspaces)
                foreach (VoyageSlotState slot in workspace.Slots)
                    if (slot.CardId == id) slot.CardId = 0;
        }

        private static void ReleaseEmptyWorkspace(VoyageState s, int id, int nextLocation)
        {
            VoyageWorkspace own = Workspace(s, id);
            if (own == null || own.Slots.Any(slot => slot.CardId != 0)) return;
            s.Workspaces.Remove(own);
            if (s.PreparationLocation == id) s.PreparationLocation = nextLocation;
        }

        private static void SyncPreparationProjection(VoyageState s)
        {
            s.Staged = new List<int>();
            s.Preparations = new List<VoyagePreparation>();
            foreach (VoyageWorkspace workspace in s.Workspaces)
            {
                var ids = workspace.Slots.Where(slot => slot.CardId != 0).OrderBy(slot => slot.Index).Select(slot => slot.CardId).ToList();
                VoyageCardState location = Find(s, workspace.Location);
                if (location != null && location.JobId == 0) ids.Insert(0, location.Id);
                if (workspace.Location == s.PreparationLocation) s.Staged = ids;
                else s.Preparations.Add(new VoyagePreparation { Location = workspace.Location, Cards = ids });
            }
        }

        private static VoyageCardState Child(VoyageState s, int id) => s.Cards.Find(card => card.StackParent == id);

        private static IEnumerable<VoyageCardState> Tail(VoyageState s, int id)
        {
            var seen = new HashSet<int>();
            VoyageCardState card = Find(s, id);
            while (card != null && seen.Add(card.Id)) { yield return card; card = Child(s, card.Id); }
        }

        private static int Root(VoyageState s, int id)
        {
            var seen = new HashSet<int>();
            VoyageCardState card = Find(s, id);
            while (card != null && card.StackParent != 0 && seen.Add(card.Id)) card = Find(s, card.StackParent);
            return card?.Id ?? 0;
        }

        private static void DetachStack(VoyageState s, int id, bool single)
        {
            VoyageCardState card = Find(s, id);
            if (card == null) return;
            if (single)
            {
                VoyageCardState child = Child(s, id);
                if (child != null) child.StackParent = card.StackParent;
            }
            card.StackParent = 0;
        }

        private bool StackCompatible(VoyageCardState first, VoyageCardState second)
        {
            if (rules.Desk.StackMode == 2) return true;
            if (string.IsNullOrWhiteSpace(cards[first.CardId].Family) || string.IsNullOrWhiteSpace(cards[second.CardId].Family))
                return first.CardId == second.CardId;
            return rules.Desk.StackMode == 1 ? first.CardId == second.CardId :
                cards[first.CardId].Family == cards[second.CardId].Family;
        }

        private bool CanStack(VoyageState s, int id, int target, bool single, out string reason)
        {
            reason = "";
            VoyageCardState source = Find(s, id), other = Find(s, target);
            if (s.Dead || s.Won || !rules.Desk.AllowStacking || source == null || other == null || id == target ||
                source.JobId != 0 || !Available(s, other) || IsFollowingPreparedLimb(s, source) || IsFollowingPreparedLimb(s, other))
            { reason = "这两张牌现在不能堆叠。"; return false; }
            if (single && !rules.Desk.AllowSingleCardDrag || !single && !rules.Desk.AllowWholePileDrag && Child(s, id) != null)
            { reason = "当前牌桌规则不允许这样拖动。"; return false; }
            // Places are targets for inputs rather than ordinary piles.
            if (cards[source.CardId].Kind == VoyageCardKind.Resource || cards[source.CardId].Kind == VoyageCardKind.Facility ||
                cards[other.CardId].Kind == VoyageCardKind.Resource || cards[other.CardId].Kind == VoyageCardKind.Facility)
            { reason = "场所不能叠入牌堆。"; return false; }
            if (Workspace(s, id)?.Slots.Any(slot => slot.CardId != 0) ?? false)
            { reason = "请先退回这张牌所带场所中的投入。"; return false; }
            VoyageCardState[] moving = single ? new[] { source } : Tail(s, id).ToArray();
            if (moving.Any(card => card.Id == target)) { reason = "不能把牌堆接回自身。"; return false; }
            var movingIds = new HashSet<int>(moving.Select(card => card.Id));
            VoyageCardState[] existing = Tail(s, Root(s, target)).Where(card => !movingIds.Contains(card.Id)).ToArray();
            if (moving.Length + existing.Length > rules.Desk.MaxStackSize)
            { reason = "牌堆已达到上限。"; return false; }
            if (moving.Any(card => card.JobId != 0 || !StackCompatible(card, other)))
            { reason = "这些牌的堆叠分类不兼容。"; return false; }
            return true;
        }

        private string ValidateDesktop(VoyageState s)
        {
            if (s.DeskVersion != 1 || s.Workspaces == null) return "牌桌存档版本无效。";
            var locations = new HashSet<int>();
            var occupied = new HashSet<int>();
            foreach (VoyageWorkspace workspace in s.Workspaces)
            {
                if (workspace == null || workspace.Slots == null || !locations.Add(workspace.Location) ||
                    workspace.Slots.Count > MaxStageSize - (workspace.Location == 0 ? 0 : 1) ||
                    workspace.Location != 0 && Find(s, workspace.Location) == null) return "场所槽位记录无效。";
                VoyageCardState place = Find(s, workspace.Location);
                if (place != null && !IsLocation(s, place.Id))
                    return "槽位没有对应场所。";
                for (int i = 0; i < workspace.Slots.Count; i++)
                {
                    VoyageSlotState slot = workspace.Slots[i];
                    if (slot == null || slot.Index != i) return "槽位序号无效。";
                    if (slot.CardId == 0) continue;
                    VoyageCardState card = Find(s, slot.CardId);
                    if (card == null || card.JobId != 0 || card.Id == workspace.Location || !occupied.Add(card.Id) ||
                        card.StackParent != 0 || Child(s, card.Id) != null || place != null && place.JobId != 0)
                        return "卡牌被槽位、牌堆或任务重复占用。";
                    if (cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility)
                        return "场所本体不能占用投入槽。";
                }
            }
            var parents = new HashSet<int>();
            foreach (VoyageCardState card in s.Cards.Where(item => item.StackParent != 0))
            {
                VoyageCardState parent = Find(s, card.StackParent);
                if (parent == null || !parents.Add(parent.Id) || card.JobId != 0 || parent.JobId != 0 ||
                    occupied.Contains(parent.Id) || occupied.Contains(card.Id) || !StackCompatible(card, parent)) return "牌堆归属无效。";
                if (cards[card.CardId].Kind == VoyageCardKind.Resource || cards[card.CardId].Kind == VoyageCardKind.Facility ||
                    cards[parent.CardId].Kind == VoyageCardKind.Resource || cards[parent.CardId].Kind == VoyageCardKind.Facility)
                    return "场所不能保存在普通牌堆中。";
                var seen = new HashSet<int> { card.Id };
                while (parent != null)
                {
                    if (!seen.Add(parent.Id)) return "牌堆形成循环。";
                    parent = Find(s, parent.StackParent);
                }
                if (Tail(s, Root(s, card.Id)).Count() > rules.Desk.MaxStackSize) return "牌堆超过当前上限。";
            }
            if (!locations.Contains(s.PreparationLocation) || s.Preparations.Count != s.Workspaces.Count - 1)
                return "当前场所与槽位投影不一致。";
            foreach (VoyageWorkspace workspace in s.Workspaces)
            {
                var expected = workspace.Slots.Where(slot => slot.CardId != 0).Select(slot => slot.CardId).ToList();
                VoyageCardState location = Find(s, workspace.Location);
                if (location != null && location.JobId == 0) expected.Insert(0, location.Id);
                List<int> actual = workspace.Location == s.PreparationLocation ? s.Staged :
                    s.Preparations.Find(draft => draft.Location == workspace.Location)?.Cards;
                if (actual == null || !expected.SequenceEqual(actual)) return "场所准备列表不是槽位状态的正确投影。";
            }
            return "";
        }

        private void NormalizeDesktop(VoyageState s)
        {
            s.Workspaces.RemoveAll(workspace => workspace.Location != 0 && Find(s, workspace.Location) == null);
            foreach (VoyageWorkspace workspace in s.Workspaces)
                foreach (VoyageSlotState slot in workspace.Slots)
                    if (slot.CardId != 0 && (Find(s, slot.CardId) == null || Find(s, slot.CardId).JobId != 0)) slot.CardId = 0;
            foreach (VoyageCardState card in s.Cards.ToArray())
                if (card.StackParent != 0 && (card.JobId != 0 || Find(s, card.StackParent) == null ||
                    Find(s, card.StackParent).JobId != 0 || !StackCompatible(card, Find(s, card.StackParent)))) DetachStack(s, card.Id, false);
            foreach (VoyageCardState card in s.Cards.Where(item => item.StackParent != 0))
            {
                VoyageCardState root = Find(s, Root(s, card.Id));
                card.X = root.X; card.Y = root.Y;
            }
            EnsureWorkspace(s, s.PreparationLocation);
            SyncPreparationProjection(s);
            foreach (VoyageCardState follower in s.Cards.Where(card => IsFollowingPreparedLimb(s, card)).ToArray())
                DetachStack(s, follower.Id, true);
        }

        private void PlaceReturned(VoyageState s, VoyageCardState card)
        {
            if (card == null || card.JobId != 0 || InSlot(s, card.Id)) return;
            float width = (geometry.CardWidth + rules.Desk.Gap) / geometry.Width;
            float height = (geometry.CardHeight + rules.Desk.Gap) / geometry.Height;
            for (int ring = 0; ring < 12; ring++)
                for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (ring != 0 && Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        float x = card.X + dx * width, y = card.Y + dy * height;
                        if (x < width / 2 || x > 1 - width / 2 || y < height / 2 || y > 1 - height / 2) continue;
                        if (s.Cards.Any(other => other.Id != card.Id && other.JobId == 0 && !InSlot(s, other.Id) &&
                            Math.Abs(other.X - x) < width && Math.Abs(other.Y - y) < height)) continue;
                        card.X = x; card.Y = y; return;
                    }
        }

        private static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
    }
}
