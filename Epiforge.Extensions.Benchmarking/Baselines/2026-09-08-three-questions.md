# Three questions the record could not answer

*2026-09-08 — `QueryShareBenchmarks`, `PropertyKeyingBenchmarks`, `OrderByKeyChangeScalingBenchmarks`, DefaultJob, MemoryDiagnoser*

## Is the query's per-element share one number?

No. Each query shape has its own, and they are far apart.

| query | total | its observation, priced alone | the query's share |
|---|---|---|---|
| `ObserveWhere(person => person.Rank > 0)` | 991.9 B | 728.0 B | **263.9 B** |
| `ObserveSelect(person => person.Rank)` | 1077.4 B | 752.0 B | **325.4 B** |

Both figures are per element, with the 4.3 KB collection wrapper taken out. **61.5 bytes apart**, so there is no shared per-element query machinery to attack — the cost belongs to each query's own bookkeeping. `ObservableCollectionSelectQuery` keeps a `Projection` object for every element that `ObservableCollectionWhereQuery` has no counterpart for, and its state dictionary carries a wider value, which is the right order of magnitude for the gap.

What this leaves standing is the reading of `ObserveElementWithAccess`: of the `Where` query's 263.9 bytes, a treap node, a dictionary entry, and a `List<T>` holding exactly one node in every case but a repeated element. The list is between a quarter and a third of the total and is the only part of it with an obvious route.

Ordering is absent from the comparison on purpose. `ObserveOrderBy` types its key selector as `Expression<Func<TElement, IComparable>>`, so its observation is not the one priced alone in `ObservationConstructionBenchmarks` and its share cannot be had by the same subtraction.

## What does keying a subscription by property name cost, and what does it buy?

An expression reading two properties of one object places two handlers on it through the fast path, where the graph places one. Both sides, measured.

**Construction.** A `Where` over a predicate reading one property costs 973.00 KB per thousand; over one reading two, 1324.56 — **360.0 bytes per element for the second property**. That is not the 64-byte marginal subscription: a second property is a different registry key, so it builds a whole `DirectSubscriptionSource`, its own event registration on an object already subscribed to, and its own dictionary entry. It is 26.6% of what that query costs to build.

**Propagation.** Changing the rank of every one of a thousand elements, with an observation of two of their properties standing:

| arm | mean | allocated |
|---|---|---|
| nothing observing | 9.171 μs | 46.88 KB |
| observed, fast path | 21.822 μs | **46.88 KB** |
| observed, graph | 40.439 μs | 70.31 KB |

**The fast path allocates nothing at all per change** — the 46.88 KB is 48 bytes per element of `PropertyChanging` and `PropertyChanged` arguments which the changed objects raise before either mechanism sees anything, and which is the consumer's cost, not the library's. It matches the recorded constant exactly. The graph's 70.31 is those same 48 plus **24 bytes per change of its own**, which is the boxed result.

So the trade is one-sided on allocation: merging the two handlers would save 360 bytes per element at construction and cost nothing per change in allocation, only the time of a handler waking for a property it does not want. Observed, the fast path costs **12.65 ns per change** against the graph's 31.3.

## Does an ordered query's cost for one key change grow because it evaluates more, or because each evaluation costs more?

Because each costs more.

| elements | mean | allocated |
|---|---|---|
| 250 | 1.115 μs | **775 B** |
| 1000 | 1.499 μs | **776 B** |
| 4000 | 2.131 μs | **776 B** |

**Allocation is flat to the byte across a sixteenfold change in size while the time nearly doubles.** A query which evaluated more would allocate more. The time grows by about 0.25 μs per doubling of the collection, which is the signature of a climb whose depth is logarithmic in the element count — the ordered query's own structure, not extra evaluation. The finding recorded on 4 September, that the saving "grows with element count where one node's traversal per evaluation predicts a constant", is answered: the traversal is not constant, the structure is a tree.

**And one key change allocates 776 bytes**, which is a large number for a single change and is not decomposed here. Part of it is the boxed `IComparable` key the public signature requires.
