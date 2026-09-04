# 4 September 2026 — the invocation wrapper costs time, not memory

`LambdaInvocationRewriter` was written on 2 September to remove an allocation, measured zero, and was reverted. `2026-09-04-select-query-storage.md` argued it deserved a second hearing because the first measurement was taken under 568 bytes of notification noise. It has now been re-tested on a clean instrument at one site. **The allocation hypothesis is dead — measured zero a second time.** The change is worth keeping anyway, for a reason nobody was looking for.

## The measurement

Only `ObserveSelect`'s pair-selector composition was changed. Every other arm is a control.

| arm | allocated before | allocated after | time before | time after |
|--- |---: |---: |---: |---: |
| `ChangeEveryValueInASelectQuery` | 256,000 B | 256,000 B | 181.9 μs | **139.2 μs** |
| `ChangeEveryValueInASelectQueryWithASubscriber` | 344,000 B | 344,000 B | 204.6 μs | **160.8 μs** |
| `ChangeEveryValueInAWhereQuery` | 96,000 B | 96,000 B | 132.6 μs | 130.4 μs |
| `ChangeEveryValueInAWhereQueryWithASubscriber` | 256,000 B | 256,000 B | 168.4 μs | 165.1 μs |
| `ChangeEveryValueInAnAllQuery` | 256,000 B | 256,000 B | 171.9 μs | 173.0 μs |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96,000 B | 65.6 μs | 65.4 μs |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48,000 B | 9.16 μs | 8.35 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808,000 B | 150.5 μs | 152.3 μs |
| `ReplaceOneKeyWithNoObservation` | 0 B | 0 B | 4.83 μs | 4.77 μs |

**Not one byte moved anywhere.**

The two select arms fell 23% and 21% in time. Between-run drift on this pair of runs, read off the seven controls, ran from −9% to +1%, and the largest control excursion was the bare arm at −8.8% — so the select movement is roughly two and a half times the worst control, in the same direction for both select arms, while both where arms and the `All` arm sat still. That is an effect, not drift. It is the kind of claim that would be worth a repeat run if a decision hung on it; nothing does, because the change is green and free.

## Why the allocation was never there

An `ObservableInvocationExpression` disposes and rebuilds the whole expression beneath it whenever one of its **arguments** changes. In this benchmark the argument is the key-value pair, and the pair does not change — the `Rank` of the person it holds does. The invocation node therefore never rebuilds. What it costs is a node's worth of traversal on every evaluation, which is time and nothing else.

So the 160 bytes by which a select observation exceeds a where observation are not the wrapper. They are something else in the composition — the `Expression.New` that builds the projected `KeyValuePair` on each evaluation is the obvious candidate — and that remains unexplained.

**The mechanism did not change.** Removing the invocation also makes the expression visible to `DirectSubscriptionAnalyzer`, and I said before the run that a drop could be the node's cost or the observation newly qualifying for the fast path, with no way to tell them apart. The allocation figures settle it: a switch to direct subscription would not leave every byte identical. These observations are still going through the graph.

## Verdict, recorded so it is not re-proposed a third time

- **As an allocation change, this is finished.** Two measurements, both zero, the second on an instrument with nothing left to hide behind. Anyone who reads `LambdaInvocationRewriter` and thinks it will save memory should stop here.
- **As a time change, it is worth 20%+ on a wrapped observation's propagation** and costs nothing. It is kept on that basis.

## What changed in the rewriter since 2 September

It is now a `sealed class : ExpressionVisitor` with a static `Apply`, matching `FixedSubexpressionRewriter` beside it, rather than a static class wrapping a nested visitor.

More importantly it **declines** instead of assuming. Where the lambda's body quotes an expression, substituting inside the quoted tree would change what a caller reads back out of it, so `Apply` returns null and the call site falls back to `Expression.Invoke`. Any selector that builds a query over an `IQueryable` inside itself carries a quote, so this is a real case that the original version would have rewritten blindly.

## Next

`ObserveWhere` wraps its predicate the same way and was deliberately left alone, so the two where arms above are an untouched before for extending the rewriter to it — and to the other seven sites. The expectation is time, not memory, and any site whose arm does not move should be left wrapped rather than changed on principle.

The 808-byte observed indexer is still untouched.

## A process failure worth recording

This run had to be built twice because I based an edit on a copy of `ObservableDictionaryQuery.cs` staged two turns earlier, before the per-event split existed, and committing it silently reverted that split. The existing habit says re-stage a file before reasoning from it. That is too weak. The rule is: **a staged copy is a valid base for an edit only if it was staged after the last thing I wrote to that path.** Writing from a stale snapshot is worse than reading one, because it destroys newer work rather than merely misinforming.
