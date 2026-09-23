# The subscriptions that were delegates

*2026-09-23 — `OperatorConstructionBenchmarks` and `DerivedQueryNotificationOrder`, before and after queries began delivering their changes to the queries derived from them through a registry of their own rather than through event handlers*

## The question

GS5's heap on 6.0.5, at the point its person query over 4,096 time slices had been built, held 56 handler delegates a slice at 64 bytes each: 18% of 19.5 KB. They were the subscriptions of queries to the queries they were built over, three to a projection, one to a filter, four to a concatenation, and six to a flattening, which subscribed to its projection through that projection's scoped wrapper and so paid for the wrapper's own three as well. Every `-=` at disposal built a new delegate from a method group to find the old one, which is why GS5's allocation profile on 6.0.4 charged 8.9% of all sampled bytes to delegates allocated inside `Dispose`.

## The precedent

The observable expression graph moved off `PropertyChanged` for the same reason: a node implements `IObservableExpressionDependent`, and the node it depends on keeps a doubly linked list of 48-byte `ObservableExpressionSubscription`s and calls it directly. Queries now do the same. A derived query implements `IObservableQueryDependent`, receiving collection changes and, where it wants them, property changes and changings; the query it is built over keeps a list of `ObservableQuerySubscription`s, under the lock its caches of child queries already used, which moved to `ObservableQuery` as `ChildrenAccess` so that a query with children needs no second lock. Converted: projection, filter, flattening (through its projection's underlying query), concatenation (its first side always, its second when that is a query, told apart by subscription because both sides may be one query), grouping and count.

**Two departures from the graph, both deliberate.** Delivery stops at the subscriptions which existed when it began: a query built by a handler during delivery starts from the state the change produced, and walking on would deliver the change to it a second time. The graph walks its list live and has the same exposure; it was not touched. And derived queries take a change before any handler subscribed to the query's events hears of it.

## The order of delivery, observed before the change

`DerivedQueryNotificationOrder` was written and run before anything else was. `AHandlerSubscribedBeforeADerivedQueryWasBuiltSeesItAlreadyUpdated` failed on all five frameworks on 6.0.5, reading the derived filter's count as 2 when the change had made it 3: a handler subscribed before a derived query was built heard of the change first. `AQueryBuiltWhileAChangeIsDeliveredDoesNotTakeItAgain` passed, since a delegate list is copied when it is invoked. After the change both pass on all five, 848 of 848.

## Before and after

256 builds and disposals over standing sources, lambdas held, GS5's optimizer. Allocation includes what disposal allocates, which is where most of the difference in the flattening and concatenation arms comes from.

| arm | 0 elements, before | after | 1 element, before | after |
|---|---:|---:|---:|---:|
| Root | 36.89 μs / 140 KB | 36.29 μs / 146 KB | 35.66 μs / 140 KB | 36.43 μs / 146 KB |
| Where | 44.36 μs / 178 KB | 48.87 μs / 166 KB | 108.70 μs / 448 KB | 111.54 μs / 436 KB |
| Cast | 63.43 μs / 242 KB | **49.74 μs / 166 KB** | 98.25 μs / 446 KB | **86.12 μs / 370 KB** |
| Select | 55.24 μs / 242 KB | **49.41 μs / 166 KB** | 91.59 μs / 446 KB | **83.13 μs / 370 KB** |
| Concat | 50.40 μs / 222 KB | **47.94 μs / 128 KB** | 50.27 μs / 222 KB | **47.88 μs / 128 KB** |
| SelectMany | 152.64 μs / 574 KB | **108.87 μs / 326 KB** | 208.10 μs / 844 KB | **162.85 μs / 596 KB** |
| Slice | 849.22 μs / 2,732 KB | **642.42 μs / 1,800 KB** | 1,593.04 μs / 3,980 KB | **1,297.00 μs / 3,048 KB** |

GS5's chain for one slice holding one trait value fell from 15.5 KB to 11.9 KB allocated and from 6.2 μs to 5.1 μs; with none, from 10.7 KB to 7.0 KB and from 3.3 μs to 2.5 μs. **Root is 24 bytes a query heavier**: the registry's first, last and sequence fields on every query, used or not. Where is 4.51 μs slower over 256 builds at no elements and 2.84 at one; its one delegate became one subscription and a field, so it gains least, and nothing in the change reaches a filter's work.

## Predictions scored

- **Held within 3%.** Slice at one element about 3,100 KB: 3,048. At none, about 1,860: 1,800.
- **Held within 2 KB.** Projection and cast about 165 KB: 166. Concatenation about 127: 128. Flattening about 324: 326.
- **Held within 3 KB.** Filter about 163 KB: 166.
- **Held, and by 4 bytes a query more than predicted.** Root about 145 KB: 146, 24 bytes a query against 20, because three fields of 20 bytes pad to 24.
- **Held.** The order test failing before the change with 2, on all five frameworks, and passing after.

## What has not been measured

GS5's traversal and heap on this version; the prediction for its live graph was about 1.3 KB a slice less, 7%. `QueryLambdaReuseBenchmarks`, the source of the README's figures on holding lambdas, which remain 6.0.4's. The graph's own live walk of its dependents, which has the exposure this change guards against in queries.
