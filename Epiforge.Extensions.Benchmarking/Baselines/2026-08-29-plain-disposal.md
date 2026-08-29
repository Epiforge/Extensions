# Benchmark comparison, 29 August 2026 — plain disposal

Measured against the same hardware and runtime as the 27 and 28 August documents, with one change to the libraries: `ObservableExpression` moved off `SyncDisposable` and onto `PlainSyncDisposable`, losing the finalizer, the `Logger` reference, the logger-set stack trace field and the `PropertyChanged` delegate, and keeping a bare `PropertyChanging` delegate for `ScopedObservableExpression`.

This is not a release baseline. It measures unreleased code.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFootprintBenchmarks*
```

## The comparison this document cannot make

There is no valid before-figure for `QueryFootprintBenchmarks`. The last run before the change was `QueryFootprintBenchmarks-20260828-174822`, and `QueryFootprintBenchmarks.cs` was edited eight minutes after that run began. The two runs therefore measure different benchmarks, not different libraries.

The evidence that they must not be compared is in the baseline row. `OneNode` at 4,000 elements allocated 173,732 KB in the earlier run and 2,908 KB in this one — a sixtyfold difference on the row that is supposed to be the control, produced by a change to the benchmark rather than to the library. `SharedChangeableSubexpression` moved similarly. The `ThreeNodes`, `FiveNodes` and `ElevenNodes` rows agree between the two runs to within about half a percent, which is what a genuinely unaffected row looks like and what makes the other two rows so obviously suspect.

No baseline document captured the pre-change figures, so the earlier numbers exist only in a log for a benchmark that no longer exists. This document is written partly so that the next such change has something to be measured against.

## What the run says on its own terms

Allocation per element, derived from the reported totals:

| Predicate | 250 | 1,000 | 4,000 | 10,000 |
|--- |---: |---: |---: |---: |
| `OneNode` | 834 B | 818 B | 744 B | 794 B |
| `ThreeNodes` | 3,847 B | 3,844 B | 3,787 B | 3,856 B |
| `FiveNodes` | 5,209 B | 5,200 B | 5,171 B | 5,182 B |
| `ElevenNodes` | 10,343 B | 10,425 B | 10,318 B | 10,386 B |

Per-element cost is flat in element count, varying by under two percent across a fortyfold range. The figure is a property of the predicate, not of the collection size.

The slope from three nodes to eleven is 812, 823, 816 and 816 bytes per node per element at the four sizes. Call it **816 bytes per expression node per element**.

Treat that as a slope and not a law: the `FiveNodes` row sits roughly 300 bytes per element below a straight line drawn through the three- and eleven-node rows, so subexpression sharing makes the relationship not quite linear. `OneNode` is below even the intercept, consistent with `person => true` folding to a constant rather than building a graph.

## What that means for this change

The four reference fields removed from each node come to 24 bytes on a 64-bit runtime. Against 816 bytes per node per element, that is **three percent**.

Allocation measurement here is precise enough to resolve three percent — the per-element figures above are stable to under two percent across sizes. It is not resolvable without a matched baseline, which is what this run lacks. So the change is sound on its merits, and this benchmark neither confirms nor refutes the arithmetic behind it. The finalizer removal is not visible here at all; a benchmark that constructs and disposes deterministically never queues a finalizable object, so that saving lives on the teardown path and would need `QueryFanOutBenchmarks` or a dedicated finalization measurement to appear.

## Where the 816 bytes actually go

The estimate that mattered was 24 bytes. The measurement says the thing being shaved is 816. That gap is the finding.

An `ObservableExpression` allocates three separate synchronization objects per node — `dependentsAccess`, `deferringEvaluationAccess` and `InitializationAccess` — each a distinct heap object on .NET 9 and later, plus three reference fields to hold them. That is a visible candidate an order of magnitude larger than the one just removed, and unlike the base-class fields it is measurable by construction: the three have disjoint purposes but are never contended against one another on the same node.

This is the next thing to measure, and the first thing to do is record a baseline for it before touching anything.
