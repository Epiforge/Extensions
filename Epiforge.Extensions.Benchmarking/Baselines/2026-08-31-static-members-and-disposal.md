# Benchmark comparison, 31 August 2026 — static members, and a disposal that never happens

Two eligibility rules, one API guard, and one bug introduced and caught in between.

`IsFixed` accepted a field only when it had a target expression, so a static field was not a fixed target and `SomeType.Setting.Threshold` built a graph. A static field raises no notification for the same reason an instance field does not, so both mechanisms already read one once and held it.

Separately, a static property's getter is a static method, so `DisposeStaticMethodReturnValues` — true by default — registered its value for disposal, and the analyzer refused it. `DateTime.Now` was ineligible. This is the same rule that refused method-backed operators until yesterday, and it yields to the same argument: `ObservableMemberExpression.GetShouldValueBeDisposed` is read only by `DisposeIfNecessaryAndPossible`, whose whole body is three runtime type tests, so when the type cannot implement either interface the graph's disposal is not a disposal.

That argument covers a blanket default nobody asked for. It does not cover a property a consumer *registered* through `AddPropertyValueDisposal`, where overriding them would be a judgement about their intent. So the options now refuse that registration instead: every path — properties, indexers, operators, `AddExpressionValueDisposal` — routes through `AddMethodReturnValueDisposal`, and it throws when the return type cannot implement a disposal interface. With the ambiguous registration impossible, the analyzer's rule needs no exception for it.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches, a thousand elements.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *EligibilityMatrixBenchmarks*
```

## Method

`EligibilityMatrixBenchmarks` runs five predicate shapes through two `CollectionObserver` instances differing only in `UseDirectSubscription`. Every ratio comes from one process, so nothing is subtracted across instruments. It was written *before* these changes precisely so that each one would have a control, which is the discipline the earlier documents in this directory lacked.

The five shapes admit different numbers of elements — 1000, 999, 999, 975, 99 — so allocation is not comparable across shapes, only within a pair.

## Result

| shape | direct | graph | |
|--- |---: |---: |---: |
| **static property value** | **372.8 μs / 1,300.96 KB** | 2,871.2 μs / 2,973.58 KB | **7.70× / 2.29×** |
| **static field target** | **432.7 μs / 1,348.34 KB** | 2,846.9 μs / 3,097.55 KB | **6.58× / 2.30×** |
| rank comparison | 362.9 μs / 1,269.99 KB | 2,240.0 μs / 2,753.14 KB | 6.17× / 2.17× |
| short circuit | 4,878.4 μs / 4,766.05 KB | 4,899.2 μs / 4,766.05 KB | control |
| argument independent | 213.5 μs / 895.02 KB | 608.8 μs / 813.40 KB | 2.85× / 0.91× |

Before the property rule, `person => person.Rank > Environment.ProcessorCount` fell back and read **2,670.2 μs / 2,973.58 KB** — a dead heat with its own graph arm to within twenty bytes. It is now 7.70× faster on 2.29× less.

`short circuit` is ineligible under both observers and its two arms allocate **the same figure to the byte**, with `Gen0`, `Gen1` and `Gen2` matching. That arm is what makes the other rows attributable: it is the same measurement taken twice through two objects that differ only in the variable under test.

`rank comparison` was eligible throughout and did not move a byte across three runs.

`argument independent` is the known regression, measured in-process for the first time: **10.0% more memory, about 84 bytes per element**. It also runs **2.85× faster**, which the earlier cross-run figure of "13% on time" understated badly. It is a trade rather than a regression, and whether to spend the time win to recover the memory is now an open question rather than an obvious fix.

## A bug introduced, predicted, and caught

Admitting a static property made it eligible without making it **fixed**, so `FixedSubexpressionRewriter` did not freeze it and the compiled delegate called the getter on every evaluation. The graph does the opposite: a static property node has no dependency, so it is evaluated once at construction and held forever.

`Environment.ProcessorCount` never changes, so the benchmark above was clean while the bug was present. `DateTime.Now` would not have been.

The failure was written as a test before the fix, with the outcome stated in advance — the graph reporting a frozen value and the fast path a current one:

```
Assert.AreEqual failed. Expected:<15>. Actual:<105>.
the two mechanisms disagreed after a static property changed behind them
```

Two lines fixed it, one in each half of the mechanism: `IsFixed` admits `MemberExpression { Member: PropertyInfo, Expression: null }`, and `Resolve` gains the case the frozen values array then needs. Both patterns require a null target, so an instance property cannot be frozen when it should be subscribed; one that somehow arrived would hit the `NotSupportedException` rather than silently freeze.

**The fix cost 32 bytes per element**, measured: 1,269.71 KB became 1,300.96 over a thousand observations. The values array grows from one slot to two, which is 8 bytes, and `property.GetValue(null)` boxes the `int`, which is 24.

The lesson is not new, which is what makes it worth writing down again. The analyzer tests asserted eligibility and nothing else, and eligibility is not behavior. The same gap produced the argument-rooted-field `NotSupportedException` two changes ago, and it was described in `2026-08-31-wider-eligibility.md` under the heading of what a generator's grammar does not know. It was then walked into again, in the same session, by the same author.

## The break

`AddMethodReturnValueDisposal` and both `AddConstructedTypeDisposal` overloads now throw `ArgumentException` for a type that cannot implement a disposal interface. Package validation will not report this: no signature changed.

What it can break is narrow — a call that has never had any effect, since the graph's disposal of such a value was already three failing type tests. The blast radius in this repository was **one call site of sixteen**, and it was `DisposedPropertyValueIsIneligible` registering `TestPerson.Name`, a `string`. That test pinned the disposal rule using an input which could never reach it. It now uses a property whose value the graph really does dispose.

`StaticPropertyIsEligibleWhenStaticDisposalIsExcluded` had the same problem in the other direction: it used `DateTime.Now`, which is now eligible either way, so the test would have passed without testing anything. It uses `Console.Out` — `TextWriter`, unsealed and `IDisposable` — so the option is once again the only thing deciding the outcome.

The guard sits after the `useGenericDefinition` conversion, so a definition returning an open `T` is permitted; `typeof(T).IsSealed` is false, which is the right answer, because it could be anything. `DisposeGenericMethodReturnValueWithOpenReturnType` exists to pin that ordering.

## Noticed, not pursued

The frozen static property is resolved once **per observation**, though its value cannot depend on the argument. A thousand elements freeze the same value a thousand times, and that is the 32 bytes. Sharing it per lambda would recover them, and it is the same shape of problem as an argument-independent expression building one node per element — recorded together rather than patched separately.
