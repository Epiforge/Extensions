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

## Extended to the dictionary's where and to-collection compositions

Same change, two more sites, measured on the same instrument. `ObserveAll` builds a where query, so its arm measures rather than controls this time.

| arm | time before | time after | allocated |
|--- |---: |---: |---: |
| `ChangeEveryValueInAWhereQuery` | 130.4 μs | **114.4 μs** | 96,000 B, unchanged |
| `ChangeEveryValueInAWhereQueryWithASubscriber` | 165.1 μs | **140.3 μs** | 256,000 B, unchanged |
| `ChangeEveryValueInAnAllQuery` | 173.0 μs | **148.9 μs** | 256,000 B, unchanged |
| `ChangeEveryValueInASelectQuery` (control) | 139.2 μs | 140.1 μs | 256,000 B |
| `ChangeEveryValueInASelectQueryWithASubscriber` (control) | 160.8 μs | 157.3 μs | 344,000 B |
| `ChangeEveryValueObservedWithoutAQuery` (control) | 65.4 μs | 65.3 μs | 96,000 B |
| `ChangeEveryValueWithNoObservation` (control) | 8.35 μs | 8.40 μs | 48,000 B |
| `ReplaceOneKeyObservedByAnIndexer` (control) | 152.3 μs | 152.7 μs | 808,000 B |
| `ReplaceOneKeyWithNoObservation` (control) | 4.77 μs | 4.75 μs | 0 B |

Controls held within 2.2% on this pair of runs, tighter than the previous pair. The three changed arms fell 12–15%. Not one byte moved anywhere, again.

**The model now has five arms behind it and predicted all five.** Removing an invocation node that adapts a caller's lambda costs zero allocation, saves between a tenth and a quarter of the propagation time, and does not change which mechanism evaluates the expression. The saving is one node's traversal per evaluation, nothing more and nothing less.

## The census of wrapping sites

| site | state |
|--- |--- |
| `ObservableDictionaryQuery.ObserveSelect`, key and value | rewritten, measured |
| `ObservableDictionaryQuery.ObserveWhere` | rewritten, measured |
| `ObservableDictionaryQuery.ObserveToCollection` | rewritten, no arm |
| `ObservableCollectionOrderByQuery` | rewritten, before recorded below |
| `ObservableCollectionGroupByQuery` | rewritten, before recorded below |
| `ObservableCollectionLookupQuery` | **still wrapped — no instrument** |
| `ObservableCollectionToDictionaryQuery` | **still wrapped — no instrument** |

Two further `Expression.Invoke` sites exist and are **not** candidates, recorded here so nobody rewrites them on the strength of the pattern: `ExpressionObserver` uses one to reconstruct a user's own invocation, and `ObservableCoalesceExpression` uses one to build a compiled delegate. Neither adapts a caller's lambda to a shape.

`ObserveToCollection` was rewritten without an arm, which is a departure from the discipline and is recorded as such. The two sites with no instrument at all were left wrapped rather than changed on an analogy.

## Befores for the order-by and group-by sites

Taken on unchanged code, in the same session as the run above.

`ObserveOrderByBenchmarks`:

| arm | 100 | 1,000 | 10,000 |
|--- |---: |---: |---: |
| `Enumerate` | 98.83 ns / 40 B | 817.69 ns / 40 B | 8,221.02 ns / 40 B |
| `KeyChange` | 1,484.43 ns / 813 B | 2,359.17 ns / 816 B | 5,943.67 ns / 816 B |
| `SourceAddAndRemove` | 5,638.37 ns / 9,756 B | 6,512.11 ns / 9,776 B | 13,919.43 ns / 9,926 B |

`ObserveGroupByBenchmarks`:

| arm | 100 | 1,000 | 10,000 |
|--- |---: |---: |---: |
| `Count` | 12.85 ns / 0 B | 12.80 ns / 0 B | 12.81 ns / 0 B |
| `Enumerate` | 23.95 ns / 40 B | 24.41 ns / 40 B | 27.89 ns / 40 B |
| `KeyChange` | 399.27 ns / 724 B | 479.97 ns / 711 B | 1,375.78 ns / 711 B |
| `SourceAddAndRemove` | 4,701.90 ns / 7,821 B | 6,903.16 ns / 7,947 B | 8,255.26 ns / 8,026 B |

`KeyChange` is the arm the rewrite should move, since that is the one which re-evaluates the selector. `Count` and `Enumerate` are controls. Both instruments use `[Params]` on element count, which is not how instruments are built here any more; they predate the convention and were not rewritten for this measurement, because changing an instrument in the same pass as the code it measures destroys the before.

## Still untouched

The 808-byte observed indexer.

## A process failure worth recording

This run had to be built twice because I based an edit on a copy of `ObservableDictionaryQuery.cs` staged two turns earlier, before the per-event split existed, and committing it silently reverted that split. The existing habit says re-stage a file before reasoning from it. That is too weak. The rule is: **a staged copy is a valid base for an edit only if it was staged after the last thing I wrote to that path.** Writing from a stale snapshot is worse than reading one, because it destroys newer work rather than merely misinforming.
