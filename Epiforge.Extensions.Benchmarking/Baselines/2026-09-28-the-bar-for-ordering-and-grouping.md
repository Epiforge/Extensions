# The bar for ordering and grouping

*2026-09-28 — the six comparison classes with arms for NMF Expressions and ObservableComputations beside DynamicData's, first run with 7.0.1*

## What was measured

`DynamicDataComparisonBenchmarks`, `ScaleComparisonBenchmarks`, `OrderedComparisonBenchmarks`, `OrderedScaleComparisonBenchmarks`, `GroupedComparisonBenchmarks` and `GroupedScaleComparisonBenchmarks` gained an arm for each of NMF Expressions 2.3.0 and ObservableComputations 2.3.0, the latest of each on NuGet, beside DynamicData 9.4.33 and this library. Every arm does exactly what its neighbours do to the same collection of `BenchmarkPerson`, and each library is given the same hoisted lambda, in the form its documentation leads with:

- **NMF Expressions**: `((IEnumerable<T>)source).WithUpdates().Where(…)`, `OrderBy(…)` or `GroupBy(…)`, given a dummy successor, because an NMF view with nothing attached to it does not follow its source, and disposed of.
- **ObservableComputations**: `source.Filtering(…)`, `Ordering(…)` or `Grouping(…)`, bound with `For` to an `OcConsumer` whose disposal tears it down.
- Both peers order by an integer key, the form their ordering takes, where this library and DynamicData order by the same key through a comparer.

Every standing view, all four libraries', is verified in setup before it is measured: a filtered view by moving an element out and back in, a sorted view by its order and a grouped view by its groups, because a view which quietly failed to follow its source would otherwise be reported as a very fast one. The run is `BenchmarkRun-20260928-151833`. Each time below is per property change above what the same changes cost with nothing observing them, and each ratio is against this library.

## Filtering

A property change which leaves a filtered view alone:

| elements | This library | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|
| a thousand, `DynamicDataComparisonBenchmarks` | 13.2 ns / 0 B | 13.7 ns / 0 B, 1.04x | 31.1 ns / 129 B, 2.35x | 201.9 ns / 608 B, 15.27x |
| a thousand | 13.9 ns / 0 B | 14.4 ns / 0 B, 1.03x | 32.2 ns / 129 B, 2.31x | 212.1 ns / 608 B, 15.23x |
| ten thousand | 15.5 ns / 0 B | 16.7 ns / 0 B, 1.08x | 45.0 ns / 128 B, 2.91x | 233.5 ns / 608 B, 15.06x |
| a hundred thousand | 66.7 ns / 0 B | 97.3 ns / 0 B, 1.46x | 370.1 ns / 128 B, 5.55x | 367.6 ns / 608 B, 5.51x |

Building and disposing of the view, time and allocation:

| elements | This library | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|
| a thousand, `DynamicDataComparisonBenchmarks` | 318.4 μs / 957.78 KB | 323.0 μs / 1,070.47 KB, 1.01x / 1.12x | 450.1 μs / 2,022.55 KB, 1.41x / 2.11x | 2,537.2 μs / 4,115.82 KB, 7.97x / 4.30x |
| a thousand | 325.7 μs / 957.78 KB | 326.3 μs / 1,071.05 KB, 1.00x / 1.12x | 464.2 μs / 2,022.60 KB, 1.42x / 2.11x | 2,614.8 μs / 4,119.31 KB, 8.03x / 4.30x |
| ten thousand | 10,905.5 μs / 9,465.39 KB | 13,026.5 μs / 10,861.59 KB, 1.19x / 1.15x | 32,327.7 μs / 20,074.09 KB, 2.96x / 2.12x | 45,209.3 μs / 41,076.12 KB, 4.15x / 4.34x |
| a hundred thousand | 166,773.3 μs / 93,426.78 KB | 172,031.8 μs / 106,943.57 KB, 1.03x / 1.14x | 400,691.2 μs / 199,674.60 KB, 2.40x / 2.14x | 831,378.6 μs / 406,495.89 KB, 4.99x / 4.35x |

**This library leads filtering, or ties it, at every size, in time and in allocation, changing and building.** ObservableComputations is closest: within 8% on a change up to ten thousand elements, also allocating nothing, and within 3% on building at a thousand and a hundred thousand, allocating 12% to 15% more. Above that the gap opens to 1.46x on a change. NMF allocates 128 B a change and takes 2.31x to 5.55x the time.

## Sorting

An element moving in a sorted view:

| elements | This library | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|
| a thousand, `OrderedComparisonBenchmarks` | 1,280.6 ns / 292 B | 283.6 ns / 72 B, 0.22x | 372.5 ns / 556 B, 0.29x | 982.4 ns / 414 B, 0.77x |
| a thousand | 1,305.1 ns / 292 B | 280.6 ns / 72 B, 0.21x | 382.1 ns / 556 B, 0.29x | 1,008.7 ns / 414 B, 0.77x |
| four thousand | 2,052.2 ns / 312 B | 1,869.5 ns / 72 B, 0.91x | 290.8 ns / 556 B, 0.14x | 3,689.3 ns / 432 B, 1.80x |
| ten thousand | 3,198.4 ns / 312 B | 5,488.9 ns / 72 B, 1.72x | 372.4 ns / 556 B, 0.12x | 9,345.0 ns / 432 B, 2.92x |

