# Benchmark comparison, 31 August 2026 — comparing before boxing

`DirectObservableExpression<TArgument, TResult>.Evaluate` computed a typed result and assigned it to `ObservableExpression.Evaluation`, which is `(Exception? Fault, object? Result)`. Every evaluation of a value-typed expression therefore boxed — and it boxed *before* the setter's equality check, so an evaluation that changed nothing still allocated, compared the new box against the old, and dropped both.

`Evaluate` now asks whether the new result already equals the current one before assigning. The previous value is already in the base as a box, so `current.Result is TResult previous` unboxes it without allocating and no shadow field is needed, which also means nothing can drift out of sync with the base.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ResultBoxingBenchmarks* *QueryFanOutBenchmarks*
```

## The isolating suite

`ResultBoxingBenchmarks` holds a thousand observations against one shared notifier. `person => signal.Rank` and `person => signal.Marker` are identical in every structural respect — one subscription site, one attachment per observation, one shared source, one notification each — and differ only in whether the result is a value type. Both ignore `person`, which is what lets them match; that makes them degenerate shapes for construction, but construction is not what this measures.

| arm | before | after | |
|--- |---: |---: |---: |
| `ChangeValueTypedResult` | 16.668 μs / 24,048 B | 16.823 μs / 24,048 B | control |
| `ChangeReferenceTypedResult` | 12.976 μs / 48 B | 13.495 μs / 48 B | control |
| `TouchWithoutChangingValueTypedResult` | 5.988 μs / 24,000 B | **4.454 μs / 0 B** | |

`TouchRank` raises `PropertyChanged` without changing anything, so every observation re-evaluates to the answer it already had. That arm allocated **exactly 24.000 bytes per observation** — a boxed `int` on x64 and nothing else — to deliver zero notifications. It now allocates nothing.

The two changing arms are the control, and their allocation is byte-identical before and after, because a result that genuinely changes still boxes.

## The real workload

`QueryFanOutBenchmarks.ChangeTheSharedValue` is a thousand people ranked 0 to 999 observed through `person => person.Rank > threshold.Rank`, with the threshold moving between 0 and 1. Exactly one person's verdict flips; nine hundred ninety-nine do not.

| | before | after | |
|--- |---: |---: |---: |
| `ChangeTheSharedValue` | 6.418 μs / 24,238 B | **4.271 μs / 264 B** | **1.50× / 91.8×** |

Per observation, 24.24 bytes became 0.264.

The allocation figures are deterministic and were taken from `2026-08-31-wider-eligibility.md`. The time figures are not on a common run — the recorded 6.418 μs predates the optimizer memoization and the diagram scratch buffer, both of which are construction-path changes that should not touch propagation, but neither was measured on this arm to confirm it.

## The prediction

Written before the run: `TouchWithoutChangingValueTypedResult` to **0 B**, the two changing arms unmoved, and `ChangeTheSharedValue` to **about 262 bytes** — the one surviving box plus a floor I could account for.

**Measured: 0 B, unmoved, and 264 bytes.**

Count times known unit cost again, and again it held. The prediction that established the mechanism in the first place was of the same class and was exact in all three arms once one constant was corrected: `PropertyChangedEventArgs` is 24 bytes, not the 32 assumed, so the two-args floor is 48.

## A regression introduced and removed, recorded rather than buried

The first cut ran the comparison for every result type. A reference-typed result never boxes — assigning the tuple is a struct write — so for those the check bought nothing, and `EqualityComparer<object>.Default.Equals` is two virtual calls where `EqualityComparer<int>.Default.Equals` devirtualizes.

`ChangeReferenceTypedResult` went from 12.976 μs to 15.153 μs, **16.8% slower at about nine standard deviations**, while the value arm moved 2.2% in the same pair of runs. The guard now includes `typeof(TResult).IsValueType`, which the JIT folds away for a value-type instantiation, and the arm returned to 13.495 μs. That is 0.52 μs above the original against a StdDev of 0.42 in the recovery run, which is inside the noise; there is also no mechanism for a residual, since a false guard is one field load and a branch.

The control caught this. It was in the suite to prove the *value* arm's saving was attributable, and it earned its place by failing instead.

## What this does not do, and why the rest is not recommended

`2026-08-30-direct-subscription-ceiling.md` set two conditions for building the fast path. The second, verbatim: *"The result must not be boxed, which is where a third of the propagation allocation advantage comes from."* The implementation did not meet it, and this change meets it only for results that do not change.

Typing the result end to end would meet it fully. It is not recommended on the evidence. Of the 264 bytes left in the realistic workload, exactly 24 is the surviving box — nine percent — and buying it means splitting the `ScopedObservableExpression` hierarchy, which caches its last-seen value as `(Exception?, object?)` in the untyped base, and adding a direct-specific wrapper. The pathological case was never a changing result; it was a thousand observations re-evaluating to the same answer, and that case is now free.

Parked with the reasoning recorded, as the multi-tier structural cache was.

## 192 bytes not accounted for

Of the 264, this document can name 72: one box and two `EventArgs` from `SetBackedProperty`. The remaining **192 bytes are unexplained**. They were roughly 190 before this change as well, so they are not per-observation and not something this change created. `PropagationScope` is a `readonly ref struct` whose pending list is thread-static and handed back to itself after flushing, so it is not that either.

Recorded as open rather than folded into a figure presented as understood.

## How this was found

By re-checking a number. `2026-08-31-wider-eligibility.md` reported "about 78 bytes per element" of attachment overhead and proposed pooling as a follow-up. That figure is the difference between two *different predicates* in `QueryFootprintBenchmarks`, which differ in tree, plan and site count — not a measurement of an attachment, which is 56 bytes by field layout. The document attributed the difference to `DirectSubscriptionAttachment` and hedged with "accounts for most of the difference," which is a story about which code allocates rather than arithmetic, and that is the class that has failed every time in this series.

Pooling was abandoned on two grounds: 56 bytes against roughly 1,253 the query spends per element all-in, and a recycled attachment can be re-attached to a different source while `NotifyAttachments` is mid-walk, which would send the walk into the wrong list.

The boxing was found while reading `Evaluate` to price the attachment.
