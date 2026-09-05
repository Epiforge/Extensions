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

## The after

| arm | before | after | change |
| --- | ---: | ---: | ---: |
| `ReplaceOneKeyObservedByAnIndexer` | 320 B / 60.352 μs | **352 B / 79.805 μs** | +32 B, +32% |
| `ReplaceOneKeyObservedByABoxedSubscriber` | 320 B / 35.311 μs | **320 B / 35.223 μs** | — |
| `ReplaceOneKeyObservedByATypedSubscriber` | 104 B | **104 B** | — |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | 0 B | **0 B** | — |
| `ReplaceOneKeyWithNoObservation` | 0 B | **0 B** | — |

The allocation prediction was **352 bytes and it measured 352**, exactly the single-element `object?[]` that `FastInvoke` needs. The boxed arm did not move, which is the assurance that nothing outside the observation was touched. The gap between the indexer arm and the boxed arm is now **32 bytes**, having been exactly 0, and that gap is the price of the fix.

## The time, which was not predicted and should have been

The indexer arm rose **60.352 → 79.805 μs, a third**. The four arms whose allocation is unchanged moved between −1.7% and +2.1%, so that is real.

**The allocation prediction was built as a count of objects at a unit cost already on record, and it landed exactly. The time was hedged as "some increase" and it was a third.** Both were available to the same method: one redundant evaluation per change is one dictionary lookup, one delegate invocation and one array, and `ChangeEveryValueObservedWithoutAQuery` already prices a whole multi-node re-evaluation at about 65 ns. Nineteen and a half nanoseconds for a single indexer node is what that model predicts, and the model was there to be used. **Hedging is not the same as declining to predict; it just makes the prediction unfalsifiable.**

## Was the fix worth a third of that path

Yes, and the question is not close. The alternative was an observation which silently never updates, against a type whose only signal is the conventional one. *"What matters most is correctness. After that, as fast and light as possible."*

**The cost is inherent to honoring both signals and cannot be narrowed away.** Three attempts, all dead:

- **Ignore the property notification when a structural subscription exists.** Killed by the test: `TestObservableRangeCollection` is an `INotifyCollectionChanged` which announces this particular mutation by property alone.
- **Latch after the first structural notification and ignore property notifications thereafter.** Same type, same objection — a type may signal structurally for some mutations and by property for others.
- **Reorder so the cheap signal wins.** The dictionary raises the property notification *before* the change notification, and nothing at the property notification can know a structural one is coming for the same change.

What is left is to make evaluation itself cheaper, which is a general question and not this one.

## One arm to watch

`ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` moved 35.318 → 37.161 μs, **+5.2%**, on byte-identical allocation. It attaches two empty handlers and holds no observation, so nothing in this change can reach it. It sits outside the ±2% the other unmoved arms establish for this pair of runs, and this suite has already produced one 6.6% excursion which a re-run returned to baseline. Recorded, not explained; check it on the next run rather than assuming either way.

## That arm, settled — and a note of mine corrected

Re-run with nothing changed. `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` reads **35.619 μs**, against 35.207, 35.318, 37.161 before it. It came back; the excursion was a tail sample, as the absence of any mechanism said it would be.

Every arm in the run allocates exactly what it allocated in the run before — all twelve — and the means are within 2.5% throughout.

**This is the second such excursion in this suite in two days, and it was a different arm.** `ChangeEveryValueInAWhereQueryWithASubscriber` did the same thing at 6.6% and returned; this one did it at 5.2% and returned. So the note written after the first — that one particular arm is the noisy one — was **too narrow, and drawn from a single instance**. The correction, which belongs to the suite rather than to any arm:

> **Arms of `DictionaryPropagationBenchmarks` individually excurse by up to about 7% between runs while reporting well under 1% deviation within a run.** Two different arms have done it, each once, each on byte-identical allocation, each returning on the next run. A single-run time movement on any arm of this suite, unaccompanied by an allocation change, decides nothing.

That is a better rule than the one it replaces and it cost one more ninety-second run to earn. It is also a small instance of the failure this document is otherwise about: a note generalized from one observation, written down as though it were a property of the thing observed.
