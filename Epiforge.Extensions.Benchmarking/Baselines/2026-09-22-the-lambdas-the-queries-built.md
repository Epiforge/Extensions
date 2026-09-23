# The lambdas the queries built

*2026-09-22 — `QueryLambdaReuseBenchmarks`, before and after the queries began reusing the lambdas they build for themselves*

## The question

GS5 profiled a traversal which calls `ObserveCast` once for each of 4,096 time slices and found 12.8% of its time compiling the cast's lambda. `ObserveCast` wrote `element => (TResult)(object)element!` in its body, so every call built a new expression tree, and the observer's caches of optimized and compiled lambdas, and the optimizer memo in front of them, all match by reference. Every call paid for an optimizer pass, an analysis and a compilation, and left a dynamic method for the finalizer.

## How far it reached

A sweep of `Observable\Query` found the same shape at more sites than the two reported, in two kinds.

**Built on every call from nothing but types:** collection `ObserveCast`, `ObserveOfType`, `ObserveDistinct`, the parameterless `ObserveAverage` and `ObserveSum`, and `ObserveToDictionary` given only a key selector; dictionary `ObserveCast`, `ObserveOfType` and the parameterless `ObserveAverage`, `ObserveMax`, `ObserveMin`, `ObserveSum` and `ObserveToCollection`. Each now reads a `static readonly` field of a nested holder, so each closed type builds its lambda once.

**Derived from a lambda the caller passed:** the key pairing behind `ObserveMaxBy` and `ObserveMinBy`, built on every call; the key-value wrappers behind dictionary `ObserveWhere`, `ObserveSelect` and `ObserveToCollection`, built on every call, which every dictionary operator taking a predicate or selector reaches; and the wrappers behind `ObserveGroupBy`, `ObserveToLookup`, `ObserveToDictionary` and collection `ObserveAll`, built once for each query and so once for each source. Each is now held in a `ConditionalWeakTable` keyed by reference on the lambda it derives from, so it lives as long as the caller keeps that lambda and no longer.

`LambdaReuse` enumerates the public operators by behavior rather than by this list: an observer is given an optimizer which records every tree it has not been handed before, each operator is observed over three sources with the same lambda, and the second and third sources must hand it nothing new. Before the change it failed exactly the 40 operators above and passed the 11 which only pass a caller's lambda through.

## Two defects found beside it

**`ObserveOrderBy` leaked.** It already cached its wrapped selectors, in a static `ConcurrentDictionary` compared by `ExpressionEqualityComparer` and never emptied. A selector which captures anything carries its closure as a constant, a closure has reference equality, so every closure instance was a new entry kept for the life of the process together with what it captured and, through the compile cache, its compiled form. `CapturedLambdaRetention.CollectionOrderBy` failed before the change and passes after; the other seven operators it covers passed both times.

**Dictionary `ObserveAll` and `ObserveAny` cached under one key and removed under another.** Both looked up and stored the query under the caller's predicate and constructed it with the optimized one, which `QueryDisposed` then removed. Where the optimizer rewrote the predicate, the removal missed, the disposed query stayed cached, and the next identical observation was handed it back and never updated. `OptimizedPredicateCaching` failed all four dictionary tests before the change and passed the four collection controls throughout.

## The measurement

Each arm builds one query over each of 256 standing one-element sources and then disposes all of them, under `ExpressionOptimizer.tryVisit` as GS5 configures. The after column is the final run, of the code as released; the run before it, of the first cut, was within 5% of it on every other arm and differed materially only in `OrderByRebuilt`, below.

| arm | before | after | |
|---|---:|---:|---:|
| `WhereShared` — control | 246.3 μs / 1.38 MB | 239.1 μs / 1.38 MB | |
| `WhereRebuilt` — control | 10,572.9 μs / 3.55 MB | 10,471.4 μs / 3.55 MB | |
| `SelectShared` — control | 235.2 μs / 1.43 MB | 230.0 μs / 1.43 MB | |
| `OrderByShared` — control | 1,288.7 μs / 3.63 MB | 1,227.9 μs / 3.63 MB | |
| `Cast` | 8,433.2 μs / 3.38 MB | **230.5 μs / 1.43 MB** | **36.6x** |
| `OfType` | 17,056.5 μs / 6.68 MB | **660.2 μs / 2.81 MB** | **25.8x** |
| `AllShared` | 12,374.5 μs / 3.79 MB | **344.5 μs / 1.66 MB** | **35.9x** |
| `MaxByShared` | 17,208.3 μs / 4.89 MB | **577.1 μs / 2.33 MB** | **29.8x** |
| `GroupByShared` | 21,826.0 μs / 7.56 MB | **1,665.2 μs / 4.93 MB** | **13.1x** |
| `ToDictionaryShared` | 23,624.7 μs / 6.44 MB | **678.7 μs / 2.6 MB** | **34.8x** |
| `ToLookupShared` | 63,763.5 μs / 16.61 MB | **5,041.4 μs / 9.18 MB** | **12.6x** |
| `DictionaryWhereShared` | 21,046.2 μs / 4.25 MB | **223.6 μs / 1.13 MB** | **94.1x** |
| `DictionaryCast` | 21,267.9 μs / 4.37 MB | **222.9 μs / 1.14 MB** | **95.4x** |
| `OrderByRebuilt` | 2,869.3 μs / 4.04 MB | 2,977.3 μs / 4.04 MB | 1.04x, within noise |

