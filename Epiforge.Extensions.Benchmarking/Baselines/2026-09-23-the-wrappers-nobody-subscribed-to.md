# The wrappers nobody subscribed to

*2026-09-23 — `MonitorAcquisitionBenchmarks` and `ScopedWrapperDisposalBenchmarks`, before and after the scoped wrappers began attaching to their queries only for what is subscribed to them*

## The question

GS5 profiled its traversal on 6.0.2 with EventPipe CPU sampling. On the benchmark thread, 38.3% of samples were self time in `Monitor.Enter_Slowpath`, 10.6% in the slow worker behind `RuntimeHelpers.GetHashCode`, and about 20% removing delegates: `MulticastDelegate.RemoveImpl`, `DeleteFromInvocationList`, and the removal inlined into `ScopedObservableCollectionQuery.Dispose`. Nothing else ran managed code. GS5 asks one shared query for a lookup once per time slice, and asked whether each wrapper's own subscription to the shared lookup was necessary.

## Locks and hash codes, measured before anything was changed

The locks at the hot call sites are per-query `readonly object access = new()`, and `ObservableCollectionSelectQuery.OnInitialization` locks one created moments earlier, so the profile read as a fresh object's first acquisition taking the slow path. That was a reading of the profile, and it was measured before anything was built on it.

| arm | time | allocated |
|---|---:|---:|
| allocate an object — baseline | 1.98 ns | 24 B |
| first hash of a fresh object | 13.83 ns | 24 B |
| hash of an object already hashed | 0.80 ns | |
| hash of an object carrying a sync block | 0.99 ns | |
| lock a fresh object | 13.60 ns | 24 B |
| lock a fresh object twice | 26.91 ns | 24 B |
| lock a long-lived object | 12.74 ns | |
| lock an object carrying a sync block | 11.97 ns | |
| lock a fresh `Lock` | 11.48 ns | 40 B |
| lock a long-lived `Lock` | 12.23 ns | |
| **hash a fresh object, then lock it** | **290.51 ns** | 24 B |

**The reading was wrong.** A fresh object's first acquisition costs what any other acquisition costs. The one dear case is an object whose header already holds a hash code when it is first locked, and no lock target in the library is ever hashed. GS5 confirmed that Tachyon has no `lock` statement and no `Monitor` call anywhere, that its entities override `GetHashCode` with a cached value, and that its trait values hash by identity. So neither the 38% nor the 10.6% is explained by the uncontended cost of either operation. **The unmeasured guess** is that time the thread spent suspended for collections, of which the profile's allocation rate would cause many, was attributed to whichever runtime call it was in. Switching the per-query locks to `Lock` would save about half a nanosecond an acquisition and cost 16 bytes a query, and was not done.

## Disposing many wrappers over one shared query

Every scoped wrapper attached handlers for `PropertyChanged` and `PropertyChanging` to its query in its constructor, and a lookup's wrapper for both `DictionaryChanged` events as well, whether or not anything ever subscribed to the wrapper. `CollectionChanged` was already attached on the first subscriber. Removing a handler from a delegate searches its invocation list from the end and copies what remains into a new array, so N wrappers over one query cost O(N²) to dispose, in time and in memory, in either order.

`ScopedWrapperDisposalBenchmarks` makes N wrappers over one standing shared query and disposes them, sixteen source elements, GS5's optimizer.

| arm | before | events attached lazily | and the lock created lazily |
|---|---:|---:|---:|
| 256 lookups, creation order | 459.4 μs / 1.67 MB | 23.84 μs / 36 KB | **20.84 μs / 26 KB** |
| 1,024 lookups, creation order | 7,078.0 μs / 22.69 MB | 97.05 μs / 144 KB | **83.26 μs / 104 KB** |
| 4,096 lookups, creation order | 106,886.3 μs / 346.77 MB | 393.80 μs / 576 KB | **335.76 μs / 416 KB** |
| 4,096 lookups, reverse order | 42,897.7 μs / 346.77 MB | 392.61 μs / 576 KB | **331.45 μs / 416 KB** |
| 4,096 filters, creation order | 43,853.6 μs / 173.57 MB | 307.89 μs / 480 KB | **248.10 μs / 320 KB** |

At 4,096 lookups: **318x faster and 854x lighter**. Growth from 1,024 to 4,096 went from 15.1x to 4.03x, which is linear, and the two orders are within 1.3%.

**The second change was measured separately from the first.** Allocation after the first was exactly 144 bytes a lookup wrapper and 120 a filter's, of which 40 was the `Lock` every wrapper made in its constructor; the collection and dictionary wrappers always had, and the first change had given the scalar wrapper one too. Each wrapper now creates it on the first subscription, publishes it with an interlocked compare-and-exchange, and at disposal locks only if one exists, since if none does nothing was ever attached. Allocation fell to exactly 104 and 80 bytes, and time fell 14.7% with it.

## A defect found while writing the change

`CollectionChanged` on the collection and dictionary wrappers, and both `DictionaryChanged` events on the dictionary wrapper, attached to the shared query when a handler was added after the wrapper had been disposed, and the disposed wrapper then forwarded the live query's changes. Every event on every wrapper now ignores a handler added after disposal. `ScopedQueryForwarding` pins it in four tests, three of which failed on all five frameworks before the change (collection filter, dictionary filter, lookup); the count wrapper's passed, because it had no such event. The other twelve tests in the class pin forwarding to handlers added after creation, removed and added again, and after another wrapper over the same query is disposed; they passed before and after.

## Predictions scored

- **Falsified, as it was built to be able to.** A fresh object's first lock several times a long-lived one's: 13.60 ns against 12.74.
- **Held.** A fresh `Lock` about its allocation plus a few nanoseconds: 11.48 ns and 40 bytes.
- **Held.** A fresh object hashed then locked well above one only locked: 290.51 against 13.60.
- **Held, by too little to matter.** Hashing an object carrying a sync block dearer than one whose header holds the hash: 0.99 ns against 0.80. Both are a small fraction of a fresh object's first hash.
- **Held.** Disposal in creation order growing about 16x per 4x before the change: 15.1x.
- **Wrong in direction, and relayed.** Reverse order was advised to GS5 as cheap; it cost the same 346.77 MB, because removal copies what remains wherever it removes from. GS5 did not act on it.
- **Loose.** Allocation before the change at N²/2 references a list: measured 1.29x that, because a shrinking invocation list keeps up to twice the capacity it needs.
- **Beaten.** 4,096 lookups after the first change predicted at 1 to 3 ms and under 2 MB: 393.80 μs and 576 KB.
- **Held.** The two orders within 9% after the change: 0.3%, then 1.3%.
- **Held exactly.** The lazy lock predicted at 416, 104, 26 and 320 KB: all four to the byte.
- **Loose.** Time with the lazy lock predicted to fall slightly or stay within 9%: it fell 14.7%.

## What has not been measured

GS5's traversal on 6.0.3. What the 38% of samples in `Monitor.Enter_Slowpath` and the 10.6% in the hash worker actually were; the uncontended costs above cannot account for them. A scalar wrapper on its own; its lock was never measured, only removed.
