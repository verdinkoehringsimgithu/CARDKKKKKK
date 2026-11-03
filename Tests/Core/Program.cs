using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using CardKKK.Tabletop.Core;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };
    private static int failed;
    private static int passed;

    private static int Main()
    {
        Run("spawn identifiers and defensive copies", Isolation);
        Run("whole pile preview, merge and conservation", WholeDrag);
        Run("single card extraction and free placement", SingleDrag);
        Run("cancel, invalid drop and busy mutation rollback", Rollback);
        Run("stable sorting, reordering and flipping", Ordering);
        Run("seeded Fisher-Yates multiset and reproducibility", Shuffling);
        Run("incompatible and over-capacity targets", Capacity);
        Run("growing merge footprint rejects collision", GrowingMerge);
        Run("spawn and stack geometry limits", Geometry);
        Run("spawn auto-placement and full-board rollback", SpawnPlacement);
        Run("automatic same-family spawn merge and nearby tidy", AutomaticMerging);
        Run("nearest target tie and same-source reorder", TargetSelection);
        Run("observer failure and reentrancy isolation", Observers);
        Run("pop placement and insufficient space", Pop);
        Run("tidy, overlap resolution and overflow", Layout);
        Run("malformed snapshot restore rejection", RestoreValidation);
        Run("3000 randomized operations", Randomized);
        Run("100/500/1000 card preview timings", Benchmark);
        Console.WriteLine($"RESULT {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            test();
            passed++;
            Console.WriteLine($"PASS {name} ({watch.Elapsed.TotalMilliseconds:F2} ms)");
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine($"FAIL {name}: {exception}");
        }
    }

    private static CardData Card(string id, int rank = 0, string family = "test")
    {
        return new CardData
        {
            Id = id, DefinitionId = "definition-" + rank, Label = "Card " + id,
            Family = family, Rank = rank, FaceUp = true,
            Attributes = new List<CardAttribute> { new CardAttribute { Key = "power", Value = rank } }
        };
    }

    private static PileData Pile(string id, float x, float y, params string[] ids)
    {
        return new PileData { Id = id, Position = new TablePoint(x, y), CardIds = new List<string>(ids) };
    }

    private static BoardSnapshot State(IEnumerable<CardData> cards, params PileData[] piles)
    {
        return new BoardSnapshot { Cards = new List<CardData>(cards), Piles = new List<PileData>(piles) };
    }

    private static BoardEngine Load(BoardSnapshot snapshot, BoardRules rules = null)
    {
        var engine = new BoardEngine(rules ?? new BoardRules());
        Accept(engine.Restore(snapshot));
        Invariants(engine);
        return engine;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Accept(OperationResult result)
    {
        Check(result != null && result.Success, "Rejected operation: " + result?.Error);
    }

    private static string Fingerprint(BoardSnapshot state) => JsonSerializer.Serialize(state, JsonOptions);

    private static void Reject(BoardEngine engine, Func<OperationResult> action)
    {
        string before = Fingerprint(engine.Capture());
        Check(!action().Success, "Expected operation rejection");
        Check(Fingerprint(engine.Capture()) == before, "Rejected operation modified model");
        Invariants(engine);
    }

    private static void Isolation()
    {
        var rules = new BoardRules();
        var engine = new BoardEngine(rules);
        var input = Card("untrusted-id", 3);
        var a = engine.Spawn(input, new TablePoint(0, 0));
        var b = engine.Spawn(input, new TablePoint(400, 0));
        Accept(a); Accept(b);
        Check(a.CardId != b.CardId && a.CardId != input.Id, "Spawn did not generate unique IDs");
        input.Attributes[0].Value = 999;
        input.Label = "changed";
        rules.Width = 1;
        engine.Rules.MaxCards = 0;
        var exported = engine.Capture();
        Check(exported.Cards.All(card => card.Attributes[0].Value == 3 && card.Label != "changed"), "Spawn shared template state");
        exported.Cards[0].Attributes[0].Value = -1;
        exported.Piles[0].CardIds.Clear();
        Check(engine.Capture().Cards[0].Attributes[0].Value == 3, "Capture shared nested objects");
        Check(engine.Rules.Width == 1600 && engine.Rules.MaxCards == 1000, "Rules are not defensive copies");
        var restored = engine.Capture();
        var second = Load(restored);
        restored.Cards[0].Attributes[0].Value = 100;
        restored.Piles.Clear();
        Check(second.Capture().Cards[0].Attributes[0].Value == 3, "Restore shared mutable input");
        var roundTrip = JsonSerializer.Deserialize<BoardSnapshot>(Fingerprint(second.Capture()), JsonOptions);
        Accept(second.Restore(roundTrip));
        Invariants(engine); Invariants(second);
    }

    private static BoardEngine TwoPiles()
    {
        return Load(State(new[] { Card("a"), Card("b"), Card("c"), Card("d") },
            Pile("p", 10, 10, "a", "b", "c"), Pile("q", 500, 10, "d")));
    }

    private static void WholeDrag()
    {
        var engine = TwoPiles();
        string before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("p"));
        Check(engine.CurrentDrag.CardIds.SequenceEqual(new[] { "a", "b", "c" }), "Drag order incorrect");
        var point = new TablePoint(500, 10);
        var preview = engine.PreviewDrop(point);
        Check(preview.IsValid && preview.TargetPileId == "q", "Expected merge preview");
        Check(Fingerprint(engine.Capture()) == before, "Preview modified model");
        var expected = new List<string> { "d" };
        expected.InsertRange(preview.InsertionIndex, new[] { "a", "b", "c" });
        Accept(engine.CommitDrop(point));
        var state = engine.Capture();
        Check(!engine.IsDragging && state.Piles.Count == 1 && state.Cards.Count == 4, "Merge changed card population");
        Check(state.Piles[0].CardIds.SequenceEqual(expected), "Commit differs from preview");
        Invariants(engine);
    }

    private static void SingleDrag()
    {
        var engine = TwoPiles();
        string before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("p", "b"));
        Check(engine.CurrentDrag.CardIds.SequenceEqual(new[] { "b" }), "Wrong card detached");
        var point = new TablePoint(500, 32);
        var preview = engine.PreviewDrop(point);
        Check(preview.IsValid && preview.TargetPileId == "q", "Single merge preview rejected");
        Check(Fingerprint(engine.Capture()) == before, "Single drag changed model early");
        var expected = new List<string> { "d" };
        expected.Insert(preview.InsertionIndex, "b");
        Accept(engine.CommitDrop(point));
        Check(engine.Capture().Piles.Single(p => p.Id == "p").CardIds.SequenceEqual(new[] { "a", "c" }), "Source compaction incorrect");
        Check(engine.Capture().Piles.Single(p => p.Id == "q").CardIds.SequenceEqual(expected), "Insertion differs from preview");
        Accept(engine.BeginDrag("p", "c"));
        Accept(engine.CommitDrop(new TablePoint(900, 10)));
        Check(engine.Capture().Piles.Count == 3, "Free drop failed to split pile");
        Invariants(engine);
    }

    private static void Rollback()
    {
        var engine = TwoPiles();
        string before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("p", "b"));
        Check(!engine.PreviewDrop(new TablePoint(-200, 0)).IsValid, "Out-of-bounds preview accepted");
        Accept(engine.CancelDrag());
        Check(Fingerprint(engine.Capture()) == before && !engine.IsDragging, "Cancel failed");
        Accept(engine.BeginDrag("p"));
        Check(!engine.CommitDrop(new TablePoint(float.NaN, 0)).Success, "NaN drop accepted");
        Check(Fingerprint(engine.Capture()) == before && !engine.IsDragging, "Invalid drop failed to restore");
        Reject(engine, () => engine.BeginDrag("unknown"));
        Reject(engine, () => engine.BeginDrag("p", "unknown"));
        var saved = engine.Capture();
        Accept(engine.BeginDrag("p"));
        var operations = new Func<OperationResult>[]
        {
            () => engine.Spawn(Card("x"), new TablePoint(900, 10)), () => engine.BeginDrag("q"),
            () => engine.BringToFront("q"), () => engine.FlipCard("a"), () => engine.FlipPile("p"),
            () => engine.SortPile("p"), () => engine.ShufflePile("p", 7), () => engine.ReorderCard("p", "a", 2),
            () => engine.PopCard("p"), () => engine.TidyTable(), () => engine.ResolveOverlaps(), () => engine.Restore(saved)
        };
        foreach (var operation in operations)
        {
            Reject(engine, operation);
            Check(engine.IsDragging, "Rejected busy command ended drag");
        }
        Accept(engine.CancelDrag());
    }

    private static void Ordering()
    {
        var engine = Load(State(new[] { Card("a", 3), Card("b", 1), Card("c", 1), Card("d", 2) }, Pile("p", 10, 10, "a", "b", "c", "d")));
        Accept(engine.SortPile("p"));
        Check(engine.Capture().Piles[0].CardIds.SequenceEqual(new[] { "b", "c", "d", "a" }), "Rank sort unstable");
        Accept(engine.ReorderCard("p", "b", 3));
        Check(engine.Capture().Piles[0].CardIds.SequenceEqual(new[] { "c", "d", "a", "b" }), "Final reorder index incorrect");
        Reject(engine, () => engine.ReorderCard("p", "c", -1));
        Reject(engine, () => engine.ReorderCard("p", "c", 4));
        Accept(engine.FlipCard("a"));
        Check(!engine.Capture().Cards.Single(c => c.Id == "a").FaceUp, "Flip failed");
        Accept(engine.FlipPile("p"));
        Check(engine.Capture().Cards.Single(c => c.Id == "a").FaceUp && !engine.Capture().Cards.Single(c => c.Id == "b").FaceUp, "Pile flip failed");
    }

    private static void Capacity()
    {
        var engine = Load(State(new[] { Card("a", 0, "red"), Card("b", 0, "blue") }, Pile("p", 10, 10, "a"), Pile("q", 500, 10, "b")));
        string before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("p"));
        Check(!engine.PreviewDrop(new TablePoint(500, 10)).IsValid, "Incompatible target accepted");
        Check(!engine.CommitDrop(new TablePoint(500, 10)).Success, "Incompatible merge committed");
        Check(Fingerprint(engine.Capture()) == before, "Incompatible merge lost data");
        engine = Load(State(new[] { Card("a"), Card("b"), Card("c") }, Pile("p", 10, 10, "a"), Pile("q", 500, 10, "b", "c")), new BoardRules { MaxPileSize = 2 });
        before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("p"));
        Check(!engine.PreviewDrop(new TablePoint(500, 10)).IsValid, "Over-capacity preview accepted");
        Check(!engine.CommitDrop(new TablePoint(500, 10)).Success, "Over-capacity merge committed");
        Check(Fingerprint(engine.Capture()) == before, "Over-capacity merge lost data");
    }

    private static void Shuffling()
    {
        var a = TwoPiles();
        var b = TwoPiles();
        var expected = a.Capture().Piles[0].CardIds.OrderBy(id => id).ToArray();
        Accept(a.ShufflePile("p", 7919));
        Accept(b.ShufflePile("p", 7919));
        Check(Fingerprint(a.Capture()) == Fingerprint(b.Capture()), "Same seed and state produced different shuffle");
        Check(a.Capture().Piles[0].CardIds.OrderBy(id => id).SequenceEqual(expected), "Shuffle changed membership");
        Reject(a, () => a.ShufflePile("missing", 1));
        Invariants(a);
    }

    private static void GrowingMerge()
    {
        var engine = Load(State(new[] { Card("a"), Card("b"), Card("c"), Card("d"), Card("e"), Card("f", 0, "other") },
            Pile("source", 10, 0, "a", "b"), Pile("target", 500, 0, "c", "d", "e"), Pile("above", 500, 200, "f")));
        string before = Fingerprint(engine.Capture());
        Accept(engine.BeginDrag("source"));
        Check(!engine.PreviewDrop(new TablePoint(500, 0)).IsValid, "Growing target footprint overlapped third pile");
        Check(!engine.CommitDrop(new TablePoint(500, 0)).Success, "Blocked growing merge committed");
        Check(Fingerprint(engine.Capture()) == before, "Growing merge rejection changed model");
    }

    private static void SpawnPlacement()
    {
        var engine = new BoardEngine(new BoardRules());
        Accept(engine.Spawn(Card("a", 0, "one"), new TablePoint(0, 0)));
        Accept(engine.Spawn(Card("b", 0, "two"), new TablePoint(0, 0)));
        Accept(engine.Spawn(Card("c", 0, "three"), new TablePoint(-100, -100)));
        Check(engine.Capture().Piles.Count == 3, "Spawn could not find nearest legal positions");
        Invariants(engine);
        var full = new BoardEngine(new BoardRules { Width = 100, Height = 140 });
        Accept(full.Spawn(Card("a", 0, "one"), new TablePoint(0, 0)));
        Reject(full, () => full.Spawn(Card("b", 0, "two"), new TablePoint(0, 0)));
        Reject(full, () => full.Spawn(Card("c", 0, "one"), new TablePoint(0, 0)));
    }

    private static void Observers()
    {
        var engine = TwoPiles();
        int calls = 0;
        bool reentrantRejected = false;
        engine.Changed += () => throw new InvalidOperationException("Deliberate observer failure");
        engine.Changed += () =>
        {
            calls++;
            reentrantRejected = !engine.FlipCard("b").Success;
        };
        Accept(engine.FlipCard("a"));
        Check(calls == 1 && reentrantRejected, "Notification failure or reentry corrupted transaction");
        Check(!engine.Capture().Cards.Single(card => card.Id == "a").FaceUp && engine.Capture().Cards.Single(card => card.Id == "b").FaceUp,
            "Observer changed committed outcome");
        Invariants(engine);
    }

    private static void AutomaticMerging()
    {
        var engine = new BoardEngine(new BoardRules());
        var first = engine.Spawn(Card("a"), new TablePoint(0, 0));
        Accept(first);
        Accept(engine.Spawn(Card("b"), new TablePoint(0, 0)));
        var state = engine.Capture();
        Check(state.Piles.Count == 1 && state.Piles[0].CardIds.Count == 2 && state.Cards.Count == 2, "Same-family overlap did not merge on spawn");
        Check(state.Piles[0].Id == first.PileId, "Automatic merge replaced existing pile identity");
        engine = Load(State(new[] { Card("a"), Card("b") }, Pile("p", 0, 0, "a"), Pile("q", 120, 0, "b")));
        Accept(engine.TidyTable());
        Check(engine.Capture().Piles.Count == 1 && engine.Capture().Cards.Count == 2, "Nearby compatible piles did not merge during tidy");
        Invariants(engine);
    }

    private static void TargetSelection()
    {
        var engine = Load(State(new[] { Card("a"), Card("b"), Card("c") },
            Pile("back", 0, 0, "a"), Pile("front", 230, 0, "b"), Pile("source", 900, 0, "c")));
        Accept(engine.BeginDrag("source"));
        var preview = engine.PreviewDrop(new TablePoint(165, 10));
        Check(preview.IsValid && preview.TargetPileId == "front", "Equidistant targets did not prefer frontmost pile");
        Accept(engine.CancelDrag());
        engine = TwoPiles();
        Accept(engine.BeginDrag("p", "b"));
        preview = engine.PreviewDrop(new TablePoint(10, 54));
        Check(preview.IsValid && preview.TargetPileId == "p", "Single card cannot reorder in original pile");
        Accept(engine.CommitDrop(new TablePoint(10, 54)));
        Check(engine.Capture().Piles.Single(p => p.Id == "p").CardIds.SequenceEqual(new[] { "a", "c", "b" }), "Same-source reorder incorrect");
        Invariants(engine);
    }

    private static void Geometry()
    {
        var engine = new BoardEngine(new BoardRules { MaxCards = 1 });
        Accept(engine.Spawn(Card("a"), new TablePoint(0, 0)));
        Reject(engine, () => engine.Spawn(Card("b"), new TablePoint(400, 0)));
        engine = new BoardEngine(new BoardRules());
        Reject(engine, () => engine.Spawn(Card("a"), new TablePoint(float.PositiveInfinity, 0)));
        Reject(engine, () => engine.Spawn(Card("a"), new TablePoint(float.NaN, 0)));
        var cards = Enumerable.Range(0, 20).Select(i => Card("c" + i)).ToArray();
        var oversized = State(cards, Pile("p", 0, 0, cards.Select(c => c.Id).ToArray()));
        var shortBoard = new BoardEngine(new BoardRules { Height = 200, MaxPileSize = 20 });
        Reject(shortBoard, () => shortBoard.Restore(oversized));
        var tooMany = State(Enumerable.Range(0, 1001).Select(index => Card("too-many-" + index)));
        Reject(engine, () => engine.Restore(tooMany));
        bool rejected = false;
        try { new BoardEngine(new BoardRules { CardWidth = float.NaN }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid rule geometry accepted");
    }

    private static void Pop()
    {
        var engine = TwoPiles();
        Accept(engine.PopCard("p"));
        Check(engine.Capture().Piles.Count == 3 && engine.Capture().Piles.Single(p => p.Id == "p").CardIds.SequenceEqual(new[] { "a", "b" }), "Pop failed");
        Invariants(engine);
        engine = Load(State(new[] { Card("a"), Card("b") }, Pile("p", 0, 0, "a", "b")), new BoardRules { Width = 100, Height = 162 });
        Reject(engine, () => engine.PopCard("p"));
    }

    private static void Layout()
    {
        var state = State(new[] { Card("a"), Card("b"), Card("c", 0, "other") }, Pile("p", 0, 0, "a"), Pile("q", 120, 0, "b"), Pile("r", 800, 400, "c"));
        var a = Load(state); var b = Load(state);
        Accept(a.TidyTable()); Accept(b.TidyTable());
        Check(Fingerprint(a.Capture()) == Fingerprint(b.Capture()), "Tidy nondeterministic");
        Check(a.Capture().Cards.Count == 3, "Tidy changed population");
        Accept(a.ResolveOverlaps()); Invariants(a);
        var overlap = State(new[] { Card("a", 0, "red"), Card("b", 0, "blue") }, Pile("p", 0, 0, "a"), Pile("q", 20, 20, "b"));
        var engine = new BoardEngine(new BoardRules());
        if (engine.Restore(overlap).Success)
        {
            Accept(engine.ResolveOverlaps()); Invariants(engine);
            Check(engine.Capture().Cards.Count == 2, "Overlap resolve lost cards");
        }
        else Check(engine.Capture().Cards.Count == 0, "Overlapping restore partially changed state");
        var full = Load(State(new[] { Card("a", 0, "red"), Card("b", 0, "blue") }, Pile("p", 0, 0, "a"), Pile("q", 100, 0, "b")), new BoardRules { Width = 200, Height = 140, Gap = 12 });
        Reject(full, () => full.TidyTable());
    }

    private static void RestoreValidation()
    {
        var engine = TwoPiles();
        Action<Action<BoardSnapshot>> rejects = change =>
        {
            var state = engine.Capture(); change(state); Reject(engine, () => engine.Restore(state));
        };
        rejects(s => s.SchemaVersion = 999);
        rejects(s => s.Cards.Add(Card("a")));
        rejects(s => s.Cards[0].Id = "");
        rejects(s => s.Cards[0].Family = "");
        rejects(s => s.Cards[0].DefinitionId = "");
        rejects(s => s.Cards[0].Family = "other");
        rejects(s => s.Piles[0].CardIds.Add("a"));
        rejects(s => s.Piles[0].CardIds.Add("missing"));
        rejects(s => s.Piles[0].CardIds.RemoveAt(0));
        rejects(s => s.Piles.Add(Pile("empty", 900, 0)));
        rejects(s => s.Piles[0].Position = new TablePoint(float.NaN, 0));
        rejects(s => s.Piles[0].Position = new TablePoint(2000, 0));
        rejects(s => s.Cards = null);
        rejects(s => s.Piles = null);
        rejects(s => s.Cards[0] = null);
        rejects(s => s.Piles[0] = null);
        rejects(s => s.Piles[0].CardIds = null);
        rejects(s => s.Cards[0].Attributes = null);
        rejects(s => s.Cards[0].Attributes.Add(null));
        rejects(s => s.Cards[0].Attributes.Add(new CardAttribute { Key = "power", Value = 2 }));
        rejects(s => s.Revision = -1);
        rejects(s => s.Piles[1].Id = s.Piles[0].Id);
        Reject(engine, () => engine.Restore(null));
    }

    private static void Randomized()
    {
        var engine = new BoardEngine(new BoardRules { Width = 2400, Height = 1600, MaxCards = 80 });
        var random = new Random(7919);
        for (int step = 0; step < 3000; step++)
        {
            var state = engine.Capture();
            if (state.Piles.Count == 0) { Accept(engine.Spawn(Card("seed"), new TablePoint(0, 0))); continue; }
            var pile = state.Piles[random.Next(state.Piles.Count)];
            string card = pile.CardIds[random.Next(pile.CardIds.Count)];
            string before = Fingerprint(state);
            int action = random.Next(10);
            OperationResult result;
            switch (action)
            {
                case 0: result = engine.Spawn(Card("random", random.Next(6), "f" + random.Next(3)), new TablePoint(random.Next(2400), random.Next(1600))); break;
                case 1: result = engine.FlipCard(card); break;
                case 2: result = engine.SortPile(pile.Id); break;
                case 3: result = engine.ReorderCard(pile.Id, card, random.Next(pile.CardIds.Count)); break;
                case 4: result = engine.PopCard(pile.Id, card); break;
                case 5: result = engine.BringToFront(pile.Id); break;
                case 6: result = engine.TidyTable(); break;
                case 7: result = engine.ResolveOverlaps(); break;
                default:
                    Accept(engine.BeginDrag(pile.Id, random.Next(2) == 0 ? null : card));
                    var point = new TablePoint(random.Next(-100, 2500), random.Next(-100, 1700));
                    var preview = engine.PreviewDrop(point);
                    Check(Fingerprint(engine.Capture()) == before, "Preview changed model at step " + step);
                    if (random.Next(5) == 0) result = engine.CancelDrag();
                    else { result = engine.CommitDrop(point); Check(preview.IsValid == result.Success, "Preview/commit mismatch at step " + step); }
                    break;
            }
            if (!result.Success) Check(Fingerprint(engine.Capture()) == before, "Rollback failed at step " + step);
            Check(engine.Capture().Cards.Count == state.Cards.Count + (action == 0 && result.Success ? 1 : 0), "Card conservation failed at step " + step);
            Check(!engine.IsDragging, "Drag still active at step " + step);
            Invariants(engine);
        }
    }

    private static void Benchmark()
    {
        foreach (int count in new[] { 100, 500, 1000 })
        {
            var cards = new List<CardData>(); var piles = new List<PileData>();
            for (int index = 0; index < count; index++)
            {
                string id = "card-" + index; cards.Add(Card(id));
                piles.Add(Pile("pile-" + index, index % 40 * 200, index / 40 * 200, id));
            }
            var engine = Load(State(cards, piles.ToArray()), new BoardRules { Width = 8200, Height = 5400, MaxCards = 1000 });
            var begin = Stopwatch.StartNew(); Accept(engine.BeginDrag("pile-0")); begin.Stop();
            engine.PreviewDrop(new TablePoint(101, 101));
            var watch = Stopwatch.StartNew(); const int iterations = 2000;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < iterations; index++) engine.PreviewDrop(new TablePoint(101 + index % 100, 101 + index % 150));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            watch.Stop();
            Console.WriteLine($"BENCH cards={count} begin_ms={begin.Elapsed.TotalMilliseconds:F3} previews={iterations} total_ms={watch.Elapsed.TotalMilliseconds:F3} us_per_preview={watch.Elapsed.TotalMilliseconds * 1000 / iterations:F3} bytes_per_preview={allocated / (double)iterations:F1}");
            Accept(engine.CancelDrag()); Invariants(engine);
        }
    }

    private static void Invariants(BoardEngine engine)
    {
        var state = engine.Capture(); var rules = engine.Rules;
        Check(state.SchemaVersion == 1 && state.Cards.Count <= rules.MaxCards, "Schema or total count invalid");
        var cards = state.Cards.ToDictionary(card => card.Id); var piles = new HashSet<string>(); var members = new HashSet<string>();
        foreach (var pile in state.Piles)
        {
            Check(!string.IsNullOrWhiteSpace(pile.Id) && piles.Add(pile.Id), "Duplicate pile ID");
            Check(pile.CardIds.Count > 0 && pile.CardIds.Count <= rules.MaxPileSize, "Empty or oversized pile");
            float x = pile.Position.X, y = pile.Position.Y, height = rules.CardHeight + (pile.CardIds.Count - 1) * rules.PileOffset;
            Check(!float.IsNaN(x) && !float.IsNaN(y) && !float.IsInfinity(x) && !float.IsInfinity(y), "Nonfinite position");
            Check(x >= -0.001f && y >= -0.001f && x + rules.CardWidth <= rules.Width + 0.001f && y + height <= rules.Height + 0.001f, "Footprint out of bounds");
            string family = cards[pile.CardIds[0]].Family;
            foreach (string id in pile.CardIds) Check(members.Add(id) && cards.TryGetValue(id, out var card) && card.Family == family, "Membership or family invalid");
        }
        Check(members.Count == cards.Count && cards.Keys.All(id => !string.IsNullOrWhiteSpace(id)), "Orphan or empty ID");
        for (int i = 0; i < state.Piles.Count; i++)
        for (int j = i + 1; j < state.Piles.Count; j++)
        {
            var a = state.Piles[i]; var b = state.Piles[j];
            float ah = rules.CardHeight + (a.CardIds.Count - 1) * rules.PileOffset;
            float bh = rules.CardHeight + (b.CardIds.Count - 1) * rules.PileOffset;
            bool overlap = a.Position.X < b.Position.X + rules.CardWidth - 0.001f && a.Position.X + rules.CardWidth > b.Position.X + 0.001f
                && a.Position.Y < b.Position.Y + bh - 0.001f && a.Position.Y + ah > b.Position.Y + 0.001f;
            Check(!overlap, "Overlapping piles " + a.Id + " / " + b.Id);
        }
    }
}
