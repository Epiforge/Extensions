# Benchmark comparison, 2 September 2026 — the graph's boolean boxes

`2026-09-01-boxed-booleans.md` gave the direct path two shared boxes for a `bool` result and parked the same change for the graph, which boxes every node's result on every evaluation, changed or not. This is that change.

It also corrects that document's account of where the boxing is. It named three sites. There are six.

## The six sites

| site | how it boxed |
|--- |--- |
| `ObservableBinaryExpression` | `Expression.Convert(operation, typeof(object))` in the compiled delegate |
| `ObservableUnaryExpression` | the same |
| `ObservableCoalesceExpression` | the same, in its conversion delegate |
| `ObservableTypeBinaryExpression` | its delegate returned a typed `bool` which boxed at the assignment |
| `ObservableAndAlsoExpression` | `Evaluation = (null, false)` and `Evaluation = (null, value)` |
| `ObservableOrElseExpression` | `Evaluation = (null, true)` and `Evaluation = (null, value)` |

The last three were missed by the earlier note because they do not pass through a compiled delegate at all, and a search for `Expression.Convert` cannot find them. `ObservableAndAlsoExpression` and `ObservableOrElseExpression` matter most of the six, because a short-circuiting predicate is how a compound condition is ordinarily written and neither operator is eligible for direct subscription.

A new `BooleanBoxes` holds the two boxes and exposes `Convert`, which emits `Expression.Condition(operation, True, False)` when the operation's type is exactly `bool` and the old conversion otherwise. One decision point rather than six copies of the same test. `DirectObservableExpression` now shares that pair instead of holding its own, so the claim that only two boxes ever exist is literal.

The guard reads the type off the constructed operation rather than off the cache key, so a lifted comparison producing `bool?` keeps the old path. That is deliberate: `Nullable<bool>` has instance members and `bool` has none, which is what the safety argument below rests on.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *GraphResultBoxingBenchmarks* *ResultBoxingBenchmarks* *PropagationFloorBenchmarks*
```

## The suite

`GraphResultBoxingBenchmarks` flips a thousand ranks and counts what the graph spends. Every arm runs on `UseDirectSubscription = false` and `ObserveWithoutOptimization`, so the graph is what executes and the optimizer cannot fold the negation away. Two of the four arms are controls this change cannot reach: one observes nothing at all, and one observes an `int`.

The before run priced every arm exactly, which is what made the prediction possible.

| arm | before | per element | account |
|--- |---: |---: |--- |
| `FlipEveryRankWithNothingObserving` | 46.88 KB | 48 B | the element's own two `EventArgs` |
| `FlipEveryRankObservedAsAnInteger` | 93.75 KB | 96 B | and two boxed `int` |
| `FlipEveryRankObservedAsAComparison` | 117.19 KB | 120 B | and one boxed `bool` |
| `FlipEveryRankObservedAsANegatedComparison` | 140.63 KB | 144 B | and two boxed `bool` |

Every figure an exact multiple of twenty-four over a forty-eight byte floor, and every node accounted for. Nothing was left over.

## The prediction, which held to the byte

Written before the run: removing one box per boolean node leaves every observed arm at exactly the integer arm's figure, because what remains in each is the same two boxed integers.

| arm | before | after | predicted |
|--- |---: |---: |---: |
| `FlipEveryRankWithNothingObserving` | 46.88 KB | 46.88 KB | 46.88 |
| `FlipEveryRankObservedAsAnInteger` | 93.75 KB | 93.75 KB | 93.75 |
| `FlipEveryRankObservedAsAComparison` | 117.19 KB | **93.75 KB** | 93.75 |
| `FlipEveryRankObservedAsANegatedComparison` | 140.63 KB | **93.75 KB** | 93.75 |

**All four exact.** Three arms of different shapes converging on one number is a stronger check than any single ratio, because nothing but the right mechanism puts them there.

Count times known unit cost again. That class of prediction has now held on every occasion it has been applied correctly in this series.

## The confinement controls

`ResultBoxingBenchmarks` and `PropagationFloorBenchmarks` both observe through the direct path, which this change touches only by moving its two boxes into a shared class.

| | recorded | now |
|--- |---: |---: |
| `ChangeValueTypedResult` | 24,048 B | 24,048 B |
| `ChangeReferenceTypedResult` | 48 B | 48 B |
| `TouchWithoutChangingValueTypedResult` | 0 B | 0 B |
| `ChangeAffectingNoElement` | 48 B | 48 B |
| `ChangeAffectingOneElement` | 80 B | 80 B |

Byte-identical, all five. `ResultBoxingBenchmarks` is in the suite because its reference arm caught a 16.8% regression when the first boxing guard went in; it earns its place again by refusing to move.

## The time column, which did not close — a finding

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryRankWithNothingObserving` | 8.158 μs | 8.882 μs | **+8.9%, and it cannot have moved** |
| `FlipEveryRankObservedAsAnInteger` | 53.365 μs | 53.586 μs | +0.4%, control |
| `FlipEveryRankObservedAsAComparison` | 68.930 μs | 63.846 μs | −7.4%, one box removed |
| `FlipEveryRankObservedAsANegatedComparison` | 78.036 μs | 71.717 μs | −8.1%, two boxes removed |

Two things looked wrong here. A three-launch re-run settled the first and disproved my account of the second.

