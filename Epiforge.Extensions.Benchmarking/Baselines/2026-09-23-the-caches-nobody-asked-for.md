# The caches nobody asked for

*2026-09-23 — `OperatorConstructionBenchmarks` and `QueryLambdaReuseBenchmarks`, before and after queries began creating their caches of child queries, and the lock guarding them, only when the first child is built*

## The question

GS5 measured its traversal on 6.0.3 with its own `SelectMany` selectors held in fields: 496.77 ms and 252.08 MB for one person over 4,096 time slices, with collections pausing the thread for 46–57% of each call and every collection at least gen1. `GC.GetTotalMemory(true)` read around a call put 225.6 MB of the 252 in the live graph, so about 90% of what a call allocates lives as long as the query. Its gc-verbose allocation profile charged 51.4% of all sampled bytes to `ObservableCollectionQuery<TElement>`'s constructor, and 20.4% to `Lock` alone. GS5 asked what each operator costs to build over zero and one element.

## What every query built whether or not it was asked

Every collection query created 26 locks and 24 dictionaries in its constructor, one of each for every kind of child query it could share among identical observations. Every dictionary query created 12 and 10, and every scalar query one and one. A leaf query, or one with a single child, uses none or one of them, and they live as long as the query. Counting the type names in GS5's trace agreed: 3,177 `Lock` samples, the most of any type.

Each query now has one lock for all of its caches, created by the first observation that needs it and published with an interlocked compare-and-exchange, and each cache is created on first use. A query which has never had a child counts its cached queries without taking a lock. The dictionary query keeps its own change lock, which every dictionary query uses. Children of different kinds on one parent now take turns on that lock, which is held only for a lookup and a constructor; the one constructor that takes other locks, the lookup's, takes them in the order it always did.

## Before and after

`OperatorConstructionBenchmarks` builds one query over each of 256 standing sources, none sharing a query with another, with lambdas held in static fields and GS5's optimizer, and disposes them all. `Root` builds the source query itself; `Slice` builds the seven queries GS5's traversal builds from a lookup's grouping.

| arm | 0 elements, before | after | 1 element, before | after |
|---|---:|---:|---:|---:|
| Root | 126.9 μs / 1.01 MB | **40.59 μs / 204 KB** | 127.2 μs / 1.01 MB | **40.90 μs / 204 KB** |
| Where | 136.4 μs / 1.04 MB | **48.14 μs / 242 KB** | 229.7 μs / 1.31 MB | **118.06 μs / 512 KB** |
| Cast | 149.4 μs / 1.11 MB | **60.57 μs / 306 KB** | 199.7 μs / 1.30 MB | **99.48 μs / 510 KB** |
| Select | 150.2 μs / 1.11 MB | **57.72 μs / 306 KB** | 197.8 μs / 1.30 MB | **101.83 μs / 510 KB** |
| Concat | 147.4 μs / 1.09 MB | **55.15 μs / 286 KB** | 158.8 μs / 1.09 MB | **54.70 μs / 286 KB** |
| SelectMany | 422.3 μs / 2.30 MB | **166.56 μs / 702 KB** | 562.1 μs / 2.56 MB | **229.05 μs / 972 KB** |
| Slice | 5,377.2 μs / 10.35 MB | **944.32 μs / 3,308 KB** | 6,788.7 μs / 11.57 MB | **1,812.15 μs / 4,556 KB** |

A source query fell from about 4.1 KB to 816 B. The arithmetic for what was removed, 26 locks of 40 bytes, 24 empty dictionaries of 80 and the fields that held them, comes to about 3.3 KB, which is what was measured. GS5's chain for one slice holding one trait value fell from 46.3 KB to 17.8 KB and from 26.5 μs to 7.1 μs: **3.7x faster and 2.6x lighter**.

`QueryLambdaReuseBenchmarks`, the source of the README's figures on holding lambdas, over the same 256 one-element sources:

| arm | before | after |
|---|---:|---:|
| AllShared | 337.3 μs / 1.57 MB | 207.74 μs / 752 KB |
| Cast | 198.8 μs / 1.30 MB | 102.60 μs / 510 KB |
| DictionaryCast | 203.6 μs / 1.06 MB | 154.40 μs / 736 KB |
| DictionaryWhereShared | 206.8 μs / 1.06 MB | 158.00 μs / 728 KB |
| GroupByShared | 1,341.6 μs / 4.77 MB | 418.00 μs / 1,584 KB |
| MaxByShared | 471.3 μs / 2.01 MB | 320.40 μs / 1,230 KB |
| OfType | 610.8 μs / 2.62 MB | 235.06 μs / 1,060 KB |
| OrderByRebuilt | 2,797.6 μs / 3.89 MB | 1,854.09 μs / 2,342 KB |
| OrderByShared | 1,104.8 μs / 3.48 MB | 506.06 μs / 1,910 KB |
| SelectShared | 196.4 μs / 1.30 MB | 99.78 μs / 510 KB |
| ToDictionaryShared | 591.3 μs / 2.45 MB | 341.32 μs / 1,328 KB |
| ToLookupShared | 4,478.9 μs / 8.67 MB | 1,388.53 μs / 3,620 KB |
| WhereRebuilt | 10,563.1 μs / 3.48 MB | 9,784.08 μs / 2,764 KB |
| WhereShared | 220.0 μs / 1.31 MB | **117.23 μs / 512 KB** |

The README's held-against-inline figures move from 239 against 10,471 μs to 117 against 9,784; the lesson stands and grows sharper, since holding a lambda is now 83x rather than 44x cheaper.

## Three defects found on the way

**Releases lost under concurrency.** `ChildQueryCaches.ConcurrentObservationsOfOneParent`, written as a guard for this change, failed on all five frameworks before it: after 2,000 observations and disposals of ten kinds of child across eight threads, 9 (net10) and 13 (net6 to net9) children stayed cached. Every scoped wrapper released its shared query through `SyncDisposable.Dispose`, which returns at once when another call holds the disposal claim, including a call which ends by keeping the query alive for its other observers. So a release made while another was under way was lost, the count of observations never reached zero, and the child stayed cached and subscribed to its source. `ObservableQuery.Release()` now disposes under the lock initialization already took, renamed `lifetimeAccess`, and every wrapper releases through it. The test passes on all five.

**Swapped synchronization.** `ObserveUsingSynchronizationContext` on a dictionary query built the query that posts changes to the context, and `ObserveUsingSynchronizationContextEventually` the one that sends them. The existing tests could not tell, because reads through the query were marshalled to the same thread and waited behind the delivery. `SynchronizationContextDelivery` uses a context which runs what is sent at once and holds what is posted until released.

**Keyed queries never shared.** A dictionary query's `ObserveLast` and `ObserveLastOrDefault` reversed the comparer anew on every call, and `ReversedComparer<T>` has no equality of its own, so identical observations each built their own keyed query. Each comparer's reversal is now held in a weak table for as long as its caller keeps the comparer.

## Predictions scored

- **Held.** Most of the roughly 4 KB a query costs would go: 80% of it did.
- **Held.** The concurrency test would pass once releases were serialized: 846 of 846 on all five frameworks.

## What has not been measured

**The synchronization and keyed-sharing tests were never run against the code they fix**; they were written in the same round as their fixes, so that they fail without them is argued from the source, not observed. GS5's traversal on this version. What the remaining 816 bytes of a source query are. `DynamicDataComparisonBenchmarks`, whose "building a filtered view" row should move by about 3.3 KB of 973 and was not re-run.
