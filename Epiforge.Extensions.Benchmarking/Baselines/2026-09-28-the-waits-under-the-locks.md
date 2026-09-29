# The waits under the locks

*2026-09-28 — `SynchronizedQueryUnderChange`, `AsyncDisposalOnAContext`, `FlatteningRangeChanges` and ten benchmark classes, before and after 7.0.1 stopped its synchronized queries and a blocking disposal waiting on a thread which was waiting on them*

## What was wrong

- **`ObserveUsingSynchronizationContext` deadlocked when the context's own thread changed what the query was built over while a change made on another thread waited to be sent.** The other thread held the change locks of every query the change passed through while `Send` waited for the context, and the context's thread, changing the source, waited for the first of those locks. It happened directly over an observed list, through a filter, and over an `ObservableConcurrentDictionary<TKey, TValue>`.
- **Building a query over one synchronized with a context, on any other thread, deadlocked against the context applying a change.** The builder held the synchronized query's lock and read it with `Send`, while the context, applying, waited for that lock. Collection and dictionary, eventual or not, all four.
- **`ObserveUsingSyncRoot` deadlocked when a thread holding the sync root changed the source** while another thread's change, holding the observation's lock, waited for the sync root. The library took the two in one order and the writer in the other.
- **`ObserveUsingSynchronizationCallback` with a callback which sends to a context** deadlocked in the same way as the first.
- **An observer set to `BlockOnAsyncDisposal` deadlocked disposing of a value on a context's only thread** when the value's disposal resumed on the context it began on, as an awaiting `DisposeAsync` does by default: `DisposeAsync().AsTask().Wait()` blocked the only thread the disposal could finish on. It did so replacing a value on the graph and disposing of a held value with the observation on the fast path. This predates 7.0.

The failure of `CollectionUsingSynchronizationCallbackEventually.SourceManipulationAsync` seen once on 22 September was the test's: `ReplaceRange` removes and inserts one element at a time, so the query passes through four elements on its way from five to five, and the test read its count outside the callback's lock, saw four, and then enumerated before the removal was applied.

## The change

- **Each thread counts the change locks it holds.** The synchronized queries which are not eventual queue each change of their source, with the source's contents when a reset is announced, under a lock of the queue's own, and apply everything queued, in order, once the thread which raised it holds no change lock: a context query by `Send`, or at once when already on the context, a sync-root query inside `lock (SyncRoot)`, and a callback query inside the callback. A change made within a handler of another query's announcements reaches them once that announcement is over.
- **The four context-synchronized queries read without waiting on the context** when the reader holds that query's change lock or is on the context, since nothing kept there can change while either is so.
- **A blocking disposal begins on a thread-pool thread**, as it already did when not blocking, and the observer waits for it.
- **The test re-checks its condition under the callback's read lock** and waits again when the two disagree, on Daniel's go.
- Both readmes say that a handler of any query's announcements runs while that query and those it is built over hold their locks, so it must not wait on a thread which may change them.

## Tests

| run | Expressions, each of five frameworks | failing |
|---|---:|---|
| first befores | 969 of 972 | the three rows of `SynchronizedQueryUnderChange` |
| first after | 972 of 972 | |
| second befores | 972 of 978 | the six rows added to `SynchronizedQueryUnderChange` |
| second after | 978 of 978 | |
| third befores | 978 of 980 | both rows of `AsyncDisposalOnAContext` |
| after | **980 of 980** | |

Collections held at 647 of 647 and Components at 194 of 194 throughout. Every new row failed on every framework in its befores, each by timing out a deterministic gate rather than by chance: the change on the context's thread, the sync root's holder or the disposal is held until the other thread is known to be waiting, and a pair which deadlocks is left undisposed rather than torn down. `FlatteningRangeChanges`, 18 rows changing several elements at once in a sequence a selector returned, held by one team or shared by two, passed when written and is kept as a guard for the copies `2026-09-28-what-the-queries-did-not-own.md` introduced.

