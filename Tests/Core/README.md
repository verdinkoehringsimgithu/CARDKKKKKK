# Core regression tests

Run from the repository root using the .NET 8 SDK.

```powershell
dotnet run --project Tests/Core/CardTable.Core.Tests.csproj -c Release
```

The executable compiles the actual `Assets/CardTable/Core` source with C# 9. It needs no Unity stubs or external test packages. Exit code 1 indicates a failing scenario.

The 18 scenarios cover defensive copies, identifiers and card conservation, drag previews and commits, cancel and invalid-drop rollback, mutation exclusion, stable sort, seeded shuffle, flips, reordering, incompatible stacks, capacity and geometry, nearest-candidate spawn/pop placement, full-table failure, automatic merging, snapping tie breaks, snapshots and observer reentrancy.

A seeded sequence performs 3,000 commands and checks membership, bounds and pile overlap after every command. Benchmarks use sparse layouts with 100, 500 and 1,000 cards on an enlarged board and print timings for 2,000 previews per size. These are domain CPU measurements, not Unity render-frame or device performance guarantees.
