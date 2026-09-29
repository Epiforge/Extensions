# The change nobody was listening for

*2026-09-29 — the ordering, grouping, lookup and dictionary observing each element's keys themselves, the fault census carried through every chain, the whole comparison set run overnight with 7.0.1 as a second job, the footprint report with release rows for every keyed view, a soak of the keyed views which found two ways a change on another thread could be lost, and the soak and the keyed classes again on the fix*

## How this was measured

Every 7.0.1 row ran on the same machine, in the same run and under the same job as the row beside it, through `[AgainstReleasedExpressions("7.0.1")]`. Each time is per property change, above what the same changes cost with nothing observing them, and each ratio is against 7.0.1 unless it says otherwise.

- `BenchmarkRun-20260929-035504` ran the comparison set overnight: `ChainedComparison`, `DictionaryConversion` (new), `DictionaryMutation`, `DictionaryPropagation`, `DynamicDataComparison`, `GroupedComparison`, `GroupedScaleComparison`, `MembershipChangeComparison`, `ObserveDictionaryWhere`, `ObserveGroupBy`, `ObserveOrderBy`, `OperatorConstruction`, `OrderByKeyChange` in four classes, `OrderByMoveDistance`, `OrderedComparison`, `OrderedRandomMoveComparison`, `OrderedScaleComparison`, `PairingProjectionScale`, `QueryLambdaReuse`, `ScaleComparison` and `SynchronizedQuery`.
- Reading it found one cost of this round's own making, removed below, and `QueryLambdaReuse` was run again on the fix at 11:25.
- The footprint report was taken at 10:59 and the keyed view soak (new) finished at 11:23, both on the code of the overnight run.
- On the fix for what the soak found, the soak ran again, finishing at 12:38, and `BenchmarkRun-20260929-124821` ran `DictionaryConversion`, `GroupedComparison`, `MembershipChangeComparison` and `OrderedComparison`, 49 arms.

Tests on that code, and again on the fix at 13:02: Expressions 1,049 of 1,049, Collections 651 of 651, Comparisons 48 of 48, Components 194 of 194, on each of five frameworks.

## What changed

- **An ordering, a grouping, a lookup and a dictionary built with `ObserveToDictionary` observe each element's keys themselves**, one observation per distinct element however often it occurs, and hear the source's changes as a dependent rather than through a projection of pairs beneath them. `KeyedElement<TElement, TKey>` is gone; a grouping and a lookup share `ObservedElementKeys<TElement, TKey>`.
- **An ordering keeps its entries in a `PrefixWeightedSequence<T>` of distinct elements**, each weighted by its occurrences, and structurally equal stable key selectors share one observation, as its projection's did.
- **Faults reach everything built over the query where they arise**: filtering, projecting, flattening, ordering, grouping, a lookup, a dictionary, casting, filtering by type, distinct, skip, take, a slice, individual changes, the maximum of a projection, and a dictionary's filtering, filtering by type, conversion to a collection and concurrent form, all took their source's fault only when their contents changed or not at all. Every scalar which reads a query takes a fault which appears or clears without its contents changing.
- **A key selector which throws leaves its element out** of a grouping, a lookup, an ordering or a dictionary and reports the fault, where it threw a `NullReferenceException` out of the change; a fault on a null element clears once the element leaves; an ordering by more than one key orders two elements lacking a key at one level by the keys after it.

## What the overnight run found

- **Building a sorted view whose key selector cannot be shared cost 9.8x.** `QueryLambdaReuse.OrderByRebuilt`, which builds an ordering over a fresh closure every time, took 17,977.4 μs and 3,128.67 KB against 7.0.1's 1,831.6 μs and 2,178.57 KB. The rewritten ordering observed its key selectors as given, so a selector which the optimizer rebuilds for every query compiled afresh for each; the projection it replaced had shared every structurally equal stable selector. It shares them again, and the rerun reads 1,524.8 μs and 1,421.07 KB against 7.0.1's 1,925.2 μs and 2,180.49 KB, 0.79x and 0.65x. A fresh filter still costs its compilation, 14,763.8 μs against 125.1 shared, as it does in 7.0.1.

## Ordering

| an element moving in a sorted view | 7.0.1 | now | |
|---|---:|---:|---:|
| a thousand, `OrderedComparison` | 1,293.2 ns / 292 B | 338.3 ns / 87 B | 0.26x |
| a thousand, `OrderedScaleComparison` | 1,278.4 ns / 292 B | 341.5 ns / 87 B | 0.27x |
| four thousand | 2,027.4 ns / 312 B | 391.8 ns / 96 B | 0.19x |
| ten thousand | 3,258.9 ns / 312 B | 450.4 ns / 96 B | 0.14x |
| two places, `OrderByMoveDistance` | 1,298.9 ns | 337.4 ns | 0.26x |
| thirty-two places | 1,358.9 ns | 636.7 ns | 0.47x |
| 512 places | 1,313.8 ns | 700.3 ns | 0.53x |
| to a random place among a thousand, `OrderedRandomMoveComparison` | 1,867.9 ns | 1,055.9 ns | 0.57x |
| to a random place among ten thousand | 4,294.3 ns | 2,411.9 ns | 0.56x |

