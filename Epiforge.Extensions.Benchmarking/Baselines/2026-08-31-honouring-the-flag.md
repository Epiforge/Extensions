# 31 August 2026 — what the flag was actually for

`ObservableRangeCollection<T>` takes a constructor flag, `RaiseCollectionChangedEventsForIndividualElements`. Three methods never consulted it: `RemoveAll`, `GetAndRemoveAll`, and the two `RemoveRange` overloads which take items.

I read the flag as a performance switch — false meaning "batch for speed", true meaning "stay compatible" — and concluded the three methods were denying flag-`false` consumers a cheaper path. That reading was wrong, and it produced the wrong fix.

The flag exists so a collection can be bound to a consumer which cannot process a `NotifyCollectionChangedEventArgs` carrying more than one item at all. **`true` is a prohibition; `false` is a permission.** The obligation to consult it falls on any method which could emit an event carrying a range; a method which cannot emit one — `Reset`, `ResetRemovingAll` — is exempt by construction.

That inverts the finding. The three methods only ever emitted single-item events, so they were already safe under the prohibition: **no collection which set the flag was affected by the defect or is affected by the fix.** They were wrong only in never taking the permission they were granted.

It also settles the shape of the fix, which had been open. If `false` is permission to be descriptive rather than an instruction to be terse, the right event is not a single `Reset` — that discards the indices the permission exists to carry, and would make `ResetRemovingAll` largely redundant. The right event is **one `Remove` per contiguous run of removed elements**: never more events than before, never an event the consumer did not permit, indices preserved, no threshold and no magic number.

## What coalescing is worth

Both arms remove the same elements at the same positions, so the array shifting is identical and only the event count differs — one against four thousand in the clustered case, four thousand against four thousand in the scattered case.

| elements | matches | per element | per run | ratio | allocated | per run | alloc ratio |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1,000 | clustered | 50.63 μs | 29.73 μs | 0.59 | 23.44 KB | 1.07 KB | **0.05** |
| 1,000 | scattered | 49.20 μs | 43.70 μs | 0.89 | 23.44 KB | 23.44 KB | 1.00 |
| 16,000 | clustered | 43,755.81 μs | 43,648.47 μs | 1.00 | 375 KB | 15.72 KB | **0.04** |
| 16,000 | scattered | 24,616.33 μs | 24,624.49 μs | 1.00 | 375 KB | 375 KB | 1.00 |

Twenty to twenty-five times less memory when the matches are adjacent, exact parity when they are not, and no regression anywhere.

The time result is the honest half: **coalescing does not fix the cost of `RemoveAll` and was never going to.** Both modes still call `RemoveAt` per element, so the O(n·k) shifting stands, and at sixteen thousand elements it is the entire measurement. `ResetRemovingAll` remains the answer for cost; both doc comments say so.

Note also that the clustered case is *slower in absolute terms* than the scattered one at sixteen thousand — 43.8 ms against 24.6 ms — because the block sits at the front and every removal shifts the whole tail. Removing a contiguous block one element at a time is the worst case for shifting, not the best.

## The regression the first run found

The first version allocated a `T[1]` for every run, including runs of one. Scattered removal therefore allocated 7% more than the code it replaced, at both sizes — a real regression, small, and caught only because the benchmark had a scattered arm. The core now takes a single-item path when a run is one long, which restores exact parity.

The clustered ratio improved from 0.12 to 0.05 in the same pass, because `RemoveAll` stopped materialising the items it was only counting. Both arms benefited: the baseline itself fell from 26.64 KB to 23.44 KB at a thousand elements and from 422.95 KB to 375 KB at sixteen thousand, which is about twelve bytes per element removed.

## `RemoveRange` visits the collection once

The items-taking overloads searched the collection for each item supplied — `IndexOf` per item, a linear scan each time. Removing a thousand items from a ten-thousand-element collection was ten million comparisons. This was recorded in `2026-08-31-range-collection-notifications.md` as wanting its own change, and the flag work turned out to be that change.

Counting the supplied items into a multiset and sweeping the collection once through the run-coalescing core is equivalent, because all occurrences of a value are interchangeable under the equality `IndexOf` was already using, and it visits each element once instead of once per item supplied. The multiset is a `NullableKeyDictionary`, this library's own type, which is what keeps `null` a legal element.

The visible consequence is that a flag-`false` collection now reports removals in the order the items occur in the collection rather than the order they were supplied. The items removed and the resulting contents are unchanged. Flag-`true` collections keep the old traversal exactly, so the guarantee that they see no change survives.

## Properties are announced before the collection

Every batched path raised `Count` and `Item[]` *after* the `CollectionChanged` event. The base class raises them before, so the two modes disagreed about ordering, and a consumer reading `Count` inside a collection-changed handler saw a different sequence depending on a constructor argument.

Seven call sites reordered. The test does not assert anything about the base class: it asserts that **the last thing announced is the collection change**, which holds in both modes and is exactly what was violated. Ten operations are covered.

## Two bugs found on the way

**An infinite loop, mine, shipped for one build.** Folding `RemoveAll` and `GetAndRemoveAll` into one core with an optional output list produced `removed?.Add(GetAndRemoveAt(i))`. When `removed` is null — which is what `RemoveAll` passes — the null-conditional short-circuits the whole expression and `GetAndRemoveAt` never runs, so nothing is removed and the loop index never advances. Every `RemoveAll` on a flag-`true` collection hung.

The test suite caught it, but not the tests written for this change: those all called `GetAndRemoveAll`, which passes a non-null list and takes the working path. Nothing exercised the method with the bug. `RemoveAllCountsWithoutCollectingInEitherMode` now does, with a five-second timeout so a regression fails rather than wedging the run.

**A single item which is itself a list, pre-existing.** `new NotifyCollectionChangedEventArgs(action, item, index)` where `T` implements `IList` binds to the `changedItems` overload rather than the `changedItem` one. An `ObservableRangeCollection<int[]>` removing one array raised an event describing two integers. `RemoveRange` had this already; the new core would have inherited it. Both cast to `object?` now, and a test fails loudly on the old behaviour.
