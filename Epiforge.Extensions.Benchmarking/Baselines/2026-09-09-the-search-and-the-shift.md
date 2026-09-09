# The search and the shift

*2026-09-09 — `GroupRemovalCostBenchmarks`, group sizes 64 / 256 / 1,024 / 4,096*

## The question

A grouped query moves an element between groups by calling `Remove` on the collection backing its old group. That searches for the element and then shifts everything after it down, which is why the cost of a migration grows with the size of the group and why this library loses grouping to DynamicData above about eight thousand elements.

**Only the search can be removed.** A structure that yields an element's position without looking for it would still have to shift the array consumers read. So whether the fix is worth building depends entirely on which half is the expensive one — and that had been estimated at "maybe 60%" and never measured.

| group size | search | shift | search share |
|---|---|---|---|
| 64 | 7.71 ns | 10.92 ns | 41% |
| 256 | 31.77 ns | 23.38 ns | 58% |
| 1,024 | 153.64 ns | 76.97 ns | 67% |
| 4,096 | 845.21 ns | 244.12 ns | **78%** |

`search` is `SearchThenShift` less `ShiftOnly`; `shift` is `ShiftOnly` less the floor of removing and re-adding the last element, which is a flat 17.6–18.5 ns at every size.

## The two halves move in opposite directions

Per element scanned, the search gets **more** expensive as the group grows — 0.120, 0.124, 0.150, 0.206 ns — because it is a virtual equality call per element over an array which stops fitting in cache. Per element moved, the shift gets **cheaper** — 0.171, 0.091, 0.075, 0.060 ns — because `Array.Copy` vectorizes and amortizes its setup.

**So the search's share rises with size**, which is the wrong direction: the larger the collection, the more of the cost is the part that could have been avoided.

## What removing it would buy

At the group size that matters — about 494, which is a collection of 7,900 divided sixteen ways — the search is **~63%** of the linear cost. Applying that to the measured grouped growth term of 0.0552 ns per collection element leaves about **0.0199**.

| | today | with the search removed |
|---|---|---|
| grouping 10,000 elements | 734 ns, a **1.18x loss** | ~381 ns, a **~1.6x win** |
| crossover against DynamicData | ~7,900 elements | ~24,000 elements |

Those two projections are arithmetic on measured figures, not measurements.

## The prediction, scored — the first one about this code to land

Estimated beforehand at "maybe 60% of the growth term, and I have been wrong reasoning about this code twice." Measured **63%** at the relevant size. **After two consecutive misses on the ordered query, this one was right.** The difference in method is worth noting: the ordered estimates reasoned about which of nineteen operations dominated a chain, while this one reasoned about two operations whose costs were separable by subtraction. **An estimate is worth making when the instrument can decompose it, and worth distrusting when it cannot.**

## The suggestion that prompted it, evaluated

It arrived from outside as: *the migration path already has the element; if it also has the index, `RemoveAt` skips the scan.*

**Right that the scan is the thing to kill.** Wrong that the index is available: `RemoveElement` holds the element and the key, and what is needed is the element's position *within its group's collection*. Maintaining that in an array-backed collection costs more than the scan it saves, because every removal shifts the position of everything after it — a dictionary write per shifted element to avoid a comparison per scanned element.

**The workable form** is a structure that *produces* the index sub-linearly. `ObservableCollectionOrderByQuery` already does exactly this: `nodesByElement` maps element to node and `positions.IndexOf(node)` returns the index in O(log k). The grouped query can keep the same pair per group and call `RemoveAt(index)`. **No new collection type is needed** — what consumers observe stays an `ObservableRangeCollection`.

**It is not a copy of the ordered query, though.** OrderBy uses one weighted node per distinct sort position; a group holds duplicates as separate positions in append order, so the map needs one node per occurrence and a removal must take the first. That is the part to test first if this is ever built.

## What has not been measured

Whether the search's share holds for element types with a cheaper equality — `BenchmarkPerson` has no `IEquatable<T>`, so `EqualityComparer<T>.Default` dispatches virtually, and a sealed type implementing `IEquatable<T>` would scan faster and shift the balance. Whether the shift's per-element cost keeps falling past 4,096. What the maintenance of a per-group index would itself cost, which is subtracted from every figure above.
