# The copy the observer keeps

*2026-09-28 — `QueryConstructionUnderChange`, `ConcurrentDictionaryNotificationOrder`, `UnobservableExpressions`, `EventualResets`, `ObservedSequenceCopyBenchmarks` and seven benchmark classes, before and after 7.0.1 made building a query safe against a change raised on another thread*

## What was wrong

Observable expressions re-read their sources on every notification, so a change they missed while being built was repaired by the next. Queries apply each change to state of their own, so a change they missed stayed missed.

- **A query built while what it was built over changed missed the change.** A query read its source and then subscribed to it, and a change between the two reached neither. Filtered, projected, ordered, grouped, summed, keyed and extreme-value queries all lost it, and an ordering put the new element in the wrong place because its key never reached the selection. Filtering a dictionary being changed threw `InvalidOperationException` out of `ObserveWhere`, which enumerated the live dictionary across the change.
- **Observing a sequence read the sequence itself.** A query reading the observation read whatever the sequence held at that moment, which a writer on another thread could already have changed beyond what the query had been told. That made the first defect impossible to fix one query at a time.
- **An observed dictionary applied notifications in the order they arrived.** `ObservableConcurrentDictionary<TKey, TValue>` changes without a lock and announces afterwards, so two writers' notifications can arrive in the opposite order to their changes. An add announced after a later add of the same key threw `ArgumentException` into the thread which made it, and a remove announced before the add it followed left the key in the query forever.
- **Eventual synchronized queries read their source when a reset reached their thread**, by which time a later change could be in the source as well as in the queue behind the reset, and was taken twice.
- **A lambda passed as an argument was refused as `Expression`1`**, the class representing it, rather than by its node type.

## The change

- Every query makes and announces its changes under a lock of its own. A query being built holds the locks of what it reads and of everything those read through, in the order the queries were made, while it reads and subscribes. The order is the order changes flow in, so no two threads take them the other way round.
- Observing a sequence which announces its changes keeps a copy, changed only as notifications arrive, and every query reads the copy. That turns the observation into a query like any other and makes the documented condition the only one left: nothing may change the sequence until `CollectionObserver` returns the observation, and a list must announce its changes in the order it makes them.
- Observing a dictionary which announces its changes keeps a copy too, but reads each key a notification names from the dictionary itself and makes the copy agree. The last notification for a key is handled after the last change to it, so the order they arrive in no longer matters.
- A group of `ObserveGroupBy` or `ObserveToLookup` keeps no copy. Only its owner changes it, and always under the owner's lock, so it takes that lock instead of one of its own. Because the owner holds it whenever a group changes, and nothing else can reach a group while its owner is being built, a group and the query over its elements announce their changes without taking it.
- Eventual synchronized queries take their source's contents at the moment a reset is announced.
- The refusal names the node type.

## Tests

| | before | after |
|---|---:|---:|
| Expressions, each of five frameworks | 924 of 939 | **943 of 943** |
| Collections, each of five | 647 of 647 | 647 of 647 |
| Components, each of five | 194 of 194 | 194 of 194 |

The fifteen which failed before are exactly the new rows of `QueryConstructionUnderChange`, `ConcurrentDictionaryNotificationOrder` and `UnobservableExpressions`. Each race holds the build part way through a read on one thread and starts the change on another, waiting for the change to begin before letting the build go, so every failure happened on every run. The four rows of `EventualResets` were written with the change and have no before on this machine.

## What it costs

The before for the comparison classes is the clean 6.1.1 run, `BenchmarkRun-20260927-185141`. 7.0.0 changed no query code. The after is `BenchmarkRun-20260928-104049`, except for the grouped rows, which are `BenchmarkRun-20260928-114057`, taken after the change described under *Grouped changes* below. Each time is per property change above what the same changes cost with nothing observing them.

| arm | before | after | |
|---|---:|---:|---:|
| A property change in a filtered view of a thousand | 13.7 ns / 0 B | 13.6 ns / 0 B | |
| The same at ten thousand | 15.9 ns / 0 B | 15.9 ns / 0 B | |
| The same at a hundred thousand | 65.0 ns / 0 B | 66.2 ns / 0 B | |
| An element changing group, a thousand | 243.5 ns / 592 B | 250.8 ns / 592 B | 1.03x |
| The same, `GroupedScaleComparisonBenchmarks`, four thousand | 389.5 ns | 392.4 ns | 1.01x |
| The same, ten thousand | 744.4 ns | 753.6 ns | 1.01x |
| An element moving in a sorted view, a thousand | 1,288.0 ns / 292 B | 1,286.0 ns / 292 B | 1.00x |
| The same at ten thousand | 3,123.7 ns | 3,094.7 ns | 0.99x |
| Building a filtered view of a thousand | 314.8 μs / 949.95 KB | 308.4 μs / **957.78 KB** | |
| Building a grouped view of a thousand | 374.2 μs / 1,155.88 KB | 376.2 μs / **1,164.63 KB** | 1.01x |
| Building a sorted view of a thousand | 707.3 μs / 1,752.89 KB | 698.6 μs / **1,761 KB** | |

**Filtered, sorted and grouped views change as fast as they did, grouped ones within 3%.** The size above which DynamicData regroups an element faster stays at about 8,300.

**Building allocates the copy, and nothing else of note.** A filtered view of a thousand allocates 7.83 KB more, which is a list of a thousand references. Grouped and sorted views allocate 8.8 KB and 8.1 KB more.

### Grouped changes

A group and the query over its elements each held the owner's lock in every handler, so moving an element between groups held it eight to twelve more times, each time on a thread which already held it, and each time finding the owner and asking whether this thread held its lock. `BenchmarkRun-20260928-104049` measured that:

| arm | 6.1.1 | holding | after |
|---|---:|---:|---:|
| An element changing group, a thousand | 243.5 ns | 256.3 ns | **250.8 ns** |
| The same, `GroupedScaleComparisonBenchmarks`, a thousand | 243.4 ns | 256.5 ns | **249.7 ns** |
| The same, four thousand | 389.5 ns | 418.2 ns | **392.4 ns** |
| The same, ten thousand | 744.4 ns | 857.9 ns | **753.6 ns** |
| Building a grouped view of a thousand | 374.2 μs | 396.0 μs | **376.2 μs** |

Those holds were redundant, and removing them took back all but 1% to 3%. **What removing them took back grew with the collection, 6.8 ns a change at a thousand, 25.8 ns at four thousand and 104.3 ns at ten thousand, although the number of holds a change makes does not, and these runs do not say why.**

### The copy itself

`ObservedSequenceCopyBenchmarks` inserts an element into ten thousand and removes it again, at the front and at the end, with nothing observing the list, with an observation of it alone, and with a filtered view over that observation.

| arm | end | front |
|---|---:|---:|
| unobserved | 15.98 ns / 144 B | 1,464.26 ns / 144 B |
| observed | 45.70 ns / 144 B | 3,821.92 ns / 144 B |
| filtered | 603.94 ns / 1,416 B | 4,627.27 ns / 1,416 B |

The copy allocates nothing per change. Moving it costs the observed arms' difference between front and end less the unobserved arms', **2,328 ns, 1.6x what the list itself spends on the same move**. That is the price of a change far from the end of a long list, and the reason the readme names it.

The first version of this class had no observation-only arms and could not separate the copy from the filtered view, whose own work also grows with the elements after the change.

### Chains of queries

`OperatorConstructionBenchmarks` builds each operator over 256 standing collections, and `Slice` builds the seven queries GS5 builds for a time slice. The before is the overnight 6.1.0 run, `BenchmarkRun-20260927-031028`. At zero elements no expression is observed, so nothing 6.1.1 or 7.0.0 changed in observation reaches those rows. The middle column is this change as first written, `BenchmarkRun-20260928-022733`.

| arm, zero elements | 6.1.0 | first written | after |
|---|---:|---:|---:|
| `Root` | 146 KB | 128 KB | 130 KB |
| `Where`, `Select`, `Cast` | 166 KB | 168 KB | 170 KB |
| `Concat` | 128 KB | 130 KB | 132 KB |
| `SelectMany` | 326 KB | 356 KB | **344 KB** |
| `Slice` | 1,800 KB | 2,048 KB | **1,916 KB** |

As first written, every query built over another allocated that query's whole notification-deferral state in order to take its lock, and each hold through a query which reads through others allocated a list, which put `Slice` up 13.8%. The lock now stands alone and is created only when first needed, and a thread reuses its list, which brought `Slice` to **7,664 B a slice against 7,200, 6.4% more**. `Root` allocates less than before because an observation keeping a copy no longer subscribes to the list's property notifications.

### What a live view holds

`--footprint`, per element retained, against 6.1.1:

| | before | after |
|---|---:|---:|
| A filtered view of a thousand | 919.1 B | **927.1 B** |
| A grouped view of ten thousand | 1,003.4 B | **1,011.5 B** |

Eight bytes an element is the copy's reference. Every five-cycle row still ends at 128 to 192 B, so nothing built is left behind.

## What remains

Nothing of what this change left open. A query's last release racing a change on another thread, the sequences an `ObserveSelectMany` selector returns being read as they stand, and a second operand of `ObserveConcat` implemented outside this library are closed in `2026-09-28-what-the-queries-did-not-own.md`.
