# Benchmark comparison, 29 August 2026 — initialization lock

Measured against the same hardware and runtime as the other documents in this series, with one change to the libraries: `ObservableExpression.InitializationAccess` became nullable and is set to null once initialization succeeds, so the lock object is collectable rather than retained for the life of the node. `ExpressionObserver` reads the field before locking and skips the lock entirely when it is null.

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

## The prediction that was wrong

The change was proposed on the expectation that it would recover roughly thirty-two of the forty-one bytes per node that the deferral lock had been worth. It recovered none, and it never could have.

`MemoryDiagnoser` reports bytes *allocated*. The lock object is still allocated, in its field initializer, exactly as before; the change only lets it be collected earlier. The forty-one bytes attributed to the deferral lock came from never allocating one at all. These are different quantities and the estimate conflated them.

Allocation after the change is identical to the byte on every row of both benchmarks.

## QueryFanOut, 1,000 elements

Two runs were taken against identical code, the second specifically to test whether the first run's apparent regression was real.

| | before | after, run 1 | after, run 2 |
|--- |---: |---: |---: |
| `ChangeTheSharedValue` | 21.28 μs | 21.03 μs | 21.51 μs |
| `ConstructAndDisposeWithFanOut` | 2,795.61 μs | 2,880.43 μs | 2,878.84 μs |
| `ConstructAndDisposeWithoutFanOut` | 2,260.23 μs | 2,463.39 μs | 2,216.08 μs |
| `ConstructAndDisposeWithoutFanOut` allocated | 3,680.03 KB | 3,681.28 KB | 3,680.03 KB |

## ObservableExpression

The before-figures here predate two changes — the read guard was never measured on this benchmark — so this delta contains both. The read guard's share is estimated at well under one percent, since `CreateAndDispose` builds about four nodes and removes on the order of ten interlocked writes against a multi-microsecond measurement.

| existing expressions | before | after | change |
|--- |---: |---: |---: |
| 0 | 2.404 μs | 2.349 μs | −2.3% |
| 1,000 | 2.561 μs | 2.524 μs | −1.4% |
| 10,000 | 4.588 μs | 4.321 μs | **−5.8%** |

The ten-thousand row is the one that had not moved through any previous change on this path, holding at 4.501, 4.511 and 4.588 μs, because it is dominated by cache lookup rather than construction. Cache lookup is precisely where an already-initialized node now skips a lock, and it is the row that moved most.

## What the repeat run established about this benchmark

`ConstructAndDisposeWithoutFanOut` moved from 2,463.39 μs to 2,216.08 μs **with no change to the code between the two runs**. That is an eleven percent swing on an idle row, and the allocation anomaly in the first run — 3,681.28 KB against 3,680.03, with gen 0 at 195.3125 rather than 187.5 — vanished with it.

That reading was diagnosed at the time as a property of the benchmark. A later measurement showed it is not, and the correction is recorded in the section below; the withdrawals that follow still stand, because they rest on single-launch measurements regardless of what the row is capable of when pooled.

Two earlier claims in this series rest partly on this row:

- The plain disposal document reads −11.9% on this row. That figure is at the edge of the noise, but the twelve percent conclusion there does not depend on it alone: `CreateAndDispose` at zero existing expressions independently gave −12.4% on a much tighter row.
- The deferral latch document reads −5.6% on this row for the read guard. **That figure should not be relied upon.** The conclusion it accompanied does not depend on it either; the read guard's evidence is `ChangeTheSharedValue` at −20.0%, a row whose standard deviation runs two hundred to five hundred nanoseconds against a five microsecond movement.

`ChangeTheSharedValue` and `CreateAndDispose` remain the trustworthy instruments in this pair. The construct-and-dispose rows are for allocation, which is deterministic, and not for time.

`ConstructAndDisposeWithFanOut` is the one open question. Two post-change runs agree to within 1.6 μs of each other at about 2,880 μs, against a single pre-change measurement of 2,795.61. The post-change pair is tight enough to trust; whether the single pre-change figure was representative is unknown, so a three percent construction cost here is possible and unproven.

## Correction: the row is capable, the run was contaminated

Every benchmark in this project ran at BenchmarkDotNet's default `launchCount` of one. The diagnosis above assumed that reported error therefore excluded process-to-process variance, and that pooling several launches would widen the error bars to something honest. `QueryFanOutBenchmarks` was given `[SimpleJob(launchCount: 3)]` to test that.

The prediction was wrong in the direction that falsifies it.

| | single launch, range across runs | three launches, pooled |
|--- |---: |---: |
| `ChangeTheSharedValue` | 21.03–21.51 μs, error ±0.25–0.41 | 21.09 μs, error ±0.12 |
| `ConstructAndDisposeWithFanOut` | 2,795–2,880 μs, error ±44–56 | 2,816.12 μs, error ±24.5 |
| `ConstructAndDisposeWithoutFanOut` | 2,216–2,463 μs, error ±38–49 | 2,161.86 μs, error ±19.6 |

Error roughly halved, which more samples alone would explain. Standard deviation also fell, from 74.3 to 44.1 and from 58.6 to 38.2 on the two construction rows, which more samples alone would not. If between-process variance were the missing term, pooling three processes would have raised the standard deviation rather than lowering it.

So process-to-process variance is small here, and the 2,463.39 μs reading — whose own standard deviation was 72.96, high by this row's standards — was a contaminated run rather than a fair sample of the row's spread. The benchmark is not structurally incapable of resolving a few percent. It reports 0.9% error when pooled. It is simply vulnerable to an occasional disturbed run, and three launches dilute that.

This also closes the open question above. `ConstructAndDisposeWithFanOut` at 2,816.12 ± 24.5 sits within one percent of the 2,795.61 measured before the change. The suspected three percent construction cost is not supported. The two single runs near 2,880 that agreed to within 1.6 μs of each other were agreeing precisely about something that did not reproduce.

`launchCount` of three is now set on `QueryFanOutBenchmarks` and `ObservableExpressionBenchmarks`, the two used for decisions on this path. `QueryFootprintBenchmarks` is left at one: it already runs for six minutes, and it is used for breadth rather than for resolving small differences.

## Why the change is kept

The benefit is live-set reduction, and no benchmark in this suite measures it. Both harnesses construct and dispose, so a lock object that dies early and one that dies late cost the same in every column they report.

The consuming application decides this, not the harness. Grindstone 5's Dynamo holds long-lived observable queries as indexes — a `GroupBy` over a trait value enumeration, grouped by entity, standing for the lifetime of the session. That shape pays construction once and holds the graph indefinitely, so a per-node object that is no longer retained is a permanent reduction in resident set, while a possible three percent on construction is paid once and never again. It is also the shape whose repeated lookups hit the warm-cache path that measured −5.8%.

A workload of rapid construct-and-dispose churn would weigh this the other way. That is not the workload this library was built for.
