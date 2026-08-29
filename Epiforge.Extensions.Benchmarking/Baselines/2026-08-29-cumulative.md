# Benchmark comparison, 29 August 2026 — cumulative

`QueryFootprintBenchmarks` re-measured after five changes, against the twenty-row table recorded in the plain disposal document. This is the broad instrument: five predicate shapes across four element counts. Its allocation column is deterministic and has been the only reliable column in this series.

The five changes since that baseline:

- the deferral latch — a per-node `Lock` replaced by an `int` and `Interlocked`
- the read guard — a `Volatile.Read` in front of the latch's exchange
- the initialization lock — released once initialization succeeds rather than retained for the node's life
- the sequence fence — `Sequence` and `dependentSequence` removed
- the dependents lock — tried as a `SpinLock`, reverted to `Lock`, so a net no-op

Only the first and fourth should change allocation. The read guard removes an instruction, the initialization lock only lets an already-allocated object die sooner, and the dependents lock ended where it started.

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

`launchCount` is left at one here. Allocation is deterministic and does not need pooling; the timing column on this benchmark should be read as indicative only.

## Allocation

| Shape | Elements | before | after | saved | saved per element |
|--- |---: |---: |---: |---: |---: |
| `OneNode` | 250 | 203.67 KB | 203.63 KB | 0.04 KB | 0.2 B |
| `ThreeNodes` | 250 | 939.17 KB | 904.90 KB | 34.27 KB | 140.4 B |
| `FiveNodes` | 250 | 1,271.48 KB | 1,222.17 KB | 49.31 KB | 202.0 B |
| `ElevenNodes` | 250 | 2,525.13 KB | 2,434.04 KB | 91.09 KB | 373.1 B |
| `SharedChangeableSubexpression` | 250 | 1,096.59 KB | 1,061.29 KB | 35.30 KB | 144.6 B |
| `OneNode` | 1,000 | 798.59 KB | 799.17 KB | −0.58 KB | −0.6 B |
| `ThreeNodes` | 1,000 | 3,754.13 KB | 3,629.71 KB | 124.42 KB | 127.4 B |
| `FiveNodes` | 1,000 | 5,078.09 KB | 4,912.38 KB | 165.71 KB | 169.7 B |
| `ElevenNodes` | 1,000 | 10,180.88 KB | 9,816.99 KB | 363.89 KB | 372.6 B |
| `SharedChangeableSubexpression` | 1,000 | 4,369.59 KB | 4,222.58 KB | 147.01 KB | 150.5 B |
| `OneNode` | 4,000 | 2,907.80 KB | 2,907.61 KB | 0.19 KB | 0.0 B |
| `ThreeNodes` | 4,000 | 14,793.67 KB | 14,291.17 KB | 502.50 KB | 128.6 B |
| `FiveNodes` | 4,000 | 20,200.09 KB | 19,494.43 KB | 705.66 KB | 180.6 B |
| `ElevenNodes` | 4,000 | 40,303.35 KB | 38,923.31 KB | 1,380.04 KB | 353.3 B |
| `SharedChangeableSubexpression` | 4,000 | 17,043.17 KB | 16,549.16 KB | 494.01 KB | 126.5 B |
| `OneNode` | 10,000 | 7,752.21 KB | 7,752.26 KB | −0.05 KB | 0.0 B |
| `ThreeNodes` | 10,000 | 37,659.43 KB | 36,146.43 KB | 1,513.00 KB | 154.9 B |
| `FiveNodes` | 10,000 | 50,602.05 KB | 48,512.30 KB | 2,089.75 KB | 214.0 B |
| `ElevenNodes` | 10,000 | 101,422.43 KB | 97,672.27 KB | 3,750.16 KB | 384.0 B |
| `SharedChangeableSubexpression` | 10,000 | 42,732.22 KB | 41,859.18 KB | 873.04 KB | 89.4 B |

## The control

`OneNode` is `person => true`. Its constant is built once and shared, so it creates no per-element observable node at all — and it saved nothing at any of the four sizes, the largest movement across all four being 0.2 bytes per element on 203 KB. A change that removes bytes from an expression node should be invisible to a shape that has no per-element expression nodes, and it was.

That the control held is what makes the other four rows readable.

## Bytes per node

Dividing the per-element saving by the number of observable nodes each shape creates *per element* — not by the expression-tree node count in the name, since constants and any subtree not mentioning the parameter are built once and shared across all elements:

| Shape | per-element nodes | saving per node, 10,000 elements |
|--- |---: |---: |
| `ThreeNodes` | 3 | 51.6 B |
| `FiveNodes` | 4 | 53.5 B |
| `ElevenNodes` | 8 | 48.0 B |

The prediction made before the run was **49 bytes per node**: 41 for the deleted deferral lock, measured on `QueryFanOut`, plus 8 for the fence, measured the same way. Three shapes bracket it at 48 to 54.

The node counts above are my reading of which subexpressions mention the parameter, and the spread is as likely to be error in those counts as in the measurement. The figure to quote is roughly fifty bytes.

`SharedChangeableSubexpression` is the one shape that does not fit. It gives 48.2, 50.2, 42.2 and 29.8 bytes per node at the four sizes — agreeing at the two smaller ones and drifting away at the two larger. Allocation is deterministic, so drift is the wrong word for whatever this is, and it is recorded here unexplained rather than averaged away.

## Timing

Indicative only, on a benchmark whose standard deviations here run three to six percent:

| Shape, 10,000 elements | before | after |
|--- |---: |---: |
| `OneNode` | 10,149.2 μs | 10,078.3 μs |
| `ThreeNodes` | 83,730.0 μs | 75,005.4 μs |
| `FiveNodes` | 113,484.3 μs | 97,185.7 μs |
| `ElevenNodes` | 212,447.6 μs | 195,442.5 μs |

Eight to fourteen percent off the node-heavy shapes and under one percent off the control, which is the pattern the mechanism predicts — fewer allocations and one fewer lock acquisition per node — but this column has failed to resolve differences of this size all day, and it is offered as corroboration rather than evidence.

## Where the day landed

Per observable expression node, across the whole set of changes: 24 bytes from moving off `PropertyChangeNotifier` and the finalizer, 41 from deleting the deferral lock, 8 from the fence. Roughly **73 bytes per node**, of which this run measures the last 49.

The notification path is separately down 44.8% end to end, from 38.55 μs to 21.01 μs on `ChangeTheSharedValue` at a fan-out of a thousand, essentially all of it from the deferral latch and its read guard.
