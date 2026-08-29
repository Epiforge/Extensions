# Benchmark comparison, 29 August 2026 — wrapper deferral

An observable expression whose graph contains a diamond — one leaf feeding two branches of different depth which rejoin — recomputed its join twice for a single input change and raised a notification each time. The first of those notifications carried a value computed from one updated branch and one stale one: a value that was never simultaneously true of the expression's inputs.

The cost of that is not the wasted recompute, which is nanoseconds. It is the wasted notification, which at the boundary is a layout pass, a redraw, or a broadcast to every subscriber, and which is then immediately corrected. This work suppresses the boundary event without reordering the graph.

This is not a release baseline. It measures unreleased code on the `wrapper-deferral` branch.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-OEWQXS : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObservableExpressionDiamondBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutFlipBenchmarks*
```

## The contract

> No consumer of an observable expression ever receives an evaluation that was not simultaneously true of every input that expression depends on.

That is a claim about what crosses the boundary, not about the graph. The graph still recomputes a shared join once per path. Anything the fast path adds later is held to the same sentence.

## What changed

`PropagationScope` is a thread-static depth counter and a list of enlisted observations. Every root at which a change can enter the expression graph now brackets its work in one: the three handlers in `ObservableMemberExpression` and `ObservableIndexExpression` that subscribe to a value's `PropertyChanged`, and the two that subscribe to its `CollectionChanged` and `DictionaryChanged`. Five bodies, in two files. That is the entire root set — everything else that looks like an entry point is `OnDependencyEvaluationChanged`, which is downstream by construction.

`ScopedObservableExpression` — the wrapper, and the only thing a caller or a query class ever subscribes to — raises `PropertyChanged` in exactly one place and `PropertyChanging` in one other. While a propagation is in flight the first marks itself pending and enlists instead of raising, and the second raises once and suppresses afterward. Without that second half the change would have traded two changed notifications for one changed and N changing.

The flush is two passes, clearing every pending flag before raising any notification, so that a throwing subscriber cannot leave the observations behind it permanently marked and therefore permanently silent. It swaps the list out before walking it, so a handler that starts a new propagation gets its own list, and returns the list afterward so it is reused rather than allocated per propagation.

## ObservableExpressionDiamondBenchmarks

`ChainChange` is a single-path expression, `DiamondChange` the same mutation through a diamond. The costly variants put a fixed `Thread.SpinWait` in the handler, standing in for what a boundary event actually costs.

| | before | after |
|--- |---: |---: |
| `ChainChange` | 82.63 ns, 312 B | 92.26 ns, 312 B |
| `DiamondChange` | 124.57 ns, 488 B | 134.06 ns, 488 B |
| `CostlyChainChange` | 37,688.55 ns, 312 B | 37,541.76 ns, 312 B |
| `CostlyDiamondChange` | 75,675.48 ns, 488 B | **37,672.92 ns, 488 B** |

Before, `CostlyDiamondChange` divided by `CostlyChainChange` was 2.008. After, it is 1.0035. The observer ran exactly twice and now runs exactly once, measured to three significant figures both times.

The single-path rows got 9.63 ns slower and the diamond row 9.49 ns slower. That is the mechanism's rent, and it is paid by every observation whether or not it will ever glitch. Allocation did not move on any row, which is what confirms the pending list is reused.

An intermediate revision used `Interlocked.Exchange` on the pending flag. Replacing it with a plain field recovered 2.66 ns of the rent. Both outcomes of the race it guarded are harmless: two threads racing to mark either enlist an observation twice, which is what happened before any of this existed, or skip one enlist, in which case the surviving flush still raises after both values have settled. Neither loses a notification carrying information the consumer does not already have.

## What the fan-out benchmark actually measures

| | before | after |
|--- |---: |---: |
| `ChangeTheSharedValue` | 21.01 μs, 86.26 KB | 21.23 μs, 86.26 KB |
| `ConstructAndDisposeWithFanOut` | 2,950.76 μs, 4222.57 KB | 2,959.28 μs, 4222.57 KB |
| `ConstructAndDisposeWithoutFanOut` | 2,232.40 μs, 3656.58 KB | 2,271.33 μs, 3656.58 KB |

Allocation is identical to the byte on all three rows. Time moved about one percent, at the edge of the error bars.

That row measures less than it appears to. `ChangeTheSharedValue` toggles a threshold between 0 and 1 across people ranked 0 through 999, so exactly one comparison changes sides. The other 999 nodes re-evaluate, produce the same boolean, and are suppressed by the equality check in the `Evaluation` setter before they ever reach a wrapper. One thousand nodes touched, one boundary crossed. It measures the mechanism's cost per *propagation* at scale, not per *notification*.

## QueryFanOutFlipBenchmarks

Written to separate those two costs. Two independent thousand-element graphs so their thresholds cannot contaminate each other; one flips a single element, the other moves the threshold to the median and flips five hundred.

| | | |
|--- |---: |---: |
| `FlipOneElement` | 21.76 μs | 86.26 KB |
| `FlipHalfTheElements` | 82.38 μs | 179.82 KB |

The marginal cost of a notification at the query layer is (82.38 − 21.76) ÷ 499 = **121.5 ns**, and 192 bytes.

Against that, the whole of this mechanism's rent — scope entry and exit, both propagation checks, the enlist, the flag, and the flush — was measured at 9.63 ns on `ChainChange`, which is one propagation carrying one notification. So the per-notification share is bounded above by 9.63 ns, or **at most eight percent of what a notification already costs**, and is certainly less than that, since some of the 9.63 is the scope and the scope is paid once no matter how many observations are notified.

That bound comes from measurements already in hand. Tightening it would need this benchmark run against `main`, and the decision it would inform does not change at either end of the range, so it was not run.

## What was deliberately left alone

An expression whose value returns to where it started still raises one notification. `OptimizedDiamondNotifiesOncePerChangeEvenWhenItsValueIsUnchanged` covers a diamond over `Number > 5 == Number > 10` moving from 3 to 12: both comparisons flip, the result is `true` before and after, and the consumer is told something changed. Deferral removes the wrong value but not the spurious notification. Suppressing it needs the wrapper to remember its pre-propagation evaluation, which is per-observation memory and a separate decision.

The query composition graph has its own roots — the adapters that wrap a caller's collection — and its own per-query `NotificationDeferral`, which holds a lock across its flush because it spans one query. A diamond formed by composing queries is not covered here.

Consolidating the scope's two thread-static fields into one object, and merging the wrapper's check-then-enlist into a single call, would take a propagation from roughly eight thread-static lookups to four. That is the standing hypothesis for where the remaining rent lives. Given the bound above it does not pay for itself yet.

## A note on method

Four predictions were recorded before this was measured. Two held: that the costly diamond row would fall to parity with the costly chain row, and that the single-path row was the one to watch. Two failed, both because they ignored an analysis made two days earlier in this same work — that deferral suppresses the boundary event and leaves the redundant internal recompute alone. `DiamondChange` and its allocation were predicted to fall; the join still evaluates twice and still boxes twice, so neither did.

A fifth prediction, that the `Interlocked.Exchange` pair accounted for most of the rent, was wrong by a factor of four.

One benchmark run showed a suspiciously low mean on `CostlyDiamondChange` with a standard deviation eighty times its sibling's. The question that mattered was not whether the number was noisy but whether some fraction of propagations were skipping a notification, and a benchmark cannot answer that. `DiamondNotifiesOncePerChangeAcrossManyChanges` asserts one notification per change across a hundred thousand changes; it passes, and the dispersion did not reproduce.
