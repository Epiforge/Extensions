# Where the ordered loss lives

*2026-09-08 — `OrderedScaleComparisonBenchmarks`, DynamicData 9.4.33, DefaultJob, MemoryDiagnoser, a thousand changes at 1,000 / 4,000 / 10,000 elements*

## The question

`OrderedComparisonBenchmarks` found this library propagating an ordered key change in 1,339.4 ns against DynamicData's 989.0 ns at a thousand elements — the only arm it loses in any comparison run — with both figures shaped like an O(n) reposition inside a list. A constant-factor problem and a complexity problem are worth very different amounts of attention before a release, so this instrument separates them.

**The change count is held fixed at a thousand and the element count varies.** Changing every element would make the total O(n squared) and the curve would climb whether the per-change cost did or not, which would have answered the question wrongly. A fixed thousand changes reads the answer off the shape: flat is O(1) per change, linear is O(n). The changed elements are spread by stride and each rank is exclusive-ored with 2, so the distance moved is identical at every size.

## The loss reverses, and it is not a scaling problem

Per key change, above the floor both libraries pay:

| elements | this library | DynamicData | |
|---|---|---|---|
| 1,000 | 1,331.5 ns | **1,002.6 ns** | theirs, 1.33x |
| 4,000 | **2,135.0 ns** | 3,648.9 ns | ours, 1.71x |
| 10,000 | **3,198.8 ns** | 9,162.1 ns | ours, 2.86x |

DynamicData's cost is linear in the collection size. This library's is strongly sublinear. Fitting each to a fixed cost plus a per-element one:

| | fixed, per change | per element | |
|---|---|---|---|
| this library | 1,124 ns | **0.2075 ns** | |
| DynamicData | **96 ns** | 0.9066 ns | |

**The two curves cross at about 1,470 elements.** Below that DynamicData is faster; above it this library is, by a margin which keeps widening.

**So the loss is a fixed overhead of roughly 1,124 ns on every key change, and nothing else.** This library's per-element reposition is 4.4x cheaper than the alternative's; its fixed cost is 12x larger. That single number is the whole of the deficit, and it is the thing to attack if the ordered query is ever optimized — it would win every size rather than most of them.

**These figures are a second run.** The first produced 1,326.5 / 2,087.8 / 3,195.3 against 986.1 / 3,624.4 / 9,156.5, agreeing within 2% throughout.

## Why DynamicData's is linear, read rather than assumed

`SortAndBind`'s `ChangeReason.Refresh` case calls `target.IndexOf(item)` and `GetInsertPositionLinear` **unconditionally**. Its own comment explains why: binary search cannot be used there because the list is not sorted at that moment. `SortAndBindOptions.UseBinarySearch` defaults to `false` and **could not help this path even when set**.

The same comment suggests that consumers using binary search should exclude refresh events with `.WhereReasonsAreNot(ChangeReason.Refresh)` — which would stop the view reordering on key changes at all, and would fail this instrument's correctness probe. **There is therefore no faster-and-still-correct DynamicData idiom for this workload that was passed over.** This is its design, not a tuning oversight and not an unfair harness.

## The result was too good, so it was checked

A sublinear per-change cost is exactly what a view which *defers* its work would report: mark dirty on each change, sort when somebody looks. The correctness probe proves the order is right when read, not that the work happened before the read. Three arms therefore change the same thousand ranks **and then walk the whole result**.

| elements | this library, the walk costs | DynamicData, the walk costs | a bare walk of the source costs |
|---|---|---|---|
| 1,000 | 10.8 μs | −7.8 μs (noise) | 1.3 μs |
| 4,000 | 30.3 μs | 11.7 μs | 4.0 μs |
| 10,000 | 35.8 μs | 19.0 μs | 11.3 μs |

**Nothing is deferred.** Reading after a thousand changes adds about 1% to either library's total at every size. A deferred sort of ten thousand elements would have cost milliseconds, not 36 microseconds. **The per-change figures are what they claim to be.**

## A small finding the guard turned up on its own

Walking this library's ordered query allocates **8 bytes per element**, consistently — 7.91 KB over a thousand, 31.35 over four thousand, 78.22 over ten thousand. Walking DynamicData's bound collection allocates 40 bytes in total, at any size.

Enumeration is not what any of these instruments set out to measure and this is not a large number, but it is a per-element allocation on a read path and it was not known before. Its cause has not been looked into.

## The predictions, scored

Recorded before the first run: *both climb roughly linearly; ours from 1,339.4 ns to 5,000-6,000 at four thousand and 12,000-14,000 at ten thousand; theirs from 989.0 to 3,700-4,400 and 9,000-11,000; the ratio holds near 1.35x.*

- **DynamicData: very nearly exact.** Predicted 9,000-11,000 ns at ten thousand, measured 9,162.1. Predicted 3,700-4,400 at four thousand, measured 3,648.9 — just under.
- **This library: wrong by 4x.** Predicted 12,000-14,000, measured 3,198.8. The prediction assumed our reposition worked like theirs, immediately after reading theirs, without reading ours. **Having just studied one implementation is a reason to check the other, not a licence to assume it.**
- **The ratio did not hold near 1.35x; it inverted.** The prediction never entertained that the loss might be confined to small collections, which is the single most decision-relevant fact the instrument produced.

## What has not been measured

What the 1,124 ns fixed cost is made of, which is now the most valuable open question about the ordered query. Whether the crossover moves under a workload where elements travel further than two positions. The same scaling shape for the grouped query, which won at a thousand and was not tested above it. The cause of the 8 bytes per element on enumeration.
