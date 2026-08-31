# Benchmark comparison, 31 August 2026 — operators backed by methods

`DirectSubscriptionAnalyzer` refused any `Binary` or `Unary` node carrying a `Method`, which is every operator the compiler resolves to a method — string equality above all, since `person.Name == "Emily"` compiles to `String.op_Equality`. The reason recorded was disposal: the graph registers a method's return value for disposal, and the fast path disposes nothing.

Relaxing the refusal alone would have changed nothing, because `IsMethodReturnValueDisposed` answers true for every static method while `DisposeStaticMethodReturnValues` is at its default, and a C# operator is always static.

What makes it possible is that the graph's disposal of such a value is not a disposal. `ObservableBinaryExpression.GetShouldValueBeDisposed` is read by exactly one thing, `DisposeIfNecessaryAndPossible`, whose entire body is three runtime type tests against `IDisposable` and `IAsyncDisposable`. When the return type cannot implement either, all three fail and nothing is disposed. The fast path's failure to dispose it is therefore not a difference in behavior.

```csharp
static bool CannotBeDisposed(Type type) =>
    type.IsSealed && !typeof(IDisposable).IsAssignableFrom(type) && !typeof(IAsyncDisposable).IsAssignableFrom(type);
```

`IsSealed` holds for value types and enums as well as sealed classes, and fails for interfaces and unsealed classes, where a runtime value could be a derived type which is disposable. `op_Equality` returns `bool`.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches, a thousand elements.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *OperatorEligibilityBenchmarks*
```

## Method

Every figure below comes from one run in one process. `direct` and `graph` are two `CollectionObserver` instances differing only in `UseDirectSubscription`, so each pair is a ratio measured against its own baseline rather than against a figure recorded on another day. Nothing here is subtracted across instruments.

The predicates are `static readonly` fields rather than lambdas written at the call site. An expression tree written inline is rebuilt on every invocation, and the compiled-lambda cache is keyed per instance, so an inline predicate makes the direct arms pay an `Expression.Compile` per call which a caller holding its predicate never pays.

## Result

| arm | mean | allocated |
|--- |---: |---: |
| `StringComparisonDirect` | **367.5 μs** | **1.22 MB** |
| `StringComparisonGraph` | 2,360.7 μs | 2.68 MB |
| `RankComparisonDirect` | 368.9 μs | 1.24 MB |
| `RankComparisonGraph` | 2,351.8 μs | 2.69 MB |
| `DeferredBranchDirect` | 7,992.8 μs | 6.67 MB |
| `DeferredBranchGraph` | 7,793.9 μs | 6.67 MB |

`person => person.Name == "Emily"` is now **6.42× faster to construct on 2.20× less memory**. `person => person.Rank > 0`, eligible all along, is 6.38× and 2.17× in the same run.

**The two ratios agree to within one percent.** That is the whole claim: a string comparison is not merely faster than it was, it now costs what an integer comparison costs, because it takes the same path and its plan is the same shape — one property subscription on the argument against a constant.

## The control

`person => person.Rank % 2 == 0 && person.Name.Length > 1` is ineligible under both observers — `&&` is a deferred branch — so the direct observer falls back to the graph and the two arms should be the same measurement twice.

They allocate **6.67 MB each**, with `Gen0`, `Gen1` and `Gen2` identical across all three columns, and differ by 2.6% on time against standard deviations of 266 and 211 ns. The report's megabyte precision cannot establish byte equality, but the generation counts matching exactly is what would break first if the two observers differed in anything but the variable under test.

Without this arm the table would be four numbers with no evidence that the two observers are otherwise the same object.

## The prediction

Written before the run: `StringComparisonDirect` at 460–520 μs and about 1,280 KB, landing beside `RankComparisonDirect`; the deferred-branch pair equal to each other.

**Both held.** The string arm landed on the rank arm within 0.4% on time and 1.6% on allocation, and the control pair matched.

Two absolute figures were off, and both have accounts rather than excuses:

`RankComparisonGraph` was predicted at about 3,300 KB and measured 2,754. The prediction was taken from `2026-08-30-queries-after-direct-subscription.md`, which predates the diagram scratch buffer — and that change cut graph observation allocation by close to a fifth. 3,324 KB less eighteen percent is 2,726. The gap is a change already measured in this directory.

`RankComparisonDirect` was predicted at about 460 μs and measured 369. The prediction was taken from `ConstructAndDisposeWithFanOut`'s sibling arm, which writes its predicate inline and therefore compiles a fresh lambda on every invocation. Hoisting the predicate removes that. This is an inference rather than arithmetic — but it is the direction the design note above predicted, and the graph arms, which compile nothing, landed within two percent of prediction.

## What was tested, and what one test would have missed

`StringComparisonIsIneligible` already existed in the analyzer suite, pinning this exact predicate as ineligible. Inverting it is the visible half of the change.

The invisible half is that the rule has two conditions and only one of them was reachable by the existing tests. `TestPerson` and `DisposableTestPerson` are both unsealed, so both fail `IsSealed` and neither reaches the disposal check — `!typeof(IDisposable).IsAssignableFrom(type)` could have been deleted with the whole suite still green. `SealedDisposableTestPerson` exists to close that, and the two older operator tests were renamed to `...ReturningUnsealedTypeIsIneligible` because their former names claimed something now false in general.

`StringComparisonIsEligible` also asserts `body.Method is not null` before analyzing. Without it the test would pass vacuously if the compiler ever stopped binding `String.op_Equality`, having never exercised the rule at all.

The strongest verification cost nothing to build. `DifferentialFuzz` has generated `Expression.Equal(Text, Text)` since before this change, so four hundred and fifty seeds' worth of programs which until now fell back to the graph in the fast world now take the fast path and are compared against the graph on both evaluation and notification count.

## Noticed, not pursued

The same reasoning applies to `isPropertyValueDisposed`, which is why `DateTime.Now` is ineligible: a static property's getter is a static method, so its value is registered for disposal, and `DateTime` cannot be disposed either. That case divides into two which do not deserve the same answer — a static property caught by the `DisposeStaticMethodReturnValues` default, where no one asked for the exclusion, and a property a consumer explicitly registered through `AddPropertyValueDisposal`, where overriding them is a judgement about their intent rather than about the graph's behavior.

Recorded as a separate question.
