# The index that cost more than it saved

*2026-09-09 — `GroupedScaleComparisonBenchmarks`, 1,000 / 4,000 / 10,000 elements, sixteen groups*

## Why this document exists

`Baselines\` holds eighty-four documents and every one of them records a decision that stood. This is the first to record one that did not. The change described here was built, wired into two query classes, measured, and reverted the same day; without this document the only trace of it would be its absence.

`2026-09-09-the-search-and-the-shift.md` licensed the work. It measured the search at 63% of a migration's linear cost at the group size that matters, projected the grouped growth term from 0.0552 to about 0.0199 ns per collection element, and closed by naming the one thing it had not measured: *"what the maintenance of a per-group index would itself cost, which is subtracted from every figure above."* The work went ahead without measuring it.

## What was built

`GroupingElementPositions<TElement>` — a `PrefixWeightedSequence` holding one node per *occurrence*, mirroring each group's backing collection, plus a `NullableKeyDictionary<TElement, object>` mapping an element to its occurrences: a bare node for the common single-occurrence case, a `List<node>` only for repeats. Removal takes the first node, `IndexOf` returns its position in O(log k), and the caller calls `RemoveAt(index)` instead of `Remove(element)`. Maintained in `AddElement`, `RemoveElement` and the reset branch of both `ObservableCollectionGroupByQuery` and `ObservableCollectionLookupQuery`.

The lookup query carries the identical `collection.Remove(element)` and was changed with the grouping query rather than after it.

## The measurement

Per migration above the floor, every element leaving a group and joining another:

| elements | group size | without the index | with the index | DynamicData |
|---|---:|---:|---:|---:|
| 1,000 | 62 | **236.9 ns** | 456.4 ns | 615.7 ns |
| 4,000 | 250 | **392.5 ns** | 603.2 ns | 633.1 ns |
| 10,000 | 625 | 734.0 ns | 736.1 ns | 636.4 ns |

Fitted: without the index **182 ns fixed + 0.0552 per element**; with it **425 ns fixed + 0.0311 per element**.

Allocation rose from 578 to 640.6 bytes per migration — a `PrefixWeightedSequenceNode` allocated on every insert where nothing extra was allocated before. Grouped retention rose 105.6 bytes per element.

## What went wrong, named

**The growth term did fall, and the fixed cost rose further.** 0.0552 to 0.0311 is a cut of 44%, not the 63% the search's share predicted. Against it, a migration now performs an `IndexOf` climb, a `RemoveAt` of two splits and a merge, an `Insert` of a split and two merges with a node allocation, and three or four dictionary operations — replacing a linear scan which over a group of sixty-two elements costs about 27 ns. The fixed cost went from 182 to 425.

**The error is a decomposition that priced only one side of a substitution.** `GroupRemovalCostBenchmarks` compared `List<T>.Remove` against `List<T>.RemoveAt` and established what the search costs. It never compared `List<T>.Remove` against *treap operations plus dictionary churn plus* `List<T>.RemoveAt`, which is what the substitution actually was. Measuring the cost of what is removed is half a measurement. The instrument that would have prevented this is two candidate implementations benchmarked head to head, in isolation, before either is wired into a query — one run.

**Both cost predictions landed and the benefit prediction did not.** Allocation was predicted at about 640 bytes and measured 640.6; retention was predicted at about 100 bytes per element and measured 105.6. The benefit was the only figure nothing had measured, and it was the only one that was wrong.

## Where the two implementations cross

The fits cross where 243 ns of added fixed cost is repaid by 0.0241 ns per element of removed growth: **about 10,100 elements, which is a group of about 630.**

The measured range ends at a group of 625. **The instrument stopped within 1% of the break-even**, so the region in which the index would win was never measured at all.

Extrapolating both fits past that point — arithmetic, not measurement — a collection of fifty thousand gives about 2,942 ns per migration without the index and about 1,980 with it.

**This is worth stating precisely, because the note made at the time of the revert did not.** That note read the change as one which "only ever helped in a region already lost," on the grounds that DynamicData costs about 713 ns at fifty thousand and beats both. That conflates two different questions. Against DynamicData the change buys nothing anywhere. Against *this library without it*, the change is a loss below about 630 elements per group, level at 630, and a growing win above it. A consumer choosing between libraries and a consumer already committed to this one are asking different questions, and only the first was answered.

## What this does not touch

**Draining a group one element at a time.** Where every removal takes the element at position 0 the search is already O(1) and the entire cost is the shift, which no index removes. Applied to that shape the index adds its fixed cost and removes nothing, making it strictly worse. Draining from the back inverts the halves rather than avoiding them — a full scan and no shift — and by the search and shift figures at a group of 4,096, 845 ns against 244, the back is the more expensive end. **Repeated single-element removal is quadratic in the size of the group whichever end it starts from**; what makes it so is the per-element round trip, and the only lever on it is a bulk removal.

## Reverted

Both query files were restored to their exact byte counts as first staged that day, 7,828 and 12,848, which is the check that the reversal was complete. `GroupingElementPositions.cs` was deleted from the project.

**Standing instruction for whoever revisits this: do not rebuild the index without first benchmarking the two implementations head to head, in isolation, at the group sizes that matter — which now means above 630 per group, since below it the answer is already known and it is no.**

## What has not been measured

Any group size above 625 with the index in place, which is the whole region in which it could pay. The two implementations against each other in isolation. Whether the maintenance cost is dominated by the treap operations or by the dictionary, which decides whether a cheaper occurrence map would move the fixed cost enough to matter. Whether the grouped crossover against DynamicData moves with the number of groups, sixteen having been used throughout.
