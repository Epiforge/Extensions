# Direct subscription: the ceiling

Measured before building the execution path, to decide whether to build it at all.

`DirectSubscriptionCeilingBenchmarks`, .NET 10.0.11, Intel Core Ultra 9 275HX, one launch, `[Params(100, 1000)]`.

## What was compared

The graph observing an expression, against a hand-rolled class doing what a fast path would do for the same expression: a delegate compiled once as a static, a direct `+=` to each source, a property-name filter matching `ObservableMemberExpression`'s (own name or empty), and compare-then-notify with `PropertyChanging` before `PropertyChanged`.

The hand-rolled version is a **ceiling, not a proposal.** It does not enter a `PropagationScope`, because that type is internal to the library — so a real implementation would be somewhat slower than these numbers. Erring that way is deliberate: an upper bound that flatters the mechanism is the one worth testing against, since a thin result would then be conclusive.

Two shapes, both of which survive the eligibility rule:

- **Selector** — `person => person.Rank`, the dominant shape in query key selectors
- **Comparison** — `person => person.Rank > threshold.Rank`, the archetype, with `threshold` captured in a closure and shared across every observation

## Propagation, per observation

| shape | graph | direct | speedup | graph bytes | direct bytes |
|--- |---: |---: |---: |---: |---: |
| selector, N=100 | 42.8 ns | 9.9 ns | 4.34× | 72 | 48 |
| selector, N=1000 | 46.9 ns | 10.6 ns | 4.43× | 72 | 48 |
| comparison, N=100 | 52.5 ns | 10.0 ns | 5.25× | 96 | 48 |
| comparison, N=1000 | 59.0 ns | 11.0 ns | 5.37× | 96 | 48 |

Both mechanisms are linear in the number of observations, and the ratio is stable across a tenfold change in scale. **Direct subscription is between four and five and a half times faster to propagate a change, and allocates a third less.**

The allocation gap has a structural explanation rather than a statistical one. A node holds its value as `(Exception? Fault, object? Result)`, so every evaluation of a value-typed expression boxes the result: 24 bytes for the selector's `int`, and the comparison pays it twice for 48. The hand-rolled version holds `int` and `bool` in typed fields and boxes nothing. The remaining 48 bytes on both sides are the source object's own `PropertyChangedEventArgs` and `PropertyChangingEventArgs`, which no mechanism can avoid.

**A fast path does not box its result.** That is not a tuning opportunity; it is a consequence of having no interior structure that must hold values uniformly.

## Construction, per observation

| shape | graph | direct | speedup | graph bytes | direct bytes |
|--- |---: |---: |---: |---: |---: |
| selector, N=100 | 1,484 ns | 19.8 ns | 74.8× | 2,371 | 184 |
| selector, N=1000 | 1,723 ns | 28.6 ns | 60.2× | 2,371 | 184 |
| comparison, N=100 | 2,407 ns | 276 ns | 8.72× | 4,579 | 2,254 |
| comparison, N=1000 | 3,569 ns | 3,121 ns | 1.14× | 4,559 | 16,676 |

The selector rows are the headline: **sixty to seventy-five times faster and thirteen times smaller.** Observing `person => person.Rank` through the graph costs 1.7 μs and 2.4 KB. A `Where` or `Select` over ten thousand elements creates one observation per element — `ObservableCollectionWhereQuery` calls `ObserveWithoutOptimization(Predicate, element)` in its per-element path — so that is 17 ms and 24 MB spent on construction alone, against 0.3 ms and 1.8 MB.

The comparison rows are the finding.

## The shared source is quadratic, and that is the real result

A prediction made before the run, and confirmed: `DirectComparisonObserve` grows **113×** for a tenfold increase in observations, where `GraphComparisonObserve` grows 14.8×. Allocation per observation rises from 2,254 bytes to 16,676. The crossover is at roughly a thousand observations; extrapolating the quadratic, direct would lose by an order of magnitude at ten thousand.

The cause is not subtle. Every hand-rolled observation subscribes to the same `threshold` object, and `Delegate.Combine` copies the whole invocation list on each `+=`, so N subscriptions cost N²/2 copies. The graph does not pay this: it caches `closure.threshold.Rank` as **one** node with **one** subscription, and the N observations attach to it through the intrusive dependent list at O(1) each.

This is the expression-caching hazard the design document recorded — *"At high fan-out over a shared subexpression, the fast path may lose. That is a measurement to make, not a reason to stop."* It has now been made, and it says something sharper than the hazard anticipated. The loss is not a constant factor to be weighed. It is a change in complexity class, and it would turn the mechanism from a 60× win into a 9× regression on exactly the workload it was built for.

**So a fast path must share its subscriptions to shared sources.** Not as an optimization — as a condition of being correct about performance at all. The machinery already exists: `ObservableExpressionSubscription`, the intrusive dependent list with tombstones built for `wrapper-deferral`, is precisely a way to attach many dependents to one source at O(1). What the execution path needs is a cache keyed by the resolved source object, the event kind, and the property name, holding one real subscription and one intrusive list of the observations that want it.

That is borrowing the graph's trick rather than discarding it, and it should be designed in from the first line rather than discovered later.

## Verdict

Build the execution path. The prize is 4–5× on propagation, 60× on construction, and a third to a thirteenth of the allocation, on the shapes that survived the eligibility rule — which are the shapes query composition creates by the thousand.

Two conditions, both established here rather than assumed:

1. Subscriptions to sources shared across observations must be shared, or the mechanism is asymptotically worse than what it replaces.
2. The result must not be boxed, which is where a third of the propagation allocation advantage comes from.

## Noticed in passing, not chased

Graph construction costs 2.4 KB for the simplest possible expression. `ExpressionEqualityComparer` builds a `Diagram` — an `IReadOnlyList<object?>` of elements — to key its cache lookups, and construction performs several. That is a candidate for its own investigation, independent of direct subscription, and it would benefit every observation rather than only the eligible ones. Recorded, not pursued.

## Method

Both predictions were stated before the run. The first — that direct would win selector construction substantially — was right but understated; I expected a large factor and did not expect sixty. The second — that the shared closure would go quadratic — was right, and was the reason the benchmark was written before the execution path rather than after.

The pattern from the typed-comparers baseline holds again: *every prediction that multiplied a count by a measured unit cost has held; every prediction that told a story about which code was hot has failed.* The quadratic prediction was arithmetic about `Delegate.Combine`, not a story. It held.
