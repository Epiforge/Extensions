# Benchmark comparison, 29 August 2026 — dependents lock and sequence fence

Measured against the same hardware and runtime as the other documents in this series. Four pooled configurations were taken, all at `launchCount` of three, to separate two changes to `ObservableExpression`'s dependent bookkeeping:

- the **fence** — `ObservableExpressionSubscription.Sequence` and the node's `dependentSequence`, which ensured a dependent subscribing during a raise was not invoked by that raise
- the **lock** — `dependentsAccess`, tried as a `SpinLock` struct in place of a `Lock` instance

This is not a release baseline. It measures unreleased code.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-*      : .NET 10.0.11, LaunchCount=3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutBenchmarks*
```

## The four configurations

| `ConstructAndDisposeWithoutFanOut` | mean | error | allocated |
|--- |---: |---: |---: |
| `Lock`, fence present | 2,161.86 μs | ±19.6 | 3,680.03 KB |
| `SpinLock(true)`, no fence | 2,624.11 μs | ±26.8 | 3,534.36 KB |
| `SpinLock(false)`, no fence | 2,481.33 μs | ±26.5 | 3,534.36 KB |
| `Lock`, no fence | 2,256.49 μs | ±21.7 | 3,656.58 KB |

| `ConstructAndDisposeWithFanOut` | mean | error | allocated |
|--- |---: |---: |---: |
| `Lock`, fence present | 2,816.12 μs | ±24.5 | 4,246.03 KB |
| `SpinLock(true)`, no fence | 2,989.26 μs | ±44.0 | 4,105.27 KB |
| `SpinLock(false)`, no fence | 2,916.20 μs | ±28.2 | 4,105.27 KB |
| `Lock`, no fence | 2,975.44 μs | ±31.6 | 4,222.57 KB |

`ChangeTheSharedValue` read 21.09, 21.11, 21.32 and 21.32 μs across the four, and allocated 86.26 KB in every one.

## Allocation, which is deterministic and agrees with itself

Isolating each change against the configuration that differs from it by one variable, over 1,000 elements at three nodes per element:

| change | `WithoutFanOut` | `WithFanOut` |
|--- |---: |---: |
| removing the fence | **8.00 B/node** | **8.01 B/node** |
| `SpinLock` in place of `Lock` | **41.72 B/node** | 40.04 B/node |

The fence figure was predicted to be zero. The reasoning was that `ObservableExpressionSubscription` carries sixteen bytes of header, three references and a `bool`, so a four-byte `int` should have vanished into existing padding. It did not; both rows agree at exactly eight bytes per node, which is the node's own `dependentSequence` and the cell's `Sequence` each crossing a boundary rather than filling one. Predicting field layout from first principles was a mistake worth not repeating.

`SpinLock` saves about 41 bytes per node — a `Lock` instance plus the reference field holding it, less the four bytes the struct occupies inline. That is consistent with the 41 bytes measured when the deferral lock was deleted outright.

## Timing, which does not agree with itself

The `SpinLock` comparison points in opposite directions on the two rows. Against `Lock, no fence`, `SpinLock(false)` is 10.0% **slower** on `WithoutFanOut` and 2.0% **faster** on `WithFanOut`. A primitive cannot be both.

Worse, one comparison rules out the instrument rather than the change. `Lock, fence present` and `Lock, no fence` differ only by the removal of two field writes and one branch — strictly less work — and construction measured 4.4% *slower* without them, with error bars that do not overlap. Deleting work does not cost four percent. That difference is drift.

So the reported error is still understating true variability, at `launchCount` of three, after that setting was introduced specifically to fix this. `ConstructAndDisposeWithoutFanOut` spans 2,161.86 to 2,624.11 across four pooled runs — twenty-one percent — while each run reports about one percent. The earlier correction, which concluded that pooling had made this row honest, was too optimistic. It made it *tighter*, which is not the same thing.

The rule to carry forward: **on this benchmark, treat allocation as evidence and construction timing as anecdote.** The section below tests one explanation for that and rejects it, without finding a better one. `ChangeTheSharedValue` remains trustworthy — it allocates 86 KB rather than 4 MB, and it has read within one percent of itself across every configuration here.

## A machine-load hypothesis, tested and mostly rejected

The `Lock, no fence` configuration was run while a 4K film was playing on the same machine. It was re-run with nothing else running.

| | with playback | quiet |
|--- |---: |---: |
| `ConstructAndDisposeWithFanOut` | 2,975.44 μs | 2,950.76 μs |
| `ConstructAndDisposeWithoutFanOut` | 2,256.49 μs | 2,232.40 μs |

About one percent, on a machine whose GPU does the decoding on dedicated hardware and never routes the frames through system memory. The hypothesis was reached for because it tidily explained the one figure that resisted explanation, which is a poor reason to believe something.

`ChangeTheSharedValue` read 21.01 μs on the quiet run — its tightest of the day, and within a tenth of a percent of the original baseline. Playback did not touch it either.

So the 3.3% gap between `Lock, fence present` at 2,161.86 μs and `Lock, no fence` at 2,232.40 μs is not machine load, and it is not the deleted instructions, because deleting two field writes and a branch cannot cost three percent. The remaining candidate is **field layout**: allocation confirms the node shrank by eight bytes, and an object that changes size packs differently against cache lines. A few percent in either direction from that is ordinary, unpredictable from a source diff, and not worth chasing here.

The question is closed rather than answered. Five runs went into a three percent difference on a cost paid once per node, and every candidate explanation leaves the same decision: keep `Lock`, leave the fence out, take the eight bytes.

## What was kept

The fence is gone. It cost eight bytes per node, it removed a volatile read and a branch per dependent per notification, and it existed to match a multicast delegate semantic the library had already deliberately abandoned in the other direction. `ChangeTheSharedValue` did not move when it was removed, so the notification saving is below measurement — the case for removing it is that it is less code and less semantics to explain, not that it is faster.

`Lock` was kept over `SpinLock`. The 41 bytes per node are real and would suit long-lived graphs, but there is no trustworthy timing evidence that they are free, and this benchmark has now demonstrated it cannot supply any at the five to ten percent level. Choosing a lower-level primitive on unmeasurable timing grounds, in exchange for five percent of a node's total footprint, is not a trade worth making blind. It is a one-line change to revisit if memory ever becomes the binding constraint in a consumer, and the right instrument for that question would measure a long-lived resident graph rather than construct-and-dispose churn.
