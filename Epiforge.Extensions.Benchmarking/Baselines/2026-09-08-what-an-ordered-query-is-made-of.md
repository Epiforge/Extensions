# What an ordered query is made of

*2026-09-08 — `OrderByMembershipShareBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## The decomposition

Four arms make the identical change — one element appended to the source and removed again — with a different layer of the ordered query standing over it.

| arm | mean | allocated | its own share |
|---|---|---|---|
| `AddAndRemoveUnobserved` | 16.65 ns | 144 B | the collection alone |
| `AddAndRemoveWithSourceQuery` | 24.82 ns | 144 B | **0 bytes**, 8.2 ns |
| `AddAndRemoveWithSelectQuery` | 4,179.70 ns | 4,792 B | **4,648 bytes, 4,155 ns** |
| `AddAndRemoveWithOrderedQuery` | 4,858.80 ns | 5,261 B | **469 bytes, 679 ns** |

**Observing the source costs nothing per change.** The wrapper's 4.3 KB is fixed for the life of the query and none of it is per-notification.

**The projection is 88% of it.** Constructing and disposing one observation of the wrapped key selector for one element costs 4,648 bytes and 4,155 nanoseconds. Everything the ordering machinery does — the comparer, the treap, the results collection and its notifications — is 469 bytes and 679 nanoseconds, **8.9%**.

The ordered arm lands at 5,261 bytes, which is `ObserveOrderByBenchmarks.SourceAddAndRemove` at a thousand elements to the byte, from a different class with a different setup. Two instruments agreeing exactly is worth more than either alone.

## Why the projection costs what it does

4,648 bytes to construct and dispose one observation is about 4.4x what a query's per-element construction share says an observation of a member read with one subscription costs. The reason is in `DirectSubscriptionAnalyzer.AnalyzeNode`, which handles constants, parameters, members, indexes, method calls, binaries, conditionals, type tests and unaries, and ends:

    _ => new(expression, DirectSubscriptionIneligibility.UnsupportedExpressionKind)

**There is no case for `NewExpression`.** `ObservableCollectionOrderByQuery.CachedWrappedSelectorsValueFactory` builds its selector as `Expression.New(typeof(Tuple<TElement, IComparable>).GetConstructor(...), parameter, key)`, so the body of every ordered query's key projection is a `NewExpression` and every one of them is refused. **Ordered queries have never used the fast path for the projection they are built on.** The ratio the measurement shows — 4.4x the allocation and roughly 10x the time — is the graph, which the record already prices at 6.4x the fast path for one subscription.

The same wrapping is used by `ObserveGroupBy` and `ObserveLookup`, so the refusal is theirs as well. Nothing had noticed, because no instrument ever stood the projection up on its own; the graph arms of the eligibility instruments cover the shapes they were written for, and a constructor was never one of them.

## The fix, unwritten as of this document

`AnalyzeMethodCall` is the pattern: refuse where the result type could require disposal, then analyze the target and every argument, then be eligible. A `NewExpression` is the same thing without a target.

    DirectSubscriptionAnalysis AnalyzeNew(NewExpression newExpression, Planner? planner)
    {
        if (!ExpressionObserverOptions.CannotBeDisposed(newExpression.Type))
            return new(newExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        for (int i = 0, ii = newExpression.Arguments.Count; i < ii; ++i)
            if (AnalyzeNode(newExpression.Arguments[i], planner) is { IsEligible: false } argumentAnalysis)
                return argumentAnalysis;
        return DirectSubscriptionAnalysis.Eligible;
    }

This widens eligibility for every expression containing a constructor, not only the ones the query layer generates, so it wants agreement tests of its own before it is believed.

## What today did to the ordering layer

The four grouping sites removed earlier on 8 September were all in `ObservableCollectionOrderingComparer` and `ObservableCollectionOrderByQuery` — the ordering layer, and nothing else. That layer therefore stood at about 1,912 bytes this morning and stands at 469 now, **75% of it gone**. It is close to exhausted as a target, and the 4,648 beside it has not been touched at all.
