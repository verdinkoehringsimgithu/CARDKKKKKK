# Card table API contract

This contract fixes the small shared surface between the plain C# domain, Unity UI and tests. Namespace `CardKKK.Tabletop.Core`. Unity 2022.3, C# 9-compatible, no external dependencies. All engine operations are synchronous and used from Unity's main thread.

## Data

- `[Serializable] TablePoint` struct with public float `X`, `Y`, constructor `(float x, float y)`. Coordinates use board-local bottom-left origin.
- `[Serializable] CardAttribute` class with public string `Key`, public int `Value`.
- `[Serializable] CardData` class with public string `Id`, `DefinitionId`, `Label`, `Family`; public int `Rank`; public bool `FaceUp = true`; public `List<CardAttribute> Attributes` initialized empty.
- `[Serializable] PileData` class with public string `Id`; public `TablePoint Position` (bottom-left of bottom card); public `List<string> CardIds` initialized empty, ordered bottom to top. Each later card is offset UP by `rules.PileOffset` units.
- `[Serializable] BoardSnapshot` class with public int `SchemaVersion = 1`, `Revision`; public `List<CardData> Cards` and `List<PileData> Piles` initialized empty. Piles are ordered back to front. All returned data is a defensive copy.
- `BoardRules` class public fields `Width = 1600`, `Height = 900`, `CardWidth = 100`, `CardHeight = 140`, `PileOffset = 22`, `SnapDistance = 65`, `Gap = 12`, `MaxCards = 1000`, `MaxPileSize = 20`. Validate finite positive geometry and limits at construction. Pile height is `CardHeight + (count-1)*PileOffset`.
- `OperationResult` with public read-only `bool Success`, `string Error`, `string PileId`, `string CardId`.
- `DropPreview` with public read-only `bool IsValid`, `TablePoint Position`, `string TargetPileId`, `int InsertionIndex`, `string Reason`, `IReadOnlyList<string> CardIds`. For merges, Position is the destination pile position. No target means free placement.
- `DragInfo` with public read-only `IReadOnlyList<string> CardIds`, `string SourcePileId`.

## BoardEngine

Constructor `BoardEngine(BoardRules rules)`. Owns a private validated rule copy, board model and at most one drag. Exposes `BoardRules Rules` as a copy, `bool IsDragging`, `DragInfo CurrentDrag` as a copy, `event Action Changed`.

Public methods:

```csharp
BoardSnapshot Capture();
OperationResult Spawn(CardData template, TablePoint position);
OperationResult BringToFront(string pileId);
OperationResult BeginDrag(string pileId, string cardId = null);
DropPreview PreviewDrop(TablePoint position);
OperationResult CommitDrop(TablePoint position);
OperationResult CancelDrag();
OperationResult FlipCard(string cardId);
OperationResult FlipPile(string pileId);
OperationResult SortPile(string pileId);
OperationResult ShufflePile(string pileId, int seed);
OperationResult ReorderCard(string pileId, string cardId, int index);
OperationResult PopCard(string pileId, string cardId = null);
OperationResult TidyTable();
OperationResult ResolveOverlaps();
OperationResult Restore(BoardSnapshot snapshot);
```

## Rules and boundaries

- Spawn clones the template and generates a unique ID, never trusting template.Id. DefinitionId and Family must be nonempty. All cards must belong to exactly one nonempty pile. Pile IDs and card IDs globally unique within each ID kind. Rank ties sort stably by current order.
- Spawn, unlike an explicit player drop, uses a bounded nearest-free search if the desired position is blocked. ShufflePile uses seeded Fisher-Yates, preserves all members and operates atomically. The same snapshot and seed produce the same permutation within the target runtime.
- A pile contains one Family. Same-family piles can merge only if within capacity. MaxCards and MaxPileSize limit operations and saves. Rejections preserve the committed state.
- `BeginDrag` locks other mutations except Cancel/Preview/Commit. Null cardId drags the whole pile, otherwise detaches exactly that card. Committed state remains unchanged during drag. Invalid CommitDrop ends drag and restores the original committed state. Preview and commit use the same planner. Source remaining cards are compacted only in the candidate state.
- Target selection uses a spatial hash broadphase and exact geometry/range checks, prefers nearest eligible pile, then frontmost stable tie. A pointer already inside a foreign incompatible pile is an invalid drop. Insert index is based on projected vertical offset, clamped [0, target card count]. For a same-source whole-pile move, exclude source from candidates. Single-card dragging may reorder its original pile.
- Single-card or whole-pile drop must be fully inside bounds, free of other pile footprints after merge/removal. Failed free placement never silently changes intent. Explicit Pop/ResolveOverlaps use a bounded deterministic nearest-free-slot search. Never loop indefinitely.
- `TidyTable` merges nearby eligible same-family piles with deterministic pair order and capacity checks, then packs actual footprints in rows. Insufficient room rejects the whole command. `ResolveOverlaps` handles overlapping pairs by merging eligible stacks or moving a stack to a verified free slot, atomically.
- SortPile uses stable Rank ascending. ReorderCard index is final index in the existing pile. PopCard defaults to top card, finds free placement before committing.
- Restore validates schema, finite in-bounds positions, unique membership, IDs and family consistency BEFORE replacing state. Unknown schema and invalid input preserve current board, including current drag. Valid Restore is rejected while dragging. Overlapping saved piles may be rejected with an explicit error; never silently delete cards.
- Animation cannot drive state mutation. Model transitions are atomic. Visual interpolation retargets to latest state. Drag commands are rejected until layout animation ends in the Unity adapter. This does not provide multithreaded or network access.

## Save adapter

Namespace `CardKKK.Tabletop.Unity`. `BoardSaveStore(string path)` has `OperationResult Save(BoardEngine engine)` and `OperationResult Load(BoardEngine engine)`. Use JsonUtility DTO serialization, bounded file size, temporary file and atomic replace with backup; preserve current board and old save on failure. Save rejects active drag. Load validates through engine.Restore and falls back to backup only on invalid/corrupt primary.

## Unity demo / art

Namespace `CardKKK.Tabletop.Unity`. Separate `CardTableDemo` MonoBehaviour, self-contained demo scene and Editor menu `CardKKK/Open Card Table Demo`. 1600x900 reference Canvas, mouse drag, SHIFT-drag extracts single card, normal drag moves pile. Click selects and raises whole pile. Space flip, S sort selected pile, H shuffle selected pile, P pop, R tidy, O resolve overlaps, F5 save, F9 load, Escape cancel. Buttons show these operations. Pointer dragging previews green/red footprint and insertion indication. Render placeholders only. Reserve `Assets/CardTable/Art/{CardFaces,CardFrames,Table,Icons,Audio/SFX,Audio/Music}` and a ScriptableObject theme for replacement, no third-party game assets.

## Verification

Headless domain tests compile the actual Core source using .NET 8, with C# 9 language version, no Unity stubs or copied implementations. Tests cover atomic rejection, preview/drop equivalence, single/whole drag, stable sort, IDs/membership, collisions, finite table overflow, invalid snapshots, randomized operation invariants and load scale. The user explicitly takes responsibility for Unity compilation and runtime checks. Actual on-disk save round trips and visual interaction acceptance remain manual items in README. Domain benchmark results do not establish Unity FPS.
