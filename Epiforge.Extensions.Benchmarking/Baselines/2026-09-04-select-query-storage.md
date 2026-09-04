# 4 September 2026 — the select query, and a reverted change that deserves a second hearing

`2026-09-04-per-event-observation.md` priced `ObservableDictionarySelectQuery` for the first time and found it in the trap the where query had just been pulled out of. The same fix applies, and it took three quarters of the cost off.

## The measurement

| arm | before | after | time |
|--- |---: |---: |---: |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48,000 B | 9.16 μs |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96,000 B | 65.6 μs |
| `ChangeEveryValueInAWhereQuery` | 96,000 B | 96,000 B | 132.6 μs |
| `ChangeEveryValueInAWhereQueryWithASubscriber` | 256,000 B | 256,000 B | 168.4 μs |
| `ChangeEveryValueInAnAllQuery` | 256,000 B | 256,000 B | 171.9 μs |
| `ChangeEveryValueInASelectQuery` | 1,080,000 B | **256,000 B** | **181.9 μs** (was 367) |
| `ChangeEveryValueInASelectQueryWithASubscriber` | 1,080,000 B | **344,000 B** | **204.6 μs** (was 370) |
| `ReplaceOneKeyWithNoObservation` | 0 B | 0 B | 4.83 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808,000 B | 150.5 μs |

Every arm that should not have moved did not move. The two select arms fell by 76% and separated from each other by 88 bytes — the typed arguments a real subscriber receives. Their being identical was the diagnosis; their differing is the confirmation.

## The fix, and where it differed from the where query's

Same two moves, done in one pass because the base-class hooks already existed: snapshot invalidation out of the forwarding handlers and into the mutation sites, then per-event lazy attachment through `OnChangeObservationBegan` and `OnChangeObservationEnded`.

The select query reaches its storage at five sites rather than the where query's three — the projection add, the projection remove, a `Reset`, and two indexer writes, one for a value changing in place and one for a duplicate-key claimant being promoted. Only the indexer write repeats, so it got a helper and the rest take `DiscardSnapshots()` inline. A wrapper method for a single call site is scaffolding.

Six tests were added, and one covers a gap that predated this work: the only staleness test in the suite covered the where query, so nothing would have caught a stale `Keys`, `Values` or enumeration on the select query's side. It is covered now, for both.

## What the numbers say next, and the honest caveat attached to it

The select query with nothing subscribed now costs **256 bytes** per value change. That is pure observation — the query layer's contribution is zero, as it is for the where query. But the where query's observation costs **96 bytes** for the same thousand values. A select observation costs 160 bytes more than a where observation.

The difference is in how the caller's lambdas are adapted. `ObserveWhere` wraps the caller's predicate in one `Expression.Invoke`. `ObserveSelect` builds its pair selector by wrapping the key selector and the value selector in **two** `Expression.Invoke` nodes and combining them with an `Expression.New`. An invocation node rebuilds the expression beneath it whenever an argument changes, and it also hides everything under it from `DirectSubscriptionAnalyzer`, which has no arm for an invocation.

**This is the hypothesis `LambdaInvocationRewriter` was written to test on 2 September, and it measured exactly nothing** — 664 bytes before, 664 after — so the change was reverted. That measurement was taken when the where query arm was 664 bytes, of which 568 was notification machinery. Whatever the wrapper cost was, it was buried under an order of magnitude of noise that has since been removed. The instrument could not have shown an effect of this size.

That is a reason to re-test, not a reason to believe. The 2 September reasoning failed at a step that was checkable, and the correct response to having been wrong once is a cheaper experiment, not a more confident one. So: apply the rewriter to `ObserveSelect`'s composition alone — one site, not the nine that were rewritten before — and read `ChangeEveryValueInASelectQuery`. If 256 falls toward 96, it generalizes and the nine sites are worth revisiting. If it does not, the hypothesis is dead and should be recorded as dead.

## Still open

`ReplaceOneKeyObservedByAnIndexer` remains 808 bytes against a control of zero, untouched by this entire arc and now the largest unexplained figure anywhere on the dictionary side.
