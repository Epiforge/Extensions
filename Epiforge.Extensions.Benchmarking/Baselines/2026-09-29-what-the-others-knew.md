# What the others knew

*2026-09-29 — the ordering, grouping, building and synchronized-query classes run with 7.0.1 itself as a second job, after changes drawn from reading DynamicData 9.4.33, NMF Expressions 2.3.0 and ObservableComputations 2.3.0, and the footprint report taken again*

## How this was measured

Every 7.0.1 row below ran on the same machine, in the same run and under the same job as the row beside it, through `[AgainstReleasedExpressions("7.0.1")]`, which now also pins Collections and Components to the packages 7.0.1 shipped with. The released job runs only this library's arms and their floors: another library's arm is the same code in both jobs, and an arm which reads after changing asks nothing of a release comparison. Each time is per property change, above what the same changes cost with nothing observing them.

- `BenchmarkRun-20260928-231737` ran ten classes, 200 arms: `DynamicDataComparison`, `GroupedComparison`, `GroupedScaleComparison`, `MembershipChangeComparison`, `OperatorConstruction`, `OrderByMoveDistance`, `OrderedComparison`, `OrderedRandomMoveComparison` (new), `OrderedScaleComparison` and `SynchronizedQuery`.
- Reading it found two costs of this round's own making, both removed below. `BenchmarkRun-20260929-003145` ran the three building arms, `GroupedScaleComparison`'s own arms and `PairingProjectionScale` (new), by filter, 29 arms.
- That run left the second in place and located it; with the grouping fix and a fourth size, `BenchmarkRun-20260929-011934` ran `GroupedScaleComparison` whole and `GroupedComparison`'s change arms, 50 arms.
- The footprint report was taken at 00:40 on the code of the second run; the grouping fix changes nothing it measures.

Tests on the final code: Expressions 986 of 986, Collections 651 of 651, Comparisons 48 of 48, Components 194 of 194, on each of five frameworks.

## What changed

- **An ordered query moves an element's node in place.** `PrefixWeightedSequence<T>.Move` takes the node out where it stands and reinserts it where it belongs, where a move of a range of one split and rejoined the tree three times; the destination is found outward from the old position, by steps of one, two, four and eight, before searching by halves; and `ObservableRangeCollection<T>.MoveRange` of one element shifts only what lies between the two places.
- **A grouping or a lookup pairs an element with its key in a `KeyedElement<TElement, TKey>`**, which compares both with their own types' equality, where `Tuple<T1, T2>` compared them as objects and boxed a key which is a value type six times a change; its handler walks the change's items by index instead of through `Cast`.
- **An element occurring once in a filtered, projected, flattened or dictionary view holds its position in a field of the dictionary entry**, not in a list of one, and a direct observation no longer keeps the lock it needs only while it is built.
- **Synchronized queries keep their pending changes in a `ConcurrentQueue<T>`** rather than a list under a lock; a query synchronized with a lock applies a change at once when it can take the lock without waiting and the change was not made within a handler of another query's announcement, and one synchronized with a context applies a change made on the context at once when nothing is pending.
- **A group removes the instance which left it**, found by reference, where it removed the first element equal to it.

## What the first run found

- **Building a sorted or grouped view of a thousand cost 1.91x and 1.92x.** The position's field had been two, so a projection's dictionary entry grew from 40 to 48 B, and at a thousand elements the dictionary's entries grew to 92,720 B, past the large-object threshold: every build allocated there and drew a gen 2 collection every 25 builds or so (40.4 and 29.0 a thousand operations, where 7.0.1 has none). The field is now one, holding the lone node or a list once there is a second, and every dictionary holding one is back to 7.0.1's entry size.
- **An element changing group among ten thousand cost 1.17x.** `PairingProjectionScale`, the projection beneath a grouping on its own, was not slower there, 173.1 against 161.2 ns, so the cost was above it and grew with the group. That is the group finding the element leaving it: `Collection<T>.Remove` compared it with `Equals` against every element before it, reading each. Why this round made that slower is not established; the search no longer reads the elements at all.
- **Finding it by `Equals` was also wrong.** Of two distinct elements which compare equal under one key, when one changed group the group kept it and removed the other. `GroupRemovalOfEqualInstances` failed on 7.0.1's behaviour on every framework, both rows, and passes now.

