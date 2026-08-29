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
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObservableExpressionBenchmarks*
```

## The comparison this document could not make at first

There is no valid before-figure for `QueryFootprintBenchmarks`. The last run before the change was `QueryFootprintBenchmarks-20260828-174822`, and `QueryFootprintBenchmarks.cs` was edited eight minutes after that run began. The two runs therefore measure different benchmarks, not different libraries.

The evidence that they must not be compared is in the baseline row. `OneNode` at 4,000 elements allocated 173,732 KB in the earlier run and 2,908 KB in this one — a sixtyfold difference on the row that is supposed to be the control, produced by a change to the benchmark rather than to the library. `SharedChangeableSubexpression` moved similarly. The `ThreeNodes`, `FiveNodes` and `ElevenNodes` rows agree between the two runs to within about half a percent, which is what a genuinely unaffected row looks like and what makes the other two rows so obviously suspect.

No baseline document captured the pre-change figures, so the earlier numbers exist only in a log for a benchmark that no longer exists. This document is written partly so that the next such change has something to be measured against.

## What the run says on its own terms

### The figures themselves

Recorded here because BenchmarkDotNet overwrites its own report on the next run of the same filter, and these are the before-figures for whatever is measured next.

| Predicate | Elements | Mean | Allocated |
|--- |---: |---: |---: |
| `OneNode` | 250 | 147.5 μs | 203.67 KB |
| `ThreeNodes` | 250 | 524.5 μs | 939.17 KB |
| `FiveNodes` | 250 | 818.5 μs | 1,271.48 KB |
| `ElevenNodes` | 250 | 1,936.8 μs | 2,525.13 KB |
| `SharedChangeableSubexpression` | 250 | 670.2 μs | 1,096.59 KB |
| `OneNode` | 1,000 | 615.2 μs | 798.59 KB |
| `ThreeNodes` | 1,000 | 2,850.1 μs | 3,754.13 KB |
| `FiveNodes` | 1,000 | 4,077.6 μs | 5,078.09 KB |
| `ElevenNodes` | 1,000 | 9,177.1 μs | 10,180.88 KB |
| `SharedChangeableSubexpression` | 1,000 | 3,859.0 μs | 4,369.59 KB |
| `OneNode` | 4,000 | 3,546.2 μs | 2,907.80 KB |
| `ThreeNodes` | 4,000 | 30,733.7 μs | 14,793.67 KB |
| `FiveNodes` | 4,000 | 44,116.4 μs | 20,200.09 KB |
| `ElevenNodes` | 4,000 | 82,175.8 μs | 40,303.35 KB |
| `SharedChangeableSubexpression` | 4,000 | 35,272.4 μs | 17,043.17 KB |
| `OneNode` | 10,000 | 10,149.2 μs | 7,752.21 KB |
| `ThreeNodes` | 10,000 | 83,730.0 μs | 37,659.43 KB |
| `FiveNodes` | 10,000 | 113,484.3 μs | 50,602.05 KB |
| `ElevenNodes` | 10,000 | 212,447.6 μs | 101,422.43 KB |
| `SharedChangeableSubexpression` | 10,000 | 99,419.5 μs | 42,732.22 KB |

Timing at the larger element counts carries standard deviations of two to five percent; treat the allocation column as the reliable one.

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

## The comparison two other benchmarks could make

Written after the fact. `QueryFanOutBenchmarks` and `ObservableExpressionBenchmarks` both had valid before-figures on disk — in each case the benchmark source predates its own last result, so the same benchmark measured the old library. They were re-run against the change.

### QueryFanOut, 1,000 elements

| | before | after | change |
|--- |---: |---: |---: |
| `ChangeTheSharedValue` | 38.55 μs | 39.08 μs | none |
| `ConstructAndDisposeWithFanOut` | 3,244.83 μs | 3,071.36 μs | −5.3% |
| `ConstructAndDisposeWithoutFanOut` | 2,623.85 μs | 2,311.59 μs | −11.9% |
| `ChangeTheSharedValue` allocated | 86.26 KB | 86.26 KB | none |
| `ConstructAndDisposeWithFanOut` allocated | 4,446.22 KB | 4,375.84 KB | −70.38 KB |
| `ConstructAndDisposeWithoutFanOut` allocated | 3,880.10 KB | 3,799.76 KB | −80.34 KB |

`ChangeTheSharedValue` moved 1.4%, inside its own error on both runs, and allocated the same to the byte. The notification path is unchanged, which is the expected result: the removed `OnPropertyChanged` raised a delegate that was already null, and a predictable branch costs nothing.

### ObservableExpression

| existing expressions | before | after | change |
|--- |---: |---: |---: |
| 0 | 2.759 μs | 2.418 μs | −12.4% |
| 1,000 | 2.609 μs | 2.422 μs | −7.2% |
| 10,000 | 4.501 μs | 4.511 μs | none |
| 0, allocated | 4.61 KB | 4.56 KB | −51 B |

At ten thousand existing expressions the row is dominated by the cache lookup and does not move, which is the control this pair happens to come with.

### The arithmetic, checked

Both fan-out predicates substitute each element as a constant, so each element gets its own binary, member and constant node — three — while the shared subtree is built once. The saving per node is therefore:

| row | saved per element | ÷ 3 nodes |
|--- |---: |---: |
| `ConstructAndDisposeWithFanOut` | 72.1 B | **24.0 B** |
| `ConstructAndDisposeWithoutFanOut` | 82.3 B | 27.4 B |

The estimate was 24 bytes per node — four reference fields on a 64-bit runtime. The fan-out row lands on it exactly. The row without fan-out runs about three bytes per node high, which is unexplained; it is a smaller graph and the per-query savings are not divided out, so the discrepancy is not necessarily per-node at all.

### The part the estimate missed

Allocation fell about two percent. Time fell twelve. The bytes were the smaller half of this change.

Two removals plausibly account for the difference and this run cannot separate them. A type with a finalizer is registered on the finalization queue when it is allocated and must be promoted rather than collected in gen 0; removing `~SyncDisposable()` removes that from every node, on both the allocation and the collection path. Independently, `SyncDisposable` logged on every dispose through three event-raising wrappers, and `PropertyChangeNotifier` ran a virtual `LoggerSet` on every construction — roughly half a dozen null-checked calls per node that no longer happen.

The gen 1 counter for `ConstructAndDisposeWithoutFanOut` fell from 156.25 to 93.75 collections per thousand operations, which is what the finalization-queue story predicts. Treat that as suggestive only: the same counter doubled on `CreateAndDispose` at zero existing expressions, so these columns are too coarse here to carry an argument.

The conclusion that matters is about method rather than about bytes. `QueryFootprint` was the benchmark chosen for this change and it was the wrong instrument twice over — it had no valid baseline, and by constructing and disposing deterministically it can never observe a finalization cost at all. The two benchmarks that answered the question were not the ones the change was designed around.

## Where the 816 bytes actually go

The estimate that mattered was 24 bytes. The measurement says the thing being shaved is 816. That gap is the finding.

An `ObservableExpression` allocates three separate synchronization objects per node — `dependentsAccess`, `deferringEvaluationAccess` and `InitializationAccess` — each a distinct heap object on .NET 9 and later, plus three reference fields to hold them. That is a visible candidate an order of magnitude larger than the one just removed, and unlike the base-class fields it is measurable by construction: the three have disjoint purposes but are never contended against one another on the same node.

This is the next thing to measure, and the first thing to do is record a baseline for it before touching anything.
