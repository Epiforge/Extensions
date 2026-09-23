# What a query carried for what it never did

*2026-09-23 — `OperatorConstructionBenchmarks`, before and after queries began creating their notification-deferral state on their first change, and collection queries began keeping their rarer child-query caches in one object created with the first of them*

## The question

GS5 snapshotted the heap with ClrMD at the point its person query over 4,096 time slices had been built, on 6.0.4: 87.3 MB and 1,154,967 objects, 22.3 KB and 282 objects a slice. `Lock` was the largest single type at 52 a slice, handler delegates the largest group at 56 a slice, and collection query objects themselves weighed 400 to 490 bytes each.

## What was found in the source

Every query created a lock, a depth counter and four slots for holding its notifications back while a mutation completes. Only change handlers defer notifications; building a query never does, so a query which is built and never changes carried all of it for nothing. Every collection query also carried 27 reference fields for its caches of child queries, which 6.0.4 had made lazy but left on the query; a query over which nothing is built, or only a projection, a flattening, a filter or a concatenation, needed at most four of them.

The deferral state is now created by the first deferral and published with an interlocked compare-and-exchange, and whether a deferral is under way is still read without the lock, as it was. The caches of the other 23 kinds live in one object created under the cache lock with the first of them. `ObserveCount` and `ObserveIndividualChanges` now take their query into a local under that lock rather than reading the field again after releasing it.

## Before and after

256 builds and disposals over standing sources, lambdas held, GS5's optimizer.

| arm | 0 elements, before | after | 1 element, before | after |
|---|---:|---:|---:|---:|
| Root | 40.59 μs / 204 KB | **36.89 μs / 140 KB** | 40.90 μs / 204 KB | **35.66 μs / 140 KB** |
| Where | 48.14 μs / 242 KB | **44.36 μs / 178 KB** | 118.06 μs / 512 KB | **108.70 μs / 448 KB** |
| Cast | 60.57 μs / 306 KB | **63.43 μs / 242 KB** | 99.48 μs / 510 KB | **98.25 μs / 446 KB** |
| Select | 57.72 μs / 306 KB | **55.24 μs / 242 KB** | 101.83 μs / 510 KB | **91.59 μs / 446 KB** |
| Concat | 55.15 μs / 286 KB | **50.40 μs / 222 KB** | 54.70 μs / 286 KB | **50.27 μs / 222 KB** |
| SelectMany | 166.56 μs / 702 KB | **152.64 μs / 574 KB** | 229.05 μs / 972 KB | **208.10 μs / 844 KB** |
| Slice | 944.32 μs / 3,308 KB | **849.22 μs / 2,732 KB** | 1,812.15 μs / 4,556 KB | **1,593.04 μs / 3,980 KB** |

Every collection query is 256 bytes lighter; a source query fell from 816 to 560. The select-many arm fell by twice that, because it builds a projection of its own. GS5's chain for one slice holding one trait value fell from 17.8 KB to 15.5 KB and from 7.1 μs to 6.2 μs.

## Predictions scored

- **Held within 4 bytes.** 252 bytes a collection query, 76 from the deferral state and 176 from the caches: measured 256.
- **Held within 1%.** Root about 141 KB: 140. Slice at no elements about 2,740 KB: 2,732. Slice at one element about 3,990 KB: 3,980.

## What has not been measured

GS5's traversal on this version. `QueryLambdaReuseBenchmarks`, the source of the README's figures on holding lambdas, was not re-run; its arms build the same queries and should fall by the same 256 bytes a query, so the README's 117 against 9,784 μs is 6.0.4's. The handler delegates, 56 a slice and 16% of GS5's graph, were left for a change of their own: a subscription that is not a delegate changes the order in which a query's children and its external subscribers hear of a change, and that order has not been worked out.
