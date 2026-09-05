# 4 September 2026 — bisecting the observed indexer

`ReplaceOneKeyObservedByAnIndexer` costs **808 bytes per dictionary change** against a control of exactly **0**. It has survived every pass of the dictionary arc untouched and is the largest unexplained figure in the measured surface. This document records the before figures and the prediction ahead of the run which decomposes it.

Nothing in the library has been changed. Only `DictionaryPropagationBenchmarks` has, and only by adding arms; no existing arm was touched.

## The before, from `2026-09-02-dictionary-propagation.md`

These were stable across four consecutive runs. A run overwrites the previous report, so they are restated here.

| arm | allocated | per operation |
| --- | ---: | ---: |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48 B |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96 B |
| `ChangeEveryValueInAWhereQuery` | 664,000 B | 664 B |
| `ChangeEveryValueInAnAllQuery` | 664,000 B | 664 B |
| `ReplaceOneKeyWithNoObservation` | 0 B | 0 |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808 B |

The four `ChangeEveryValue` query arms were subsequently moved by the dictionary work of 4 September and their current figures are in `2026-09-04-dictionary-lazy-attachment.md`, `2026-09-04-per-event-observation.md` and `2026-09-04-select-query-storage.md`. The two `ReplaceOneKey` arms were not touched by any of it: the indexer arm observes `source[watchedKey]` directly and has no query layer.

**The time figures from those runs are not carried forward.** They were fitted on a machine which has since been restaged. Allocation is deterministic and survives; a mean in microseconds does not, and no time comparison in this document may reach across that boundary.

## The path, read

Every `source[0] = x` in the indexer arm goes through `ObservableDictionary`'s indexer setter, which calls `NotifyIndexerChanged()` unconditionally and then, **only if `IsChangeObserved`**, constructs `NotifyDictionaryChangedEventArgs<int, BenchmarkPerson>` for a `Replace` and hands it to `OnChanged`. That guard is what makes the control 0 B.

`ObservableIndexExpression.SubscribeToObjectValueNotifications` attaches two handlers to the dictionary: the **non-generic** `INotifyDictionaryChanged.DictionaryChanged`, whose arguments are `NotifyDictionaryChangedEventArgs<object?, object?>`, and `INotifyPropertyChanged.PropertyChanged`. It does not attach to `CollectionChanged` — that arm of `SubscribeToObjectValueNotifications` is an `else if` — so the `CollectionChanged` branch of `OnChanged` is skipped, and it does not attach to the generic `DictionaryChanged`.

That the subscription is to the **boxed** interface is the fact this bisection turns on. `OnChanged` serves a boxed subscriber by projecting both item lists through `Enumerable.Select` into a second event-arguments object built with `[..sequence]` spreads over sequences whose length is not statically known, boxing the `int` key once on each side.

Two candidates, then, and reading cannot rank them:

1. **the boxed adaptation in `ObservableDictionary.OnChanged`** — two `Select` iterators, two spreads, two boxed keys, a second arguments object;
2. **the handler body in `ObservableIndexExpression.ObjectValueDictionaryChanged`** — a display class capturing the local `key`, two delegates over it which cannot be cached, and two enumerators taken through an interface by `Any` and `FirstOrDefault`.

The handoff nominated (2). **Reading has lost to bisection four times on this path**, so it is measured rather than believed.

Two things reading does settle cheaply, both of which an arm confirms:

- `PropagationScope` is a `readonly ref struct` whose pending list is recycled. It allocates nothing and is not a candidate.
- `ObjectValuePropertyChanged` compares `e.PropertyName` against `indexer?.Name`, which is `"Item"`, and `CommonPropertyChangeNotificationEventArgs.IndexerChanged` carries `"Item[]"`. The names do not match, so on this path the handler body never runs and the `PropertyChanged` subscription should cost nothing at all.

## The instrument

Four arms added to `DictionaryPropagationBenchmarks`, each performing the same thousand replacements of one key, differing only in what is subscribed. `DefaultJob`, no `[Params]`. The two existing `ReplaceOneKey` arms stay in place as the control and as the figure being decomposed, so every subtraction below is between arms of a single run.

