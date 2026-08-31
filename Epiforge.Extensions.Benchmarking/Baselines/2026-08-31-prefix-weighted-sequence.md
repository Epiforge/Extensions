# Baseline, 31 August 2026 — PrefixWeightedSequence

The first measurement of anything in `Epiforge.Extensions.Collections`. Recorded as a baseline rather than to answer a question, since the question it was written to answer had already been answered elsewhere.

`ObservableCollectionWhereQuery` keeps its element memberships in a `PrefixWeightedSequence<T>` and calls into it on the per-element path: `PrefixWeightBefore` to translate an index, `SetWeight` when an element's membership flips, `Insert` and `RemoveAt` when the source changes. Nothing in the package had ever been benchmarked.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *PrefixWeightedSequenceBenchmarks*
```

## Result

| operation | 100 | 1,000 | 10,000 | allocated |
|--- |---: |---: |---: |---: |
| `PrefixWeightBefore` | 3.210 ns | 8.116 ns | 12.014 ns | 0 B |
| `SetWeight` | 13.075 ns | 18.876 ns | 24.598 ns | 0 B |
| `InsertThenRemoveAtMiddle` | 151.2 ns | 349.7 ns | 438.7 ns | 64 B |

**Logarithmic**, and the increments are what say so: `PrefixWeightBefore` rises by 4.9 and then 3.9 nanoseconds per tenfold increase in size, a constant increment rather than a constant factor. Linear would have read 3.2, 32, 320. `SetWeight` has the same shape at +5.8 and +5.7 with a larger constant, since it walks back to the root repairing subtree aggregates rather than only descending.

**Both descents allocate nothing.** The 64 bytes on the insert-and-remove pair is the one node an insert creates.

At ten thousand items, an index translation costs **12 nanoseconds**. Called once per element at that size it would be about 120 μs against the 20,639 μs `QueryFootprintBenchmarks` measures for the same workload — **half a percent**. This structure is not what the query layer spends its time on.

## Why this was nearly a much larger benchmark

It was first written with a fourth size of 100,000 and a fourth arm reading `NodeAt`, at an estimated half hour of machine time, on the premise that `PrefixWeightBefore` might be linear and the query layer therefore quadratic in disguise.

Two things were wrong with that premise. The type is a treap whose own documentation states the complexity, and it is four days old — the newest file in the package — so the measurement would have been confirming a specification against itself.

More usefully, the evidence was already recorded. `QueryFootprintBenchmarks` had run this structure at 250, 1,000, 4,000 and 10,000 elements, and per-element cost across those sizes goes 0.79, 0.60, 2.36, 2.20 μs. It steps up once and then **flattens**. A linear index translation could not flatten. The answer was in a document written the previous night.

The lesson is not about this benchmark. It is that a survey should start at what has not been examined, and the file that had been touched most recently was the least likely place for an unexamined problem to be. Sorted by last modification, this package runs from four days to eight months, and the measurement chosen was the four-day end.

## What remains unmeasured

`ObservableRangeCollection`, four months untouched, is the source under every benchmark in the `Baselines` directory. `ObservableDictionary`, `ObservableConcurrentDictionary` and `ObservableSortedDictionary` are the bulk of the package at roughly 45 KB each. `OrderedHashSet` and the two nullable-key dictionaries have never been timed.

Separately from timing: `NotifyDictionaryChangedEventArgs` is 8,351 bytes of source, eight months untouched, whose entire test file is two cases asserting that invalid constructor arguments throw. Nothing constructs a valid instance and checks that what went in comes back out, and that type carries every notification the dictionaries raise.