| the same, other libraries | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|
| a thousand | 277.8 ns | 340.5 ns | 999.2 ns |
| four thousand | 1,847.8 ns | 279.2 ns | 3,658.9 ns |
| ten thousand | 5,498.4 ns | 363.8 ns | 9,231.5 ns |
| to a random place among a thousand | 644.5 ns | 575.2 ns | 1,089.2 ns |
| to a random place among ten thousand | 10,148.3 ns | 1,060.7 ns | 10,751.8 ns |

**This library now sorts a thousand level with NMF Expressions**, at 1.40x its time at four thousand and 1.24x at ten, and reading the view after each change costs 0.79x what NMF Expressions' reading costs at ten thousand, 489.5 against 617.5 ns. A key change allocates 87 B, the least of the four after ObservableComputations' 72 B.

## Grouping

| an element changing group, of sixteen | 7.0.1 | now | |
|---|---:|---:|---:|
| a thousand, `GroupedComparison` | 246.3 ns / 592 B | 130.4 ns / 168 B | 0.53x |
| a lookup, a thousand | 370.1 ns / 896 B | 162.7 ns / 168 B | 0.44x |
| a thousand, `GroupedScaleComparison` | 251.9 ns / 592 B | 128.9 ns / 168 B | 0.51x |
| four thousand | 386.5 ns / 603 B | 185.6 ns / 179 B | 0.48x |
| ten thousand | 755.6 ns / 592 B | 204.6 ns / 168 B | 0.27x |
| 65,536 | 7,259.5 ns / 592 B | 233.1 ns / 168 B | 0.032x |

| the same, other libraries | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|
| a thousand | 126.5 ns / 154 B | 170.9 ns / 640 B | 627.2 ns / 1,936 B |
| four thousand | 193.0 ns / 198 B | 287.8 ns / 650 B | 642.9 ns / 1,984 B |
| ten thousand | 308.3 ns / 153 B | 607.5 ns / 640 B | 648.7 ns / 1,936 B |
| 65,536 | 1,297.0 ns / 153 B | 11,073.5 ns / 640 B | 755.7 ns / 1,936 B |

**Grouping is level with ObservableComputations at a thousand and ahead of every library above that**, and DynamicData no longer passes it at any size measured: 233.1 against 755.7 ns at 65,536. The 65,536 figure favors the search from both ends, since the benchmark moves elements between groups back and forth; an element which joined its group at neither end is found by a scan from each end.

## A dictionary, and views built over a filter

| `DictionaryConversion`, a thousand | 7.0.1 | now | |
|---|---:|---:|---:|
| a value changing | 961.6 ns / 1,608 B | 152.0 ns / 560 B | 0.16x |
| building and disposing | 588.5 μs / 1,618.06 KB | 425.7 μs / 1,192.92 KB | 0.72x / 0.74x |

| `ChainedComparison`, a change of rank | this library | DynamicData | |
|---|---:|---:|---:|
| filtered then grouped, a thousand admitted | 141.6 ns / 168 B | 677.5 ns / 2,184 B | 0.21x |
| filtered then grouped, five thousand admitted | 187.0 ns / 168 B | 689.2 ns / 2,184 B | 0.27x |
| filtered then ordered, a thousand admitted | 350.9 ns / 87 B | 1,065.0 ns / 662 B | 0.33x |
| filtered then ordered, five thousand admitted | 429.6 ns / 96 B | 4,733.8 ns / 680 B | 0.09x |

## Building a view

| building and disposing, a thousand elements | 7.0.1 | now | |
|---|---:|---:|---:|
| filtered | 312.3 μs / 957.78 KB | 310.1 μs / 856.25 KB | 0.99x / 0.89x |
| sorted | 715.3 μs / 1,761.00 KB | 357.7 μs / 1,030.54 KB | 0.50x / 0.59x |
| grouped | 381.9 μs / 1,164.63 KB | 265.3 μs / 941.76 KB | 0.69x / 0.81x |

ObservableComputations builds a sorted view in 338.7 μs and a grouped one in 366.5; NMF Expressions in 412.3 and 518.8; DynamicData's cache in 2,337.0 and 2,392.3. `QueryLambdaReuse`'s shared arms, which build a view over a selector another query already compiled: a grouping 274.9 μs against 415.2, a lookup 754.1 against 1,389.0, a dictionary 203.7 against 351.9, and an ordering 248.4 against 524.1 in the rerun.

## What a view holds

| per element, a thousand | 7.0.1 | yesterday | now | ObservableComputations | NMF Expressions | DynamicData |
|---|---:|---:|---:|---:|---:|---:|
| sorted | 1,270.2 B | 1,166.2 B | 936.8 B | **897.6 B** | 1,192.9 B | 1,921.9 B |
| grouped | 1,041.6 B | 936.9 B | **831.2 B** | 859.5 B | 1,297.7 B | 2,001.7 B |
| a dictionary | | | 1,086.3 B | | | |

