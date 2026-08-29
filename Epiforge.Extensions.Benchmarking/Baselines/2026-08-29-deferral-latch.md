# Benchmark comparison, 29 August 2026 — deferral latch

Measured against the same hardware and runtime as the 27, 28 and earlier 29 August documents, with one change to the libraries: `ObservableExpression.deferringEvaluation` became an `int` guarded by `Interlocked` and `Volatile` instead of a `bool` guarded by a per-node lock, and `deferringEvaluationAccess` was deleted.

The before-figures are the after-figures of the plain disposal document, which is why that document recorded them.

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
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObservableExpressionBenchmarks*
```

## QueryFanOut, 1,000 elements

| | before | after | change |
|--- |---: |---: |---: |
| `ChangeTheSharedValue` | 39.08 μs | 26.60 μs | **−31.9%** |
| `ConstructAndDisposeWithFanOut` | 3,071.36 μs | 2,890.46 μs | −5.9% |
| `ConstructAndDisposeWithoutFanOut` | 2,311.59 μs | 2,394.69 μs | +3.6% |
| `ChangeTheSharedValue` allocated | 86.26 KB | 86.26 KB | none |
| `ConstructAndDisposeWithFanOut` allocated | 4,375.84 KB | 4,246.03 KB | −129.81 KB |
| `ConstructAndDisposeWithoutFanOut` allocated | 3,799.76 KB | 3,680.03 KB | −119.73 KB |

## ObservableExpression

| existing expressions | before | after | change |
|--- |---: |---: |---: |
| 0 | 2.418 μs | 2.404 μs | −0.6% |
| 1,000 | 2.422 μs | 2.561 μs | +5.7% |
| 10,000 | 4.511 μs | 4.588 μs | +1.7% |
| 0, allocated | 4.56 KB | 4.49 KB | −72 B |
| 1,000, allocated | 4.26 KB | 4.14 KB | −123 B |
| 10,000, allocated | 4.44 KB | 4.32 KB | −123 B |

BenchmarkDotNet added a median column to this run, which it does when a distribution is skewed, and the standard deviation on the ten-thousand row tripled. The timing rows here are noisier than the previous run and none of the three movements exceeds it. Read the allocation column.

## The notification path

`ChangeTheSharedValue` fell by a third. That is the largest single movement any change in this series has produced, and it is the row that had not moved at all for the two preceding changes.

The mechanism is the `Evaluation` getter. Every read calls `EvaluateIfDeferred`, which acquired and released a lock on every call for the life of the node — including the overwhelming majority of calls made long after deferral had been resolved. A change to a shared node runs the getter once per dependent, so at a fan-out of a thousand this was a thousand uncontended lock round-trips per notification. They are now a thousand interlocked exchanges.

Allocation on that row did not move by a single byte, which is the confirmation that the improvement is in the cost of the operation and not in a reduction of work done.

## Bytes per node

Both fan-out predicates give each element its own binary, member and constant node.

| measurement | saved per element | ÷ 3 nodes |
|--- |---: |---: |
| `ConstructAndDisposeWithoutFanOut` | 122.6 B | **40.9 B** |
| `ObservableExpression`, 1,000 and 10,000 | 122.9 B | **41.0 B** |
| `ConstructAndDisposeWithFanOut` | 132.9 B | 44.3 B |

Two independent benchmarks agree to a tenth of a byte at 41 bytes per node: one `Lock` instance on .NET 9 plus the reference field that held it. The fan-out row sits three bytes high, consistent with its shared subtree contributing nodes that are not divided by the element count.

For scale, the entire move from `SyncDisposable` to `PlainSyncDisposable` — a base class change touching thirteen files — was worth 24 bytes per node. Deleting one lock was worth 41.

## Two things not to claim

`ConstructAndDisposeWithoutFanOut` got 3.6% slower while allocating 3.2% less. The error bars overlap at their edges and the sibling row moved the other way, so this is most likely noise, but it is a regression on the face of it and it is recorded rather than explained away.

The `ObservableExpression` timing rows moved up at one thousand and ten thousand existing expressions. Same treatment: within a noisier run than its predecessor, no claim made.

## What is left on this path

`EvaluateIfDeferred` still performs an interlocked write on every read of `Evaluation`, forever, although the latch can only flip once. A `Volatile.Read` guard in front of the exchange would make the settled case — which is nearly every case — a plain read with no locked instruction at all. That is the next thing to measure, and `ChangeTheSharedValue` is now demonstrably the instrument sensitive enough to see it.

Of the three per-node locks, two remain. `InitializationAccess` is load-bearing: the observer releases its cache lock before initializing, so two threads can hold the same cached node and race. Merging it with `dependentsAccess` appears safe under a parent-to-child acquisition ordering, but that argument has not been reviewed and a lock-ordering mistake does not fail a test suite reliably.
