# 2 September 2026 — pricing the dictionary side, and a change reverted

The dictionary queries had never been benchmarked on their propagation path. `DictionaryPropagationBenchmarks` now prices it, and the numbers are an order of magnitude worse than the collection side. A change made on the strength of a hypothesis about why was reverted, because the hypothesis was wrong and the change bought nothing measurable.

## The instrument

Six arms, `DefaultJob`, a thousand elements. Two control-and-measurement pairs, plus one arm which strips the query away entirely.

| arm | allocated | per operation |
|--- |---: |---: |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48 B, the value's own two `EventArgs` |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96 B |
| `ChangeEveryValueInAWhereQuery` | 664,000 B | 664 B |
| `ChangeEveryValueInAnAllQuery` | 664,000 B | 664 B |
| `ReplaceOneKeyWithNoObservation` | **0 B** | 0 |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808 B |

Three things are established by this and should not need measuring again.

**`ObservableDictionary` allocates nothing when nothing is subscribed.** The `ReplaceOneKey` control performs a thousand replacements for zero bytes, so the dictionary already guards its event-argument construction. Any cost on this path is a consumer's.

**Observing a predicate per key-value pair costs 48 bytes above the floor**, which is exactly the two boxed integers the model predicts for `pair.Value.Rank` and `& 1`, the boolean being a shared box. The expression machinery is not the problem.

**The dictionary query layer adds 568 bytes per value change.**

An observed indexer costs a further 808 bytes per dictionary change, against a control of zero. That is a separate figure and is not diagnosed here.

For comparison, the equivalent collection query costs **nothing at all** per crossing as of `2026-09-02-the-thirty-two-bytes.md`.

**A warning about the `All` arm, which misled this document's first draft.** `ObservableDictionaryAllQuery.OnInitialization` builds a `Where` query and wraps it: `where = observableDictionaryQuery.ObserveWhere(Predicate)`. The two query arms are therefore **not two implementations which happen to agree** — one calls the other, and their byte-identical figures carry no information about a shared mechanism. Anything inferred from that identity is worthless. In particular, the inference that the `Where` query's inner `ObservableDictionary` storage is innocent because a scalar query without storage costs the same **does not hold**: the scalar query has that storage too, one layer down.

## The prediction, which was wrong, and the change which was reverted


The hypothesis: every dictionary query adapts the caller's `(key, value) => …` lambda to a key-value-pair lambda by wrapping it in `Expression.Invoke`, which puts an `ObservableInvocationExpression` in every per-element observation — a node which disposes and rebuilds its whole inner expression whenever an argument changes — and which also hides the expression from `DirectSubscriptionAnalyzer`, since the analyzer has no arm for an invocation and refuses everything wrapped in one.

The wrapper is real, at nine sites across five files, and the analyzer consequence is real. `LambdaInvocationRewriter` was written to substitute the lambda's parameters with the argument expressions at construction instead. Predicted: the two query arms fall from 664 bytes to about 96.

**Measured: 664 bytes, byte for byte unchanged.** Every arm moved 9–12% on time, including the bare control which cannot move, so that is drift and not an effect.

**The reasoning failed at a step which could have been checked before any code was written.** `ObserveWhere` does wrap. `ObserveAll` does not — it passes the caller's predicate straight through, and its code was on screen in the same output which established the wrapper existed. Two queries costing an identical 568 bytes cannot both be paying for a wrapper only one of them has. The identity was the clue and it was read as confirmation.

The change was correct — the full suite passed on every target, including `DifferentialFuzz` and `SubscriptionAgreement` — but it altered which mechanism eight query types use, for no measured benefit, and was reverted on that ground. It can return if evidence ever shows a shape that needs it.

## What to try next, and what not to

Not another hypothesis from reading. This path has now defeated reading four times in one day, and the technique which worked each time was bisection.

The live candidate is the one this document's first draft wrongly dismissed. `ObservableDictionaryWhereQuery` keeps an `ObservableDictionary` as its storage and subscribes to five of its events in `OnInitialization` to learn what it changed and forward it; and `ScopedObservableDictionaryQuery`, which is what every caller actually holds, subscribes to the inner query's `PropertyChanged`, `CollectionChanged` and `DictionaryChanged` in its constructor. Between them, something is always listening at every level, so nothing is ever guarded — which is the collection side's position before `2026-09-01-notification-guard.md`, verbatim: *"it is not that nothing is subscribed, it is that something always is."*

`ChangeEveryValueInAWhereQueryWithASubscriber` tests that by measurement rather than by argument. On the collection side a subscriber costs 72 bytes per flip more than no subscriber, because the scope there attaches lazily. If the dictionary scope attaches eagerly, adding a real subscriber should cost **nothing at all**, because the arguments are already being built for a scope nobody is listening to. Equal figures confirm the diagnosis; a rise refutes it.

Whichever way it lands, the snapshot trap noted below applies to any fix. `ResultCollectionChanged` and `ResultDictionaryChanged` do not only forward — they call `DiscardSnapshots`. Detaching them when nothing is subscribed would leave `Keys`, `Values` and `GetEnumerator` serving stale snapshots. Snapshot invalidation has to move to the query's own mutation sites first; only then is lazy forwarding safe.