What one walk of the whole view allocates after the changes, `ChangeAThousandRanksThenRead` less `ChangeAThousandRanks`, with the source's own walk last:

| elements | This library | ObservableComputations | NMF Expressions | DynamicData | the source |
|---|---:|---:|---:|---:|---:|
| a thousand | 7.91 KB | 0.04 KB | 39.38 KB | 0.04 KB | 0.03 KB |
| four thousand | 31.35 KB | 0.04 KB | 137.07 KB | 0.04 KB | 0.03 KB |
| ten thousand | 78.22 KB | 0.04 KB | 371.47 KB | 0.04 KB | 0.03 KB |

**Sorting has no single leader, and this library leads at none of the sizes measured.** ObservableComputations moves an element in 0.21x this library's time at a thousand, allocating 72 B, but its cost grows fastest with the view, to 1.72x this library's at ten thousand. NMF's cost does not grow at all, 291 to 382 ns from a thousand to ten thousand, 0.12x this library's at ten thousand, but it allocates 556 B a change and **rebuilds on reading**: one walk of its view allocates 35 to 40 B an element, 371 KB at ten thousand, where this library's walk allocates 8 B an element and the others' nothing. The walk's time is within the change arms' error for every library but NMF's, whose walk at ten thousand measured 246 μs. DynamicData's cost also grows with the view, and this library passes it between a thousand and four thousand.

## Grouping

An element changing group, of sixteen:

| elements | This library | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|
| a thousand, `GroupedComparisonBenchmarks` | 254.4 ns / 592 B | 125.7 ns / 154 B, 0.49x | 178.6 ns / 640 B, 0.70x | 630.6 ns / 1,936 B, 2.48x |
| a thousand | 250.8 ns / 592 B | 126.0 ns / 154 B, 0.50x | 176.5 ns / 640 B, 0.70x | 637.4 ns / 1,936 B, 2.54x |
| four thousand | 402.9 ns / 603 B | 189.6 ns / 198 B, 0.47x | 293.9 ns / 650 B, 0.73x | 674.0 ns / 1,984 B, 1.67x |
| ten thousand | 767.6 ns / 592 B | 306.8 ns / 153 B, 0.40x | 610.1 ns / 640 B, 0.79x | 667.8 ns / 1,936 B, 0.87x |

**ObservableComputations leads grouping at every size**, at 0.40x to 0.50x this library's time and 0.26x to 0.33x its allocation, and NMF at 0.70x to 0.79x its time, allocating 8% more. Both grow with the view as this library does, NMF faster; DynamicData's does not, which is why it passes this library near ten thousand and nobody else.

## Building a sorted or grouped view

| view | This library | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|
| a sorted view of a thousand | 722.1 μs / 1,761.00 KB | 323.0 μs / 1,094.16 KB, 0.45x / 0.62x | 435.7 μs / 1,857.03 KB, 0.60x / 1.05x | 2,646.9 μs / 3,934.87 KB, 3.67x / 2.23x |
| a grouped view of a thousand | 386.6 μs / 1,164.63 KB | 392.2 μs / 1,117.75 KB, 1.01x / 0.96x | 647.5 μs / 2,117.71 KB, 1.67x / 1.82x | 2,557.8 μs / 4,116.62 KB, 6.62x / 3.53x |

**ObservableComputations builds a sorted view in 0.45x this library's time and 0.62x its allocation**, and NMF in 0.60x the time. A grouped view costs this library and ObservableComputations the same to build. NMF's grouped build varied by 58% of its mean and its median was 498.1 μs.

## What this settles and what it does not

- **The bar for sorting and grouping is ObservableComputations, and above a few thousand elements NMF's for sorting, not DynamicData.** This library sorts a thousand elements at 4.5x to 4.7x ObservableComputations' time and groups them at 2x, and the gap in allocation is wider. Nothing here says how; reading `Ordering` and `Grouping` in ObservableComputations and `OrderBy` in NMF comes before any change to this library's. Taken up in `2026-09-29-what-the-others-knew.md`.
- **These classes measure the ordinary path and nothing else.** None of them raises a fault, changes the collection from another thread, raises a notification naming no property, or reads by index, so they say nothing about what any library does then, or what doing it costs.
- **Nothing here measures what a live view of either peer holds.** The footprint figures in the readmes are this library's and DynamicData's only. Taken up in `2026-09-28-the-release-beside-it.md`: ObservableComputations holds the least of the four in every shape.
- The readmes compare this library with DynamicData alone. Since then they compare all four; see `2026-09-28-the-release-beside-it.md`.