## What it costs

The after is `BenchmarkRun-20260928-151833`, which ran the ten classes below. For the comparison classes the before is the clean 6.1.1 run, `BenchmarkRun-20260927-185141`; 7.0.0 changed no query code, so that is the release's cost against 7.0.0. The earlier 7.0.1 runs, `BenchmarkRun-20260928-104049`, `-114057` and `-131153`, separate this round from the ones before it. **The unobserved control arms of this run ran 0.96x to 1.09x their counterparts in the 6.1.1 run, the largest at ten thousand elements**, and DynamicData's arms, which nothing here reaches, moved as much as ours did.

### Changes to a view

Each time is per property change above what the same changes cost with nothing observing them.

| arm | 7.0.0 | 7.0.1 | | DynamicData, same runs |
|---|---:|---:|---:|---:|
| A property change in a filtered view of a thousand | 13.7 ns / 0 B | **13.2 ns** / 0 B | 0.97x | 206.2 → 201.9 ns, 0.98x |
| The same, `ScaleComparisonBenchmarks`, a thousand | 13.9 ns / 0 B | **13.9 ns** / 0 B | 1.00x | 200.8 → 212.1 ns, 1.06x |
| The same, ten thousand | 15.9 ns / 0 B | **15.5 ns** / 0 B | 0.98x | 224.5 → 233.5 ns, 1.04x |
| The same, a hundred thousand | 65.0 ns / 0 B | **66.7 ns** / 0 B | 1.03x | 331.0 → 367.6 ns, 1.11x |
| An element changing group, a thousand | 243.5 ns / 592 B | **254.4 ns** / 592 B | 1.04x | 633.5 → 630.6 ns, 1.00x |
| The same, `GroupedScaleComparisonBenchmarks`, a thousand | 243.4 ns / 592 B | **250.8 ns** / 592 B | 1.03x | 624.7 → 637.4 ns, 1.02x |
| The same, four thousand | 389.5 ns / 603 B | **402.9 ns** / 603 B | 1.03x | 634.9 → 674.0 ns, 1.06x |
| The same, ten thousand | 744.4 ns / 592 B | **767.6 ns** / 592 B | 1.03x | 649.1 → 667.8 ns, 1.03x |
| An element moving in a sorted view, a thousand | 1,288.0 ns / 292 B | **1,280.6 ns** / 292 B | 0.99x | 1,004.6 → 982.4 ns, 0.98x |
| The same, `OrderedScaleComparisonBenchmarks`, a thousand | 1,300.9 ns / 292 B | **1,305.1 ns** / 292 B | 1.00x | 998.8 → 1,008.7 ns, 1.01x |
| The same, four thousand | 2,071.0 ns / 312 B | **2,052.2 ns** / 312 B | 0.99x | 3,698.2 → 3,689.3 ns, 1.00x |
| The same, ten thousand | 3,123.7 ns / 312 B | **3,198.4 ns** / 312 B | 1.02x | 9,286.3 → 9,345.0 ns, 1.01x |

**A filtered or sorted view changes in 0.97x to 1.03x of what it did in 7.0.0, and a grouped view in 1.03x to 1.04x**, while DynamicData's grouping moved 1.00x to 1.06x between the same two runs. The release note says a grouped view costs up to 5% more, rounding up rather than claiming the difference away. Allocation per change is identical on every row.

Against the round before this one, `104049` for filtered and sorted views and `114057` for grouped ones, every row moved 0.97x to 1.03x and DynamicData 0.99x to 1.09x:

