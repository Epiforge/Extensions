# Ordered and grouped, against DynamicData

*2026-09-08 — `OrderedComparisonBenchmarks` and `GroupedComparisonBenchmarks`, DynamicData 9.4.33, DefaultJob, MemoryDiagnoser, 1000 elements*

## Why these exist

The filtered comparison measured one operator on a workload where only two elements of a thousand crossed the predicate, and this library's advantage there came from *silence*: a property change which does not alter membership raises nothing and allocates nothing. **Neither of those conditions holds here.** Ordering and grouping are ground DynamicData is built for, and both workloads move every element on every round: ranks are exclusive-ored with 2, so every element trades places in the order and every element leaves one group of sixteen for another.

DynamicData's `Sort` is `[Obsolete]` in this version in favour of `SortAndBind`, which is therefore what is measured; benchmarking an operator its authors have moved away from would not have been a comparison. Grouping uses `Group` over `AutoRefresh` rather than `GroupOnProperty` because the key is a projection rather than a property, and its result is materialized with `AsObservableCache` because `ObserveGroupBy` returns something readable while `Group` returns a stream.

**Both instruments verify before they measure.** Each standing arm runs a probe in setup which moves a key and requires the result to have actually moved — to the end of the order, or into the new group and out of the old. An arrangement which quietly failed to do the work would otherwise be reported as a very fast one. Both probes passed.

## Ordering: the first loss

| arm | mean | allocated |
|---|---|---|
| `ChangeEveryRankUnobserved` | 8.651 μs | 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache` | **997.607 μs** | 451.17 KB |
| `ChangeEveryRankWithExpressions` | 1,348.033 μs | **332.03 KB** |
| `ConstructAndDisposeWithDynamicDataCache` | 2,450.154 μs | 3,935.49 KB |
| `ConstructAndDisposeWithExpressions` | **650.603 μs** | **1,771.94 KB** |

Per key change, above the floor both pay:

| | allocated | time |
|---|---|---|
| this library | **292 B** | 1,339.4 ns |
| DynamicData | 414 B | **989.0 ns** |

**DynamicData propagates an ordered key change 1.35x faster than this library does.** This is the first arm in any comparison run today where it wins, and it wins on the axis this project has spent the day optimizing. This library allocates 0.71x what it does, so the arm splits: leaner here, slower there.

Construction is this library's, comfortably — **3.77x the time and 2.22x the allocation** to stand up the same ordered view.

## Grouping

| arm | mean | allocated |
|---|---|---|
| `ChangeEveryRankUnobserved` | 8.530 μs | 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache` | 605.259 μs | 1,937.5 KB |
| `ChangeEveryRankWithExpressions` | **243.295 μs** | **625 KB** |
| `ConstructAndDisposeWithDynamicDataCache` | 2,283.843 μs | 4,118.35 KB |
| `ConstructAndDisposeWithExpressions` | **503.596 μs** | **1,298.46 KB** |

Per group migration, above the floor:

| | allocated | time |
|---|---|---|
| this library | **592 B** | **234.8 ns** |
| DynamicData | 1,936 B | 596.7 ns |

**2.54x faster and 3.27x leaner per migration**, on a workload where every one of a thousand elements changes group every round. Construction is **4.53x the time and 3.17x the allocation**.

## The finding neither library's marketing would produce

Set the two side by side. Per change, this library costs **1,339.4 ns to reorder and 234.8 ns to regroup — 5.7x more for ordering than for grouping.** DynamicData costs 989.0 ns and 596.7 ns, a ratio of 1.66x.

Ordering is harder than grouping for both, but it is *disproportionately* harder here. **The ordered query is this library's weakest operator relative to the alternative, and it is the only place the alternative is faster.** Both figures have the shape of an O(n) reposition within a list — about 1.3 ns per element shifted at a thousand elements — so neither library is doing anything exotic; this library's constant is simply worse.

That is a lead worth following and it was not followed today. Whether the per-change cost is genuinely O(n) is settled by measuring the same arm at 4,000 and 10,000 elements, which has not been done.

## The predictions, scored

Recorded before the run: *ordering is the arm most likely to go against us; the lead collapses to roughly 1.5-4x on allocation and 2-5x on time, with a real chance DynamicData wins outright on allocation. Ours should land near 360 KB above the floor. Grouping 2-5x on allocation. Construction ours by 2-3x.*

- **The direction was right and the axis was wrong.** DynamicData did win an arm, but on *time*, not allocation. The reasoning that predicted a possible loss — that ordering removes the silence this library's filtered advantage rests on — was correct; the guess about which column would show it was not.
- **Ordering allocation: predicted a 1.5-4x lead, measured 1.42x.** Below the range, so the prediction was too optimistic even where it was right.
- **Our own ordered figure: predicted ~360 KB above the floor, measured 285 KB.** Close, and derived from this morning's 360 B per key change.
- **Grouping allocation: predicted 2-5x, measured 3.27x.** In range.
- **Construction: predicted 2-3x, measured 2.22x and 3.17x on allocation but 3.77x and 4.53x on time.** The allocation guess held; the time guess was too pessimistic in both.

## What has not been measured

Ordering and grouping at 4,000, 10,000 and 100,000 elements, which would settle whether the ordered per-change cost is O(n) and whether the loss widens or closes. Whether a different DynamicData idiom — `SortAndBindOptions` with a tuned reset threshold, or a throttled `AutoRefresh` — changes either result. Multi-key ordering, which this library supports and which was not exercised. `GroupOnProperty` against a grouped query keyed directly on a property rather than on a projection.