| arm | subscribed |
| --- | --- |
| `ReplaceOneKeyWithNoObservation` | nothing — control, must reproduce **0 B** |
| `ReplaceOneKeyObservedByATypedSubscriber` | generic `DictionaryChanged`, empty handler |
| `ReplaceOneKeyObservedByABoxedSubscriber` | non-generic `DictionaryChanged`, empty handler |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | `PropertyChanged`, empty handler |
| `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` | both of the two the observation attaches, empty handlers |
| `ReplaceOneKeyObservedByAnIndexer` | the observation itself — must reproduce **808 B** |

Two of the six replicate figures already on record, which is what licenses the other four.

The subtractions the instrument is built to support, each between arms and each on allocation only:

- **boxed − typed** = what the boxed adaptation in `OnChanged` costs.
- **808 − (boxed and property-changed)** = what `ObservableIndexExpression`'s own code costs, which is the quantity candidate (2) is a claim about.
- **property-changed − control** = 0 if the name mismatch above is real.

## The prediction, stated before the run

I expect candidate (1) to dominate and the handoff's candidate (2) to be the smaller term — roughly two thirds to one third, though the split is the point of measuring and the ratio is a guess, not a figure.

What each outcome looks like:

- **`ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` comes in high**, most of the 808 already spent before any of the observation's own code runs. The suspicion in the handoff is then a minor term, and the work belongs in `ObservableDictionary.OnChanged`, where it would pay for **every** boxed subscriber rather than for this one consumer.
- **It comes in low**, under a third of the 808. The handoff is then right, the display class and the two uncacheable delegates are the money, and the work belongs in `ObjectValueDictionaryChanged`.
- **`boxed` and `typed` agree.** The boxed adaptation is free, both readings above are wrong, and the cost is somewhere neither of them looked. This is the outcome worth wanting: it is the one that teaches something.

If the two `ReplaceOneKey` arms already on record do not reproduce 0 and 808, nothing else in the run may be quoted.

## Separately, a possible defect, not a performance question

`ObservableIndexExpression.ObjectValuePropertyChanged` matches `e.PropertyName` against the indexer's `PropertyInfo.Name`, which is `"Item"`. The .NET convention for notifying that an indexer changed is `"Item[]"`, which is what `ObservableDictionary` itself raises. For `ObservableDictionary` this is harmless, because `DictionaryChanged` covers the same change. For an observed object which has an indexer and announces changes to it only by raising `PropertyChanged("Item[]")` — the convention — the observation would never update.

**This is reading, and reading is what this document just declined to trust.** It is a hypothesis with a cheap test and no benchmark: observe an indexer on a type which raises `PropertyChanged("Item[]")`, change it, and assert the observation follows. If the test fails, it is a defect and predates this work. It is recorded here so it is not lost, and is not part of this pass.

## The result

Every figure on record reproduced. Five of them, not the two the instrument was built to check: the four `ChangeEveryValue` query arms also came back at their post-4-September values, so the restage moved nothing.

| arm | allocated | per operation | mean |
| --- | ---: | ---: | ---: |
| `ReplaceOneKeyWithNoObservation` | 0 B | **0** | 4.818 μs |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | 0 B | **0** | 5.076 μs |
| `ReplaceOneKeyObservedByATypedSubscriber` | 104,000 B | **104 B** | 16.460 μs |
| `ReplaceOneKeyObservedByABoxedSubscriber` | 688,000 B | **688 B** | 92.568 μs |
| `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` | 688,000 B | **688 B** | 96.003 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | **808 B** | 151.941 μs |

The subtractions, on allocation:

- **boxed − typed = 584 B.** What `OnChanged` spends adapting one replacement for a boxed subscriber.
- **808 − 688 = 120 B.** Everything `ObservableIndexExpression`'s own handler costs.
- **property-changed − control = 0 B.** The name mismatch is real and now measured, not read.

So the 808 divides **584 / 104 / 120**: seventy-two percent in the boxed adaptation, thirteen in the typed arguments every subscriber pays for, fifteen in the observation's own code.

The indexer arm's mean of 151.941 μs sits on the ~152 μs recorded before the restage. The concern that the time figures would not survive a re-staged machine was raised in advance and was wrong for this arm. That is worth one line rather than a rule: it says nothing about any other arm, and the allocation figures were never in doubt.