| arm | before | after | DynamicData |
|---|---|---:|---:|
| A property change in a filtered view of a thousand | `104049` 13.6 ns | **13.2 ns**, 0.97x | 195.3 → 201.9 ns, 1.03x |
| The same, `ScaleComparisonBenchmarks`, a thousand | `104049` 13.7 ns | **13.9 ns**, 1.01x | 197.6 → 212.1 ns, 1.07x |
| The same, ten thousand | `104049` 15.9 ns | **15.5 ns**, 0.98x | 218.9 → 233.5 ns, 1.07x |
| The same, a hundred thousand | `104049` 66.2 ns | **66.7 ns**, 1.01x | 338.7 → 367.6 ns, 1.09x |
| An element changing group, a thousand | `114057` 250.8 ns | **254.4 ns**, 1.01x | 624.5 → 630.6 ns, 1.01x |
| The same, `GroupedScaleComparisonBenchmarks`, a thousand | `114057` 249.7 ns | **250.8 ns**, 1.00x | 627.6 → 637.4 ns, 1.02x |
| The same, four thousand | `114057` 392.4 ns | **402.9 ns**, 1.03x | 648.5 → 674.0 ns, 1.04x |
| The same, ten thousand | `114057` 753.6 ns | **767.6 ns**, 1.02x | 650.5 → 667.8 ns, 1.03x |
| An element moving in a sorted view, a thousand | `104049` 1,286.0 ns | **1,280.6 ns**, 1.00x | 994.6 → 982.4 ns, 0.99x |
| The same, `OrderedScaleComparisonBenchmarks`, a thousand | `104049` 1,278.2 ns | **1,305.1 ns**, 1.02x | 998.3 → 1,008.7 ns, 1.01x |
| The same, four thousand | `104049` 2,045.7 ns | **2,052.2 ns**, 1.00x | 3,559.9 → 3,689.3 ns, 1.04x |
| The same, ten thousand | `104049` 3,094.7 ns | **3,198.4 ns**, 1.03x | 9,208.2 → 9,345.0 ns, 1.01x |

**The per-thread count this round added to every change lock held shows no cost these runs can resolve.** Grouped and sorted views hold their locks on every change these arms make; a property change which leaves a filtered view alone does not reach the view, because the predicate's observation stays silent, so those rows take no lock at all.

### Building a view

| arm | 7.0.0 | 7.0.1 | | DynamicData |
|---|---:|---:|---:|---:|
| A filtered view of a thousand | 314.8 μs / 949.95 KB | **318.4 μs** / 957.78 KB | 1.01x | 1.05x |
| The same, `ScaleComparisonBenchmarks` | 315.7 μs / 949.95 KB | **325.7 μs** / 957.78 KB | 1.03x | 1.02x |
| Ten thousand | 11,354.1 μs / 9,387.23 KB | **10,905.5 μs** / 9,465.39 KB | 0.96x | 1.03x |
| A hundred thousand | 164,312.7 μs / 92,645.52 KB | **166,773.3 μs** / 93,426.78 KB | 1.01x | 1.01x |
| A grouped view of a thousand | 374.2 μs / 1,155.88 KB | **386.6 μs** / 1,164.63 KB | 1.03x | 1.00x |
| A sorted view of a thousand | 707.3 μs / 1,752.89 KB | **722.1 μs** / 1,761.00 KB | 1.02x | 1.04x |

**Building a view takes 0.96x to 1.03x the time it did in 7.0.0**, DynamicData's 1.00x to 1.05x, and allocates the copy, 8 B an element.

### The copy

`ObservedSequenceCopyBenchmarks` against `104049`:

| arm | before | after | |
|---|---:|---:|---:|
| `InsertAndRemoveAtEndFiltered` | 603.94 ns / 1416 B | 637.02 ns / 1416 B | 1.05x |
| `InsertAndRemoveAtEndObserved` | 45.70 ns / 144 B | 49.53 ns / 144 B | 1.08x |
| `InsertAndRemoveAtEndUnobserved` | 15.98 ns / 144 B | 17.53 ns / 144 B | 1.10x |
| `InsertAndRemoveAtFrontFiltered` | 4,627.27 ns / 1416 B | 4,715.27 ns / 1416 B | 1.02x |
| `InsertAndRemoveAtFrontObserved` | 3,821.92 ns / 144 B | 3,856.97 ns / 144 B | 1.01x |
| `InsertAndRemoveAtFrontUnobserved` | 1,464.26 ns / 144 B | 1,497.61 ns / 144 B | 1.02x |