The four controls stayed within 4.7% on time and identical on allocation. **`Cast` now costs what `ObserveSelect` given a held selector costs**, 230.5 μs against 230.0 and 1.43 MB against 1.43, which is the ceiling it was built to reach.

The unit is the control pair: `WhereRebuilt` less `WhereShared` is **40.3 μs and about 8.7 KB for each source** in the before run and 40.0 μs in the final one, one optimizer pass, analysis and compilation of a small predicate.

## A regression introduced and withdrawn

The first cut replaced `ObserveOrderBy`'s structural dictionary outright with the weak table keyed by instance. That ended the leak, and it also ended the sharing: `OrderByRebuilt`, which writes its key selector inline so that a new tree reaches the query on every call, went from 2,869.3 μs to **27,386.6 μs and 6.75 MB**, a compilation on every call at 95.8 μs and about 10.8 KB each. It was written up as a regression to announce, on the grounds that a structural cache restricted to trees whose constants all compare by value would still grow without bound for a program building trees with differing constants.

That reasoning was wrong. The growth it named was already there before the change, and it was never the defect: **the defect was retaining closures**, and a tree whose constants all compare by value holds no closure. `ExpressionKeyStability.IsStable` draws exactly that line and `ObservableScalarTransformQuery` already uses it for the same purpose. Removing the sharing traded a real cost for no gain in what the change was for.

The released code keeps both: a selector which passes `IsStable`, which is every selector capturing nothing, is shared by structure as before; a selector holding a closure goes to the weak table and is kept only as long as its caller keeps it. `OrderByRebuilt` is back at **2,977.3 μs and 4.04 MB**, 3.8% from its before, and `CapturedLambdaRetention.CollectionOrderBy`, whose selector captures a variable, still passes.

## Predictions scored

- **Held.** Every one of the 45 tests predicted to fail before the change failed, on all five frameworks, and nothing else.
- **Held.** `Cast` after the change equal to `SelectShared`, to the report's resolution in both columns.
- **Held.** Every control unmoved, in both after runs.
- **Wrong by about 2x.** Each wrapper was predicted to cost one unit, the price of the control predicate. They cost about two: the difference each arm lost is 78.7 μs a source for `GroupByShared`, 89.6 for `ToDictionaryShared`, 65.0 for `MaxByShared` and 81.3 for `DictionaryWhereShared`, and `OrderByRebuilt` gained 95.8. A wrapper constructing a tuple or a key-value pair around the caller's body costs more to analyze and compile than `person => person.Rank >= 0`. `ToLookupShared`, predicted at two units, lost 229.3 μs a source, about three wrappers' worth; the lookup builds its grouping wrapper, a dictionary over its groupings and that dictionary's key-value wrapper.
- **Wrong by 2x, and then withdrawn.** `OrderByRebuilt` was predicted to rise to about 13 ms and rose to 27.4 ms. The regression was then removed rather than announced; after the removal it was predicted back at about 2.9 ms and measured 2,977.3 μs, with every other arm and all 780 tests unmoved.

## A mistake of this change, caught by its own test

The first cut keyed `ToDictionary`'s two-selector cache by the key selector first. `ObserveToDictionary(keySelector)` passes the library's static identity lambda as the value selector, so the inner table's entry had a key which never dies. When the caller's key selector died the inner table became unreachable, but a `ConditionalWeakTable` releases its entries only after it has been finalized twice, and `CapturedLambdaRetention.CollectionToDictionary`, which collects, waits for finalizers and collects again, found the captured object alive on all five frameworks. The table is now keyed by the value selector first. **A two-level table releases late whenever its inner key outlives its outer one**: that still applies to dictionary `ObserveSelect`, and to `ObserveToDictionary` given both selectors where the caller keeps the key selector and drops the value selector. Late, not never.

## Also seen

`CollectionUsingSynchronizationCallbackEventually.SourceManipulationAsync` failed once, on net6.0 only, in a run between the two above: after `ReplaceRange` it waited for a `Count` of 4 and then read five elements. No code this change touched runs in that test, and it passed on every framework in the other three runs. Its wait condition reads `Count` outside the callback's lock while the query applies events on another thread; why a single `ReplaceRange` would briefly leave four elements before five has not been established.