## Ordering

| an element moving in a sorted view | 7.0.1 | now | |
|---|---:|---:|---:|
| a thousand, `OrderedComparison` | 1,287.7 ns / 292 B | 562.1 ns / 215 B | 0.44x |
| a thousand, `OrderedScaleComparison` | 1,272.5 ns / 292 B | 569.3 ns / 215 B | 0.45x |
| four thousand | 2,065.5 ns / 312 B | 627.3 ns / 224 B | 0.30x |
| ten thousand | 3,099.5 ns / 312 B | 783.1 ns / 224 B | 0.25x |
| two places, `OrderByMoveDistance` | 1,312.1 ns | 569.2 ns | 0.43x |
| thirty-two places | 1,377.6 ns | 982.4 ns | 0.71x |
| 512 places | 1,323.0 ns | 1,068.1 ns | 0.81x |
| to a random place among a thousand, `OrderedRandomMoveComparison` | 1,860.2 ns | 1,524.4 ns | 0.82x |
| to a random place among ten thousand | 4,597.1 ns | 3,363.7 ns | 0.73x |

**A move now costs what its distance costs.** 7.0.1 split and rejoined the whole tree whatever the distance, so its cost was flat across distances; now a short move is cheapest and a long one approaches the search by halves. Against the other libraries at a thousand, ObservableComputations moves an element in 281.6 ns, NMF Expressions in 351.9 and DynamicData in 997.3; at ten thousand 5,412.5, 362.3 and 9,302.9. To a random place, ObservableComputations takes 653.9 ns among a thousand and 10,224.3 among ten thousand, NMF Expressions 580.1 and 1,097.5, DynamicData 1,103.7 and 10,923.0.

## Grouping

| an element changing group, of sixteen | 7.0.1 | now | |
|---|---:|---:|---:|
| a thousand, `GroupedComparison` | 255.1 ns / 592 B | 195.2 ns / 272 B | 0.77x |
| a lookup, a thousand | 379.0 ns / 896 B | 317.2 ns / 576 B | 0.84x |
| a thousand, `GroupedScaleComparison` | 255.8 ns / 592 B | 195.8 ns / 272 B | 0.77x |
| four thousand | 408.7 ns / 603 B | 270.9 ns / 283 B | 0.66x |
| ten thousand | 794.0 ns / 592 B | 371.0 ns / 272 B | 0.47x |
| 65,536 | 7,365.0 ns / 592 B | 1,381.3 ns / 272 B | 0.19x |

| the same, other libraries | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|
| a thousand | 126.8 ns / 154 B | 177.8 ns / 640 B | 648.0 ns / 1,936 B |
| four thousand | 195.2 ns / 198 B | 292.2 ns / 650 B | 669.1 ns / 1,984 B |
| ten thousand | 305.7 ns / 153 B | 618.5 ns / 640 B | 682.1 ns / 1,936 B |
| 65,536 | 1,312.7 ns / 153 B | 10,867.8 ns / 640 B | 775.8 ns / 1,936 B |

**This library's grouping now grows the most slowly of the three whose cost grows**, 7.1x from a thousand elements to 65,536 against ObservableComputations' 10.4x and NMF Expressions' 61x. DynamicData's is flat and passes this library at about 29,000 elements, by interpolating between ten thousand and 65,536.

## Building a view