**A control that cannot move, moved — and it was drift.** `FlipEveryRankWithNothingObserving` constructs no observations; its measured loop flips a thousand `Rank` properties on objects nothing is subscribed to, and no code path it executes was altered. Its allocation is unchanged to the byte. Three launches put it at **8.386 μs mean, 8.201 median, with a standard deviation of 0.391** — against a before of 8.158, which is inside one deviation of that mean. The single-launch 8.882 was a tail sample.

The number worth keeping is the deviation. At one launch this arm reported 0.060 μs; at three it reports 0.391 — **6.5 times larger for the same code**. A within-run standard deviation is a statement about sampling inside one process and says nothing about the variance between processes. The fifth cut of `2026-08-30-direct-subscription-first-cut.md` put that at ±20% for construction arms; this is the same phenomenon on a propagation arm small enough to show it cleanly.

## The account of the time saving which was wrong

Written in the first draft of this document: that one box removed bought 5.084 μs and two bought 6.319 μs, that this is not the 2:1 a fixed per-box cost predicts, and that the discrepancy was a finding about the run.

**It was a finding about the method, and specifically about differencing arms.** The three-launch figures, all within one run so no drift can enter:

| | before | after | marginal cost of that node |
|--- |---: |---: |---: |
| comparison − integer | 15.565 μs | 10.517 μs | the first boolean node |
| negated − comparison | 9.106 μs | 7.556 μs | the second boolean node |

The two boolean nodes cost **15.57 and 9.11 μs before the change** — they were never equal, and nothing about this change made them unequal. A node at the root of an observation assigns a result which the scope wrapper then compares and announces; a node in the middle assigns a result which only its parent reads. Those are different amounts of work, so the arms were never linear in boolean-node count and a per-node figure obtained by subtracting one from another was never going to be clean.

The deeper error is that **allocation differences between arms are additive and decomposable; the time they cost is not.** Twenty-four bytes per element does not have a time price of its own — what it buys is a share of a collection, and collection cost is a property of the whole arm's allocation rate. Attributing a time saving to an individual allocation site by differencing two arms that differ in total allocation is not arithmetic, it is a story about which code is hot, which is the class of claim that has failed every time in this series. It failed here too, in a document that quotes the rule three sections earlier.

So: **no per-box time cost is quoted.** What is quoted is what the instrument can support — the two boolean arms improved 7.4% and 8.5%, the integer control moved 0.1% and the bare control's excursion is drift. The allocation column is deterministic, hit four predictions exactly, and is the column this change is judged on.

| arm | before | after, three launches | |
|--- |---: |---: |---: |
| `FlipEveryRankWithNothingObserving` | 8.158 μs | 8.386 μs | drift, ±0.391 |
| `FlipEveryRankObservedAsAnInteger` | 53.365 μs | 53.324 μs | −0.1%, control |
| `FlipEveryRankObservedAsAComparison` | 68.930 μs | 63.841 μs | −7.4% |
| `FlipEveryRankObservedAsANegatedComparison` | 78.036 μs | 71.397 μs | −8.5% |

The integer control holding to 0.1% across a run in which the bare control moved 2.8% is itself worth noting: the arms are not drifting together, so a run-wide correction factor does not exist and should not be invented.

## Why sharing a box is safe in the graph

The direct path's argument was that its result is never a graph node's input. That does not transfer, so the two places which compare a result by reference were checked directly rather than reasoned about.

`ObservableMemberExpression` line 81 compares `previousValue` against `value` where both come from `field.GetValue` — a box made by reflection, never a node's result. Line 52 and `ObservableIndexExpression` line 61 compare the *target* of a member or index access, which is a child node's result. A boolean cannot be either: `System.Boolean` exposes no instance member and no indexer, so no `MemberExpression` or `IndexExpression` can have a `bool`-typed target. Restricting the change to non-nullable `bool` keeps `Nullable<bool>`, whose `HasValue` and `Value` are exactly such members, on the old path.

Disposal cannot reach a shared box either. `DisposeIfNecessaryAndPossible` tests the value against the disposal interfaces, and a boxed `bool` implements neither — so even a user-defined boolean-returning operator registered for disposal is a no-op on it.

Everything else downstream compares by value: the `Evaluation` setter through `FastEqualityComparer`, the scope through `EqualityComparer<TResult>.Default`, the queries through their element comparers. No caller can hold the box at all, since `IObservableExpression<TResult>.Evaluation` returns an unboxed `TResult`.

`BooleanResultIdentity` was written before the change and pins the failure modes which would matter: a negated comparison and a type test across both mechanisms, each asserting the value at every step and an announcement on every crossing; and a conjunction and a disjunction differentially against the compiled predicate, asserting that what was announced is exactly the sequence of values the predicate actually changed to.

## What this opens, and what it does not

The graph still boxes every non-boolean result on every evaluation. An `int` node is 24 bytes per evaluation and there is no shared-box trick for it — the values are unbounded. Nothing here changes that, and the two boxed integers left in every arm above are that cost, measured.

A separate finding, established by reading `ReflectionExtensions.FastDefault` rather than by measuring: it invokes a cached delegate returning `default(T)` boxed, so it **allocates a fresh box on every call**. `ObservableExpression`'s constructor calls it once per node, which means every node over a value type pays a box at construction — on the graph, one per node per element. That is construction-side, invisible to this benchmark, and gets its own before-and-after.
