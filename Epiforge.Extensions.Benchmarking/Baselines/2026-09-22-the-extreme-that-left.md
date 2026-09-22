# The extreme that left

*2026-09-22 — `ComparisonEvictionBenchmarks` and `QuerySelectionBenchmarks`, before and after the comparison query began keeping its distinct values in the comparer's order*

## The question

GS5 measured `ObserveMax` with a culture comparer at 47.6 μs a pair of changes at 4,096 elements when the change evicted the maximum, against 7.9 μs for the ordering and first element it had been using, and reverted. The query rescanned every element whenever the value it held left. The release notes had quoted a 3.07x advantage for the comparer form; that figure measured construction alone and was relayed without saying so.

## Reproduced first

`ComparisonEvictionBenchmarks`, a pair of changes that restores what it found, each mechanism and size over a collection of its own:

| per pair | before, 256 | before, 4,096 |
|---|---:|---:|
| maximum, extreme evicted | 3,262 ns / 992 B | 46,454 ns / 992 B |
| ordering, extreme evicted | 1,414 ns / 816 B | 3,181 ns / 816 B |
| maximum, extreme left alone | 249.8 ns / 832 B | 278.5 ns / 832 B |
| ordering, extreme left alone | 1,094 ns / 720 B | 1,745 ns / 720 B |
| floor | | 17.58 ns / 96 B |

Every prediction held. Break-even was 3.3% of changes evicting at 4,096 and 31% at 256.

## Priced in isolation before it was built

`SortedCountPricingBenchmarks`, the candidate structure with nothing around it: two rescans cost 80,132 ns at 4,096, 9.8 ns a culture comparison; a remove-and-add pair on the structure cost 1,111 ns and 160 bytes; building it over 4,096 values cost 1,701 μs and 327,816 bytes. **The build was predicted at 540 μs and came in 3.1x slower**, 415 ns an insert; the per-change and memory predictions held.

## The measurement

| per pair | after, 256 | after, 4,096 |
|---|---:|---:|
| maximum, extreme evicted | **959 ns / 560 B** | **1,403 ns / 560 B** |
| ordering, extreme evicted — control | 1,401 ns / 816 B | 3,159 ns / 816 B |
| maximum, extreme left alone | **1,060 ns / 464 B** | **1,457 ns / 464 B** |
| ordering, extreme left alone — control | 1,098 ns / 720 B | 1,763 ns / 720 B |
| floor — control | | 17.56 ns / 96 B |

| construction, a thousand people | before | after |
|---|---:|---:|
| maximum of names, default comparer | 322.75 μs / 887,344 B | **755.35 μs / 975,472 B** |
| maximum of names, ordinal comparer | 314.67 μs / 887,344 B | **449.32 μs / 975,472 B** |
| element with the greatest name, ordinally | 448.63 μs / 945,676 B | **612.21 μs / 1,041,821 B** |
| first of a descending ordering — control | 966.53 μs / 1,619,472 B | 954.90 μs / 1,619,472 B |
| count ahead of one element — control | 488.67 μs / 931,028 B | 502.02 μs / 931,028 B |

**An eviction is 33.1x cheaper at 4,096 and 2.25x cheaper than the ordering. A change which leaves the maximum alone is 5.2x dearer than it was and 1.21x cheaper than the ordering.** At 256 the maximum and the ordering are level on a change which leaves the maximum alone. So in no measured shape is the comparer form now worse than the ordering, which is what GS5 asked for first. Construction rose 1.43x with an ordinal comparer and 2.34x with the default, and remains 1.26x to 2.13x cheaper than the ordering and 1.66x lighter. The query holds 88 bytes for each distinct value.

## Wrong in direction

The allocation of a pair was predicted to rise by about 160 bytes, for the two entries a pair creates. **It fell**: 992 to 560 bytes when evicting and 832 to 464 when leaving the maximum alone. The old change handler walked the change with `Cast`, `Any` and a closure; the new one walks it by index. That those iterators outweigh the two entries is the likely account and is not separately measured.

## Also found

The old change handler took no lock and deferred no notifications while it updated what the query reported. The new one does both.

Values the comparer cannot tell apart — `"a"` and `"A"` under a case-insensitive comparer, or two elements with one key for `ObserveMaxBy` — share one entry. The entry reports one of them and keeps the rest, and promotes one when the reported value leaves. A naive entry of one value and a count would have reported a value the collection no longer holds; `CollectionComparisonRandomizedOperations` pins the promotion across 20 seeds of 200 changes and in a named test.

## Predictions scored

- **Held.** Eviction about 1.4 μs at 4,096 and 1 μs at 256: 1,403 and 959.
- **Held.** Leaving the maximum alone about 1.4 μs and 1 μs: 1,457 and 1,060.
- **Held.** The ordering arms, the floor and both construction controls unmoved.
- **Loose.** Construction of the default maximum about 670 μs: 755.35, 13% over. Memory about 80 KB more: 88,128 bytes.
- **Wrong in direction.** Allocation per pair, above.

## What has not been measured

Construction at 4,096 inside the query, where the isolated build suggests about 1.7 ms. Comparers cheaper than a culture comparison over value types, where a rescan is cheap and the structure's fixed cost matters more.