| building and disposing, a thousand elements | 7.0.1 | now | |
|---|---:|---:|---:|
| filtered | 327.0 μs / 957.78 KB | 306.3 μs / 856.22 KB | 0.94x |
| filtered, from a fresh predicate | 392.0 μs / 966.07 KB | 411.9 μs / 864.50 KB | 1.05x |
| sorted | 715.1 μs / 1,761.00 KB | 698.1 μs / 1,659.44 KB | 0.98x |
| grouped | 403.1 μs / 1,164.63 KB | 372.8 μs / 1,063.07 KB | 0.92x |

Allocation falls about 104 B an element in all three; the times are within the run's resolution, and the fresh predicate's error was 48.70 μs of its 411.9. From the first run, ObservableComputations builds a filtered view in 314.1 μs, a sorted one in 313.4 and a grouped one in 366.4; NMF Expressions in 456.4, 431.1 and 496.3; DynamicData's cache in 2,513.2, 2,492.8 and 2,511.5.

## What a view holds

| per element, a thousand | 7.0.1 | now | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|---:|
| filtered | 927.7 B | **823.1 B** | 885.7 B | 1,281.1 B | 1,865.0 B |
| sorted | 1,270.2 B | 1,166.2 B | **897.6 B** | 1,192.9 B | 1,921.9 B |
| grouped | 1,041.6 B | 936.9 B | **859.5 B** | 1,297.7 B | 2,001.7 B |

**A filtered view here now holds the least of the four**, at ten thousand elements as at a thousand: 8,065,480 B against ObservableComputations' 8,958,368. Five build-and-drop cycles still leave 160 B.

## Synchronized queries

| an addition and a removal at the end of a thousand, `SynchronizedQuery` | 7.0.1 | now | |
|---|---:|---:|---:|
| with a lock | 213.88 ns | 142.15 ns | 0.66x |
| with a callback | 221.68 ns | 172.98 ns | 0.78x |
| with a context | 194.83 ns | 146.99 ns | 0.75x |
| with a context, from its own thread | 192.30 ns | 116.90 ns | 0.61x |
| with a context, eventually | 128.64 ns | 130.61 ns | 1.02x |
| observed, not synchronized | 48.70 ns | 48.19 ns | 0.99x |

Allocation is unchanged in every row. 7.0.0 measured 93.9, 115.2, 101.7 and 91.6 ns for the first four in `2026-09-28-the-release-beside-it.md`; what remains above it is the observation's copy and the hold on a query's own changes that 7.0.1 needs.

## What did not move

- **A property change which leaves a filtered view alone**, `DynamicDataComparison`: 13.8 ns in 7.0.1, 14.0 now.
- **Membership flips**, `MembershipChangeComparison`: 80.5 against 78.4 ns at a thousand, 102.9 against 100.5 at ten thousand, unhandled; allocation unchanged.
- **Building over no element or one**, `OperatorConstruction`: times within 3%, apart from a slice of one, 1,770.57 against 1,647.88 μs; a projection, a cast or a filter of one allocates 20 KB less and a flattening 30 KB less, from the first run.

## The readmes and the release note

- The readmes' sorted, grouped and building rows, the three rows of what a view holds, and the paragraphs on sorting, grouping, allocation and memory are from these runs; a row for 65,536 elements joins the grouping rows. The rows for a property change which leaves a filtered view alone, at a thousand and a hundred thousand, the membership rows, the propagation paragraph and `ToObservableChangeSet()` stay those of `BenchmarkRun-20260928-151833`, since nothing here changes what they measure.
- The release note's lines for ordering, grouping and synchronized queries now carry their times from these runs, and a line records the group's removal of the instance which left it.

## What remains

- **Why ObservableComputations groups faster**, 0.65x this library's time at a thousand elements and 0.95x at 65,536, and sorts a thousand in 0.50x.
- **Keys kept with the ordering's nodes, and the ordering and grouping observing their key selector per element** instead of through a projection of pairs, which would take a query layer's machinery out of every change. Designed from the peers' source and priced by measured units, not built.
- **What each lock held while building costs**, carried from `2026-09-28-the-release-beside-it.md`.