**A grouped view here now holds the least of the four.** Five build-and-drop cycles leave 160 B after a grouped view, a sorted view and a dictionary alike, as after a filtered one: nothing is lost on release.

## The soak

`--soak 200000` built a grouping, a lookup, an ordering by two keys, a dictionary and a rank over 32 elements drawn from 64, six rounds at a time on 24 processors, while one thread made 3,000 changes to the collection and three made 3,000 changes each to ranks and names, and then checked each view against LINQ. It ran 1,408 s: no exception, no round leaking a cached query or expression, and two mismatches, one grouping and one ordering. The first, round 21,300, held `-4b` ahead of `-4c` in an ordering by name descending: one element's name was stale.

Two ways to lose a change account for it, both found by reading and both confirmed in the sandbox by widening the window each one needs and watching mismatches appear:

- **Reading before listening.** Every per-element observation in the queries, the grouping's, lookup's, ordering's and dictionary's new ones and the filter's and projection's old ones, a dictionary's filtering, projection and conversion to a collection among them, read the observation's evaluation and subscribed to its changes afterwards. A change made on another thread between the two was announced to no one. With a spin between read and subscription, 300 rounds gave one grouping mismatch.
- **Announcing out of order.** An observation announcing a change read the node's evaluation, then wrote it as the one announced. Two threads changing the same element at once could each read, and the one reading earlier could write last, leaving the observation announcing the older evaluation until the next change. A pending notification seen on another thread was also taken on trust without a fence, so a change could be declined by one thread while the other read the node without it. With a spin between read and write, 150 rounds gave two grouping mismatches and one ordering.

Every query now subscribes before it reads, reading only once the element is recorded, and a dictionary's filtering, projection and conversion to a collection are built under their own lock as every other query is. An observation announcing in a thread-safe observer writes with a full fence, reads the node again afterwards and announces again if it has moved, so the last to write holds what the node holds; a notification seen pending is looked at again behind a fence. An observer confined to one thread is unchanged. With both windows widened on the fixed code, 400 rounds gave no mismatch.

On the fix, `--soak 200000` ran 1,364 s on 24 processors with no mismatch, no exception and no round leaking.

## What the fix cost

| a thousand elements, 7.0.1 in the same run | 7.0.1 | the fix | before the fix, overnight |
|---|---:|---:|---:|
| an element changing group | 242.7 ns | 129.1 ns, 0.53x | 130.4 ns |
| an element changing group in a lookup | 361.8 ns | 163.6 ns, 0.45x | 162.7 ns |
| an element moving in a sorted view | 1,302.9 ns | 336.5 ns, 0.26x | 338.3 ns |
| a value changing in a dictionary | 930.6 ns | 153.7 ns, 0.17x | 152.0 ns |
| a membership flip, unhandled | 77.2 ns | 79.1 ns, 1.02x | 84.6 ns against 78.2 |
| a membership flip, handled | 110.6 ns | 113.3 ns, 1.02x | 109.3 ns against 111.1 |
| building a sorted view | 700.9 μs | 346.5 μs, 0.49x | 357.7 μs |
| building a grouped view | 379.2 μs | 266.3 μs, 0.70x | 265.3 μs |
| building a dictionary | 570.2 μs | 410.5 μs, 0.72x | 425.7 μs |

At ten thousand a flip reads 1,029.3 against 1,002.7 ns unhandled, 1.03x, and 1,416.8 against 1,388.2 handled, 1.02x. Allocation is unchanged in every arm. **The fenced announcement costs nothing this run can resolve**: every keyed figure is within 1% of the overnight run and every ratio against 7.0.1 is the same, and the overnight 1.08x for an unhandled flip reads 1.02x here, so it was noise.

## What did not move

- **A property change which leaves a filtered view alone**, `DynamicDataComparison`: 13.5 ns in 7.0.1, 13.7 now.
- **Membership flips**, `MembershipChangeComparison`: 1.02x at a thousand and 1.02x to 1.03x at ten thousand on the fix, above; the overnight run's 1.08x unhandled at a thousand did not recur.
- **A dictionary's propagation**, `DictionaryPropagation` against 7.0.0: an `All` over a dictionary 1.12x, the rest within 5%.
- **Building over no element or one**, `OperatorConstruction`: within 3% apart from a slice of one, 0.94x.

## The readmes and the release note

- The readmes' sorted and grouped rows, building rows, the rows of what a view holds and the paragraphs on sorting, grouping, allocation and choosing a library are from the overnight run and the footprint report.
- The release note's lines for ordering, grouping and group removal carry their times from the overnight run; a line records the dictionary's conversion, seven the correctness of faults, null keys and the ordering's cache, and two the changes the soak found.

## What remains

- **Sharing stable selectors structurally for the grouping, the lookup, the dictionary and the filter**, as the ordering now does, which `WhereRebuilt` prices at 14,763.8 μs against 125.1.
- **A single ordering key held inline**, estimated at about 32 B an element.
