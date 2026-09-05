# 5 September 2026 — the indexer nobody told

A defect, found by a test written to settle a question the performance work raised in passing. It is a correctness finding first; the measurement is only of what fixing it costs.

## What was wrong

`ObservableIndexExpression` subscribes to its object's `PropertyChanged` and re-evaluates when the changed property is the indexer. It decided that by comparing `e.PropertyName` against the indexer's `PropertyInfo.Name`, which for an ordinary C# indexer is `"Item"`.

**.NET announces that an indexer changed as `"Item[]"`** — the indexer's name followed by empty brackets. That is what `ObservableRangeCollection`, `ObservableDictionary`, `ObservableSortedDictionary` and `ObservableConcurrentDictionary` all raise, in this library. The names never matched, so the handler body never ran.

Nothing broke visibly, because every type in this library that raises the indexer notification also raises a collection or dictionary change notification carrying the same change, and `ObservableIndexExpression` subscribes to those too. The failure was reserved for an observed object whose only signal is the conventional property notification: **its observation silently never updated.**

## Why it survived

The path was not untested. `ObservableIndexExpression.ObjectValueChanges` has covered it since the file was written: it mutates a collection without raising `CollectionChanged` and asserts the observation follows.

Its helper raises `new PropertyChangedEventArgs("Item")`.

**The test was written to the implementation, not to the convention**, and it passes for the one spelling that works. That is the whole reason three sessions of reading over this file did not find it, and it is worth more than the defect: a test which agrees with the code it tests proves the code is self-consistent and nothing else. When a test supplies the input that the code under test is checking for, ask where that input's shape actually comes from.

## The finding, before the fix

`ObjectValueChangesNotifiedByTheIndexerConvention` is `ObjectValueChanges` with one difference — the notification carries `"Item[]"`. Predicted to fail; it failed, with the observation still reading its original value after the change.

## The fix, and the choice inside it

The indexer's conventional name is cached at initialization as `Name + "[]"` — a field rather than a per-notification concatenation, and derived from the indexer's own name rather than hard-coded, since `[IndexerName]` can rename it.

**The narrower fix was rejected on a test, not on taste.** Accepting the conventional name only where `PropertyChanged` is the *sole* subscription would avoid all cost on the dictionary path — but the failing test's collection is an `INotifyCollectionChanged` which announces this particular change by property alone, so the narrow fix leaves it failing. Worse, it would make the library honor `"Item"` always and `"Item[]"` only sometimes, which is a stranger contract than either. Correctness over speed decides it: both names are accepted the same way.

**The cost is one redundant evaluation per change on a type that signals both ways**, and it cannot produce a redundant *notification*: `ObservableExpression`'s `Evaluation` setter compares fault and result before assigning, and `ScopedObservableExpression.RaiseIfEvaluationChanged` compares again. That was read before the fix was written, because it is the one fact the change stands on.

## The prediction, before the run

An observed indexer over an `ObservableDictionary` now evaluates on the property notification, which the dictionary raises *before* its change notification, and then finds nothing to do on the change notification.

- **`ReplaceOneKeyObservedByAnIndexer` rises from 320 B to about 352.** The redundant evaluation calls the get-method through `FastInvoke`, whose argument array is a single-element `object?[]` at 32 bytes. Nothing else on that path allocates.
- **`ReplaceOneKeyObservedByABoxedSubscriber` stays at exactly 320 B.** It has no observation, so nothing changed can reach it.
- **The gap between those two arms becomes about 32 bytes**, having been exactly 0 since the indexer's handler stopped allocating. That gap is the price of the fix, stated as a number rather than as a reassurance.
- The typed arm stays at 104, and the two zero arms stay at zero.

If the indexer arm rises by materially more than 32, the redundant evaluation is doing something beyond one argument array and wants looking at before this is called finished.

## Left open by this

`ObservableExpression.EvaluationResults` allocates a fresh `object?[]` on **every** evaluation of every method call, indexer, invocation, constructor and initializer — not only on the redundant one this fix introduces. Caching it per node is the obvious next question and is not obviously safe, because an observation can be re-entered during propagation. It is a bigger fish than the 32 bytes measured here and should be priced on its own instrument.