Moving the copy at the front of ten thousand costs the observed arms' difference between front and end less the unobserved arms', **2,327 ns against 2,328**. The filtered arm at the end moved 1.05x while the unobserved control beside it moved 1.10x.

### Chains of queries

`OperatorConstructionBenchmarks`, 256 standing collections, `Slice` the seven queries GS5 builds for a time slice. The columns are the overnight 6.1.0 run `BenchmarkRun-20260927-031028`, then `104049`, `131153` and this run; the ratio is this run against 6.1.0.

| arm | 6.1.0 | `104049` | `131153` | after | |
|---|---:|---:|---:|---:|---:|
| `Root`, zero elements | 37.70 μs / 146 KB | 35.08 μs / 130 KB | 35.34 μs / 130 KB | 36.31 μs / 130 KB | 0.96x |
| `Where`, zero elements | 49.95 μs / 166 KB | 54.84 μs / 170 KB | 56.60 μs / 170 KB | 59.91 μs / 170 KB | 1.20x |
| `Select`, zero elements | 51.41 μs / 166 KB | 54.90 μs / 170 KB | 58.53 μs / 170 KB | 61.33 μs / 170 KB | 1.19x |
| `Cast`, zero elements | 51.35 μs / 166 KB | 55.45 μs / 170 KB | 57.66 μs / 170 KB | 61.87 μs / 170 KB | 1.20x |
| `Concat`, zero elements | 49.55 μs / 128 KB | 55.57 μs / 132 KB | 55.98 μs / 134 KB | 58.00 μs / 134 KB | 1.17x |
| `SelectMany`, zero elements | 111.43 μs / 326 KB | 121.54 μs / 344 KB | 125.73 μs / 346 KB | 134.34 μs / 346 KB | 1.21x |
| `Slice`, zero elements | 669.63 μs / 1,800 KB | 739.18 μs / 1,916 KB | 753.15 μs / 1,924 KB | 820.79 μs / 1,924 KB | 1.23x |
| `Root`, one element | 37.70 μs / 146 KB | 35.98 μs / 138 KB | 36.07 μs / 138 KB | 38.13 μs / 138 KB | 1.01x |
| `Where`, one element | 118.52 μs / 436 KB | 126.18 μs / 436 KB | 125.22 μs / 436 KB | 135.17 μs / 436 KB | 1.14x |
| `Select`, one element | 87.01 μs / 370 KB | 95.40 μs / 370 KB | 98.04 μs / 370 KB | 103.31 μs / 370 KB | 1.19x |
| `Cast`, one element | 88.89 μs / 370 KB | 93.68 μs / 370 KB | 97.30 μs / 370 KB | 105.41 μs / 370 KB | 1.19x |
| `Concat`, one element | 49.51 μs / 128 KB | 55.57 μs / 132 KB | 56.54 μs / 134 KB | 58.00 μs / 134 KB | 1.17x |
| `SelectMany`, one element | 167.88 μs / 596 KB | 178.09 μs / 610 KB | 177.84 μs / 612 KB | 191.53 μs / 612 KB | 1.14x |
| `Slice`, one element | 1,455.71 μs / 3,048 KB | 1,539.89 μs / 3,144 KB | 1,597.06 μs / 3,152 KB | 1,764.25 μs / 3,152 KB | 1.21x |

