# Benchmark comparison, 1 September 2026 — sharing the two boolean boxes

`DirectObservableExpression<TArgument, TResult>.Evaluate` computes a typed `TResult` and assigns it to `ObservableExpression.Evaluation`, which is `(Exception? Fault, object? Result)`. A `bool` which genuinely changed therefore boxed on every change. There are only two values a `bool` can take, so there need only ever be two boxes.

`2026-08-31-result-boxing.md` removed the box for a result which *did not* change, and recorded that the surviving box was nine percent of the remaining allocation and not worth the structural cost of typing the result end to end. That reasoning still holds. This buys the same box for two static fields and a comparison instead.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

`QueryNotificationBenchmarks` on `DefaultJob`; the other two on three launches.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryNotificationBenchmarks* *PropagationFloorBenchmarks* *ResultBoxingBenchmarks*
```

## The suite

`QueryNotificationBenchmarks` flips the membership of every one of a thousand elements in a filtered query, under `person => (person.Rank & 1) == 0`, which every flip crosses. Four arms strip one layer at a time: the people alone, the people with the predicate observed per person and no query at all, the query with nothing subscribed, and the query with a subscriber.

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 171.88 KB | **148.44 KB** | 13.6% |
| `FlipEveryMembershipWithNothingObserving` | 101.56 KB | **78.13 KB** | 23.1% |
| `FlipEveryRankObservedWithoutAQuery` | 70.31 KB | **46.88 KB** | 33.3% |
| `FlipEveryRankWithNoQuery` | 46.88 KB | 46.88 KB | control |

The third arm now equals the control. **A thousand observations of a boolean predicate, every one of them crossing, allocate nothing at all** — the whole 46.88 KB is the people announcing their own property changes.

## The independent check

`PropagationFloorBenchmarks` was written a day earlier, by a different session, for a different question. Nothing subscribes to either of its queries.

| arm | before today | after the notification work | after this | |
|--- |---: |---: |---: |---: |
| `ChangeAffectingNoElement` | 48 B | 48 B | 48 B | control |
| `ChangeAffectingOneElement` | 264 B | 104 B | **80 B** | 3.3× over the day |

The two arms are now also the same speed — 3.888 μs against 3.884 μs, a ratio of 1.00 where it was 1.04. An element crossing the predicate costs no measurable time and 32 bytes.

## The confinement control

`ResultBoxingBenchmarks` observes an `int` and an `object`. Neither may move; the guard is `typeof(TResult) == typeof(bool)`.

| arm | before | after |
|--- |---: |---: |
| `ChangeValueTypedResult` | 24,048 B | 24,048 B |
| `ChangeReferenceTypedResult` | 48 B | 48 B |
| `TouchWithoutChangingValueTypedResult` | 0 B | 0 B |

Byte-identical. This class is in the suite because its reference arm caught a 16.8% regression when the boxing comparison first went in, and it earns its place again here by refusing to move.

## The prediction

Written before the run: 46.88, 78.13 and 148.44 KB; 80 B and 48 B; and both `ResultBoxing` arms unmoved.

**Measured: every one of the eight, exactly.**

Count times known unit cost, again. The units are now all established and all confirmed twice: a `PropertyChangedEventArgs` or `PropertyChangingEventArgs` is 24 bytes, a boxed `bool` or `int` is 24, a single-item `NotifyCollectionChangedEventArgs` with an index is 72, a two-element `List<object>` with its array is 88.

## What a flip costs now

Per element crossing the predicate, with nothing subscribed:

| | bytes |
|--- |---: |
| the element's own `PropertyChanging` and `PropertyChanged` arguments | 48 |
| the query's bookkeeping | 32 |
| **total** | **80** |

At the start of the day it was 216, and 24 of those 136 bytes were the box. The 32 has not been identified. It is the last unnamed figure in the propagation path and it now appears in two independent benchmarks at the same size.

## Why this is safe

A shared box is visible only to code which compares results by reference. Two places do: `ObservableMemberExpression` compares the target of a member access, and compares a field's previous value against its next. Both operate on the graph's own nodes. A direct observation's result is never a graph node's input — the direct path replaces the whole graph for an expression, it does not participate in one — and everything downstream compares by value: the `Evaluation` setter through `FastEqualityComparer`, the scope through `EqualityComparer<TResult>.Default`, the queries through their element comparers.

No caller can hold the box at all. `IObservableExpression<TArgument, TResult>.Evaluation` returns `(Exception?, TResult)`, so a consumer receives an unboxed `bool`.

`DirectSubscriptionExecution.ABooleanResultIsAnnouncedOnEveryCrossing` was written before the change and pins the failure mode which would matter: eight consecutive crossings, each announced exactly once with the right value, through both mechanisms.

## Not done

Every node in the graph boxes its result on every evaluation, through `Expression.Convert(operation, typeof(object))` in the compiled delegates of `ObservableBinaryExpression`, `ObservableUnaryExpression` and `ObservableTypeBinaryExpression`. The same two shared boxes would apply to any of those whose type is `bool`, and a comparison predicate has two such nodes rather than one.

That is a change to the graph rather than to the fast path, so it is a change to the observer's own model, and it wants its own evidence: a benchmark of an *ineligible* predicate, which nothing in the suite currently measures, and `DifferentialFuzz` as the judge.

Left for a later session with the reasoning recorded.
