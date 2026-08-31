# Benchmark comparison, 31 August 2026 — memoizing the optimizer

Sixteen places across `ObservableCollectionQuery` and `ObservableDictionaryQuery` share one shape:

```csharp
var key = predicate;
if (collectionObserver.ExpressionObserver.Optimizer is { } optimizer)
    key = (Expression<Func<TElement, bool>>)optimizer(key);
lock (cachedAllQueriesAccess)
    if (!cachedAllQueries.TryGetValue(key, out allQuery!))
```

The optimizer runs to compute a dictionary key, and then the lookup usually hits. `ExpressionObserver.ReplaceParameters` runs it a seventeenth time, once per observation, on a lambda the query layer holds and hands to every element.

`ExpressionObserver.Optimizer` is now the supplied method wrapped in a `ConditionalWeakTable<Expression, Expression>` memoization, so an expression instance is optimized once. Wrapping the property rather than editing seventeen call sites means no site can be missed, and it covers the per-observation pass as well as the per-probe ones.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches. `ExpressionOptimizer.tryVisit` from `Linq.Expression.Optimizer`, which is the optimizer this library's README recommends.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *OptimizerOverheadBenchmarks*
```

## Result

| | before | after | |
|--- |---: |---: |---: |
| `Where` cache hit, optimizer configured | 4,707.8 ns / 1,272 B | **154.9 ns / 768 B** | **30.4×** |
| graph observation, optimizer configured | 5,682.1 ns / 3,654 B | **2,669.4 ns / 3,271 B** | **2.13×** |
| `Where` cache hit, no optimizer | 140.0 ns / 768 B | 135.3 ns / 768 B | control |
| graph observation, no optimizer | 2,576.1 ns / 3,271 B | 2,593.1 ns / 3,271 B | control |

Configuring an optimizer used to cost **33.6×** on a cache hit and **2.21×** on an observation. It now costs **1.14×** and **1.03×** — twenty nanoseconds and seventy-six, which is the weak-table lookup.

**Allocation reached parity in both pairs**, to the byte. The optimizer's per-call garbage — 504 bytes on a hit, 383 on an observation — is gone entirely, because on a hit no tree is built.

The two unoptimized arms did not move, which is what makes the other two attributable.

## How this was found, and why it was invisible

Another Claude instance, reviewing an unrelated codebase that consumes this library, noticed the optimize-before-probe pattern while reading the query layer. It was not found here.

The reason it survived is worth recording: **not one benchmark in this repository configured an optimizer.** Every construction figure in every baseline document in this directory was measured with `Optimizer` null, while the *test* suite sets one on every observer it builds through `ExpressionObserverHelpers`. The suite that checks correctness used the recommended configuration; the suite that measures speed did not. A cost paid only under the documented recommendation was therefore unmeasurable by construction.

Every graph-versus-fast-path ratio recorded before this document is a lower bound for a consumer who follows the README, because the fast path skips `ReplaceParameters` and so never paid the per-observation pass at all.

## A public behavior note

`ExpressionObserver.Optimizer` is no longer reference equal to the method supplied through the options — it is that method wrapped. It behaves identically otherwise, and the memoization assumes what the structural cache key already assumed: that optimizing the same expression twice yields the same answer.

This wants a release note when the branch merges.
