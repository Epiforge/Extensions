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

## The direction this does not help, measured afterwards

The measurement above priced the case the memoization helps and not the case it cannot, which another session's review made plain: memoization is keyed on the expression instance, so a caller that hands `Observe` a freshly built lambda every call gets nothing from it.

| | mean | allocated |
|--- |---: |---: |
| fresh lambda, optimizer configured | 6,058.9 ns | 4,498 B |
| fresh lambda, no optimizer | 2,119.5 ns | 3,995 B |
| held lambda, optimizer configured | 2,606.1 ns | 3,271 B |
| held lambda, no optimizer | 2,585.6 ns | 3,271 B |

**Held: 20.5 nanoseconds and no allocation, down from 3,106 and 383 bytes. Fresh: 3,939.4 nanoseconds and 503 bytes, unmitigated.** The optimizer's whole cost, still paid, every call.

Note what cannot be concluded from this table. The two no-optimizer arms differ by 466 ns — the fresh-lambda one is the *faster* of the two, which building a lambda first should not make it — so the two pairs are not on a common baseline and subtracting across them is invalid. That rules out the arithmetic worth wanting, which would have priced the now-wasted weak-table insert a fresh caller pays. Within each pair the comparison is sound, because both sides build the same thing; across pairs it is not, and the difference is unexplained.

What the number is good for is counting. A consumer that builds a fresh expression per cell over eight columns and fifty rows pays four hundred optimizer passes where eight would do, and at 3,939 ns each that is about 1.6 milliseconds of a page render reducible to about 31 microseconds — a floor, since a display formula is a larger expression than `person => person.Rank * 2`. The fix for that is on the caller's side: hold the expression per column. No instance-keyed cache in this library can reach a caller who never reuses an instance.

## The structural fallback, reconsidered on evidence

A second cache keyed by structural equality would reach exactly that caller, and it was ruled out earlier on the judgement that rebuilding the same expression at a different address is too rare to carry machinery for. Two numbers measured since bear on it: the pass it would avoid costs 3,939 ns, and a structural lookup costs roughly what diagram generation costs, measured at 1,013 ns for a three-node tree in `2026-08-31-graph-construction-share.md`.

So it would pay for itself on time, which the earlier judgement assumed it would not. It is still not recommended. Structural keys mean a real dictionary holding expressions alive rather than a weak table, so it needs an eviction policy nothing else here needs; and where a caller can hold its expression, the caller-side fix is both cheaper and simpler. Recorded because the judgement was sound on the evidence available then and the evidence has changed.

## A public behavior note

`ExpressionObserver.Optimizer` is no longer reference equal to the method supplied through the options — it is that method wrapped. It behaves identically otherwise, and the memoization assumes what the structural cache key already assumed: that optimizing the same expression twice yields the same answer.

This wants a release note when the branch merges.