## The prediction, judged

The ranking was right and both magnitudes were wrong.

Candidate (1) was called to dominate at roughly two thirds. It is 83% of the two, and 72% of the whole arm. The estimate for it built from reading — two `Select` iterators, two spreads, two boxed keys, an arguments object, put at 330 to 390 bytes — was low by about two hundred bytes. The estimate for candidate (2), from the same reading, was 232 bytes against a measured 120: high by nearly a factor of two.

**Reading ranked these correctly and priced neither.** That is the fifth time on this path that a decomposition read off the source has come apart against an instrument, and the first time the ranking survived. The handoff's nomination of candidate (2) would have spent a pass on 15% of the arm.

## The change

Two changes, in one pass, because **the instrument separates them**: the boxed-subscriber arm moves only with the first, and the difference between the indexer arm and the boxed arm moves only with the second. Neither can hide inside the other's result.

**One.** `NotifyDictionaryChangedEventArgs<TKey, TValue>` gains an internal `ToBoxed()` which builds each projected list into an array sized from the `Count` it already has, and the four-case `DictionaryChangedBoxed` switch in `ObservableDictionary`, `ObservableConcurrentDictionary` and `ObservableSortedDictionary` — identical in all three — collapses to `OnDictionaryChangedBoxed(e.ToBoxed())`. This removes eight `Enumerable.Select` projections and the grow-and-copy each one forced on the receiving constructor, which cannot know a projected sequence's length in advance.

It does **not** remove the constructor's own defensive copy. `ToBoxed` hands it an array and the constructor copies that array into whatever the collection expression produces. Making it one copy instead of two needs the arguments object to take ownership of an array built for it, which changes what `NewItems` and `OldItems` reference at runtime — and I do not know from here whether `[..items]` against an `IReadOnlyList<T>` target currently produces a bare array or a read-only wrapper. That difference is the whole of the immutability guarantee those properties carry, so it is a question for Daniel and a separate pass, not a guess folded into this one.

**Two.** `ObservableIndexExpression.ObjectValueDictionaryChanged` searches `NewItems` and then `OldItems` with indexed `for` loops instead of `Any` and `FirstOrDefault` over lambdas capturing the local `key`. That removes the display class, the two delegates it could not cache, and the two enumerators taken through the interface.

The search order is now new-items-first with an early return, where the original always scanned `OldItems` first and let a hit in `NewItems` override it. `Any` and `FirstOrDefault` have no side effects, so the outcome is identical for every input; only the work done to reach it changes.

## The prediction for the after run, stated before it

- `ReplaceOneKeyObservedByABoxedSubscriber` falls from **688 B** toward the low 400s. The four allocations removed per replacement are two `Select` iterators and two grow-and-copy builders; the two exact-size arrays and the constructor's two copies remain. A fall to 104 would mean the boxed adaptation had become free, which it has not.
- `ReplaceOneKeyObservedByATypedSubscriber` does **not move from 104 B**. Nothing on the typed path changed. If it moves, something was changed that was not understood.
- `ReplaceOneKeyObservedByAnIndexer` falls by whatever the boxed arm falls by, **plus most of the 120**. The `KeyNotFoundException` string interpolation is on the removal path, which this arm never takes.
- **`ReplaceOneKeyObservedByAnIndexer` minus `ReplaceOneKeyObservedByABoxedSubscriber` is the number that judges change two on its own.** It is 120 now. Under 30 is the second change working.
- The control stays at **0 B**.

Time is not predicted. Four arms of this instrument allocate less after the change, and allocation differences between arms are decomposable while the time they cost is not.

## The after

| arm | before | after | change |
| --- | ---: | ---: | ---: |
| `ReplaceOneKeyWithNoObservation` | 0 B | **0 B** | — |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | 0 B | **0 B** | — |
| `ReplaceOneKeyObservedByATypedSubscriber` | 104 B | **104 B** | — |
| `ReplaceOneKeyObservedByABoxedSubscriber` | 688 B | **560 B** | −128 |
| `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` | 688 B | **560 B** | −128 |
| `ReplaceOneKeyObservedByAnIndexer` | 808 B | **560 B** | −248 |