At zero elements no expression is observed, so nothing 6.1.1 or 7.0.0 changed reaches those rows, and they are 7.0.1's alone. **Building a query over an empty collection takes 1.17x to 1.23x the time it did, on every arm but `Root`**, and a slice 3.2 μs where it took 2.6. `104049` put it at 1.07x to 1.12x and `131153` at 1.12x to 1.14x, and this run's controls ran up to 9% slow, so 7.0.1's own price for building over an empty collection lies somewhere between 7% and 23%, and these runs cannot narrow it further. Allocation is what `131153` recorded, to the kilobyte the report shows: nothing this round added allocates while building. `Root` allocates less than in 6.1.0 because an observation keeping a copy no longer subscribes to the list's property notifications.

What this round added to building is two updates of a thread-local count for every change lock taken. How many locks a slice takes has not been counted, so what share of the 9% between `131153` and this run is that count, rather than the drift the controls show, is not established. The release note states the slice's figure from this run.

### Flattening

`ObserveSelectManyBenchmarks`, each cell 6.1.0, then `131153`, then this run:

| arm | 100 teams | 1,000 | 10,000 |
|---|---:|---:|---:|
| `InnerAddAndRemove` | 120.4 / 135.3 / **138.2 ns**, 1.15x | 179.9 / 192.1 / **206.9 ns**, 1.15x | 218.6 / 242.8 / **251.2 ns**, 1.15x |
| `EnumerateByIndexer` | 5,771.1 / 6,033.6 / **6,088.8 ns**, 1.06x | 120,904.8 / 129,448.4 / **129,467.7 ns**, 1.07x | 1,647,196.0 / 1,767,893.1 / **1,810,467.0 ns**, 1.10x |
| `EnumerateThenSweepByIndexer` | 5,829.4 / 5,844.9 / **5,837.4 ns**, 1.00x | 62,914.2 / 59,013.4 / **58,928.2 ns**, 0.94x | 656,742.4 / 659,845.0 / **662,253.9 ns**, 1.01x |
| `Enumerate` | 339.8 / 339.4 / **344.8 ns**, 1.01x | 3,220.0 / 3,217.8 / **3,255.9 ns**, 1.01x | 33,465.8 / 33,447.6 / **33,969.5 ns**, 1.02x |

Allocation is unchanged, 288 B an inner change and 40 B an enumeration. **Following a change to an inner sequence costs 1.15x what it did in 6.1.0 at every size, and reading by index 1.06x to 1.10x on `EnumerateByIndexer`**, though 0.94x to 1.01x on `EnumerateThenSweepByIndexer`. Enumerating, which reads the same copies without looking one up per element, moved 1.01x to 1.02x. Whether the observation of the selector also hears an inner change, and so pays what 6.1.1 added to an evaluation, has not been established, so these runs still cannot say how much of the 15% is 7.0.1's; the release note compares with 6.1.0 and says so.

### Lambdas

`QueryLambdaReuseBenchmarks`, 256 one-element collections, 6.1.0, `131153` and this run:

| arm | 6.1.0 | `131153` | after | against `131153` | against 6.1.0 |
|---|---:|---:|---:|---:|---:|
| `AllShared` | 203.41 μs / 662 KB | 216.38 μs | 231.10 μs / 676 KB | 1.07x | 1.14x |
| `Cast` | 88.55 μs / 370 KB | 98.38 μs | 104.30 μs / 370 KB | 1.06x | 1.18x |
| `DictionaryCast` | 154.90 μs / 722 KB | 166.13 μs | 173.20 μs / 724 KB | 1.04x | 1.12x |
| `DictionaryWhereShared` | 157.12 μs / 714 KB | 168.01 μs | 172.30 μs / 716 KB | 1.03x | 1.10x |
| `GroupByShared` | 383.64 μs / 1,198 KB | 417.41 μs | 432.80 μs / 1,226 KB | 1.04x | 1.13x |
| `MaxByShared` | 311.33 μs / 1,138 KB | 340.84 μs | 354.00 μs / 1,154 KB | 1.04x | 1.14x |
| `OfType` | 214.10 μs / 844 KB | 236.39 μs | 251.10 μs / 854 KB | 1.06x | 1.17x |
| `OrderByRebuilt` | 1,893.82 μs / 2,106.08 KB | 2,032.35 μs | 2,096.80 μs / 2,178.36 KB | 1.03x | 1.11x |
| `OrderByShared` | 490.22 μs / 1,686 KB | 531.78 μs | 546.50 μs / 1,748 KB | 1.03x | 1.11x |
| `SelectShared` | 87.69 μs / 370 KB | 99.41 μs | 105.20 μs / 370 KB | 1.06x | 1.20x |
| `ToDictionaryShared` | 341.52 μs / 1,174 KB | 352.51 μs | 371.80 μs / 1,188 KB | 1.05x | 1.09x |
| `ToLookupShared` | 1,270.50 μs / 3,124 KB | 1,346.10 μs | 1,429.00 μs / 3,160 KB | 1.06x | 1.12x |
| `WhereRebuilt` | 14,228.80 μs / 2,654.36 KB | 14,644.83 μs | 14,800.50 μs / 2,654.36 KB | 1.01x | 1.04x |
| `WhereShared` | 117.88 μs / 436 KB | 128.26 μs | 135.20 μs / 436 KB | 1.05x | 1.15x |

Every arm moved 1.01x to 1.07x against `131153`, the size of the drift in the controls, and allocates what it did but for `OrderByRebuilt`, whose compilation varies. Both readmes now quote **135 μs** for a held predicate and **14,801 μs** for one written inline, whose standard deviation is 31% of it and whose distribution BenchmarkDotNet flagged as bimodal. These arms each observe an expression, so against 6.1.0 they carry 6.1.1's cost as well.

### Synchronized queries

No benchmark in the suite observes a sequence with `ObserveUsingSynchronizationContext`, `ObserveUsingSyncRoot` or `ObserveUsingSynchronizationCallback`, before this round or after it, and this round changed how every one of them applies a change and how the context queries read. What that costs is not measured.

### The readmes

Both readmes' comparison with DynamicData now quotes this run: 13.2 ns against 201.9 for a property change leaving a filtered view alone, 254.4 against 630.6 for an element changing group, 1,280.6 against 982.4 for one moving in a sorted view, 318 μs against 2,537 to build a filtered view; the sorting crossover at about 1,500 elements, with this library 1.80x faster at four thousand and 2.92x at ten thousand; the grouping crossover at about 8,400, 8,385 by linear interpolation, with this library 2.54x faster at a thousand and DynamicData 1.15x faster at ten thousand; and per change 13.9, 15.5 and 66.7 ns at a thousand, ten thousand and a hundred thousand against 212.1, 233.5 and 367.6, a lead of 15.2x, 15.1x and 5.5x. **The readmes said `ToObservableChangeSet()` cost DynamicData about 210x what its `SourceCache` does; no run of record supports it: the overnight 6.1.0 run measured 152x and the 6.1.1 run 153x.** This run measures 162x, and both readmes now say about 160x. The footprint figures were not re-measured.

## What remains

- **What a synchronized query costs to apply a change**, before or after this round. An instrument would need a context which runs what it is sent at once, so that the arms price the queue and the deferral rather than a thread switch. Taken up in `2026-09-28-the-release-beside-it.md`: 1.9x to 2.3x 7.0.0's time, allocating less.
- **What holding locks while building costs, decomposed.** 7.0.1 builds over an empty collection 1.07x to 1.23x slower across three runs; the locks a slice takes and the price of each are uncounted. Taken up in `2026-09-28-the-release-beside-it.md`, which prices each lock beyond the first at 23 to 27 ns a build.
- **The one-element building rows, the flattening and the lambda arms** still compare only with 6.1.0, and a 7.0.0 run is the only thing that would separate 6.1.1's share. Taken up in `2026-09-28-the-release-beside-it.md`, against 7.0.0 in the same run.