The observed indexer costs **31% less per dictionary change**, and its mean fell from 151.941 μs to 80.471 μs. The boxed-subscriber arm fell from 92.568 μs to 54.296 μs.

Those time movements are real. Four arms in this run allocate exactly what they allocated before it — the two controls, the property-changed arm and the typed arm — and their means moved by −1.1%, −1.4%, +0.6% and −0.7%. Between-run drift for this pair of runs is therefore inside ±1.5%, and a 47% fall is not in it.

The four `ChangeEveryValue` query arms are byte-identical across the two runs, which is what a change to the boxed path should do to arms that have no boxed subscriber.

## The prediction, judged again

**Change two beat its prediction; change one missed it.**

`ReplaceOneKeyObservedByAnIndexer` minus `ReplaceOneKeyObservedByABoxedSubscriber` was 120 B. It was predicted under 30. It is **exactly 0**: with the loops in place, `ObservableIndexExpression`'s handler allocates nothing whatsoever on this path, and an observed indexer now costs precisely what any boxed subscriber costs and not one byte more.

Change one was predicted to take the boxed arm from 688 into the low 400s. It took it to 560 — a saving of 128 where 250 to 280 was predicted. **The two `Select` iterators and the two grow-and-copies they forced were together worth half what I put on them.** The typed arm did not move at all, as predicted, which is the assurance that nothing was changed that was not understood.

That is now three magnitudes predicted from reading on this path and three missed, in both directions, with the ranking right each time it was tested. The rule the baselines already carry stands and should be stated more sharply: **reading is fit to rank candidates and unfit to price them.**

## The residue, and an account which does not close

The boxed path still costs **560 − 104 = 456 B** per replacement above the typed arguments every subscriber pays for.

What should still be there, per replacement: the two exact-size arrays `ToBoxed` builds, the two copies the constructor makes of them, two boxed `int` keys at 24 each, and the arguments object. For a one-item Replace that is roughly 80 + 80 + 48 + 32 = **240 B against a measured 456**. Two hundred and sixteen bytes are unaccounted for, and by the rule this repository already runs on, **an account which does not close is a finding, not a rounding error.**

The leading explanation, which is reading and is therefore a candidate rather than a fact: the constructor's parameters are typed `IEnumerable<KeyValuePair<TKey, TValue>>`, so `[..actualNewItems]` is lowered for a sequence of statically unknown length — take an enumerator through the interface, grow a buffer, copy it out — **no matter that what it is handed at run time is an array of known length.** `ToBoxed` removed the `Select` iterator in front of that machinery and left the machinery.

This is falsifiable and the next pass tests it. If the second copy is removed and the boxed arm falls to somewhere near 150 to 200, the explanation was right. If it falls only to about 450, it was wrong and the residue is somewhere neither reading has looked.

## What is open here

Removing the constructor's copy means the arguments object taking ownership of an array built for it, which changes what `NewItems` and `OldItems` reference at run time. Three ways to do it, and the choice is Daniel's because it is a question about a public type's guarantees, not about speed:

1. **Assign the array directly.** One allocation per side, nothing else. But `NewItems` would reference an array a consumer can cast back to `KeyValuePair<TKey, TValue>[]` and write through — if the collection expression currently produces a read-only wrapper, this weakens a guarantee the type has been shipping.
2. **Wrap with `ImmutableCollectionsMarshal.AsImmutableArray`.** No copy, and the result is genuinely immutable rather than merely typed as read-only. Costs one boxed `ImmutableArray<T>` per side, around 24 bytes. `emptyList` is already an `ImmutableArray<T>` held in the same field, so this makes the runtime type more consistent, not less. It needs .NET 8 or later, so it needs a guarded fallback for `net6.0` and `net7.0` — and the first entry under "habits that cost sessions real time" is about exactly that kind of guard.
3. **Leave it.** 456 bytes per change on every boxed subscriber of all three dictionaries, in exchange for touching nothing.

Nothing here is worth guessing at. Which of the three depends on what `NewItems` is meant to promise, and that is not written down anywhere I can read.
