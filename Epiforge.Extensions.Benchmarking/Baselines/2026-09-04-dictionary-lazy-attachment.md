# 4 September 2026 — the dictionary side stops talking to itself

`2026-09-02-dictionary-propagation.md` priced the dictionary query layer at 568 bytes per value change and named a suspect. The suspect was right. A value crossing a filtered dictionary query's predicate now costs 96 bytes, which is what the same observation costs with no query around it at all.

## The measurement

Seven arms, `DefaultJob`, a thousand elements. Befores are the figures in `2026-09-02-dictionary-propagation.md`.

| arm | before | after | time before | time after |
|--- |---: |---: |---: |---: |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48,000 B | — | 8.29 μs |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96,000 B | — | 64.16 μs |
| `ChangeEveryValueInAWhereQuery` | 664,000 B | **96,000 B** | 261 μs | **132 μs** |
| `ChangeEveryValueInAWhereQueryWithASubscriber` | 664,000 B | 664,000 B | — | 263 μs |
| `ChangeEveryValueInAnAllQuery` | 664,000 B | 664,000 B | — | 262 μs |
| `ReplaceOneKeyWithNoObservation` | 0 B | 0 B | — | 4.77 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808,000 B | — | 148 μs |

The unsubscribed where query fell from 664 bytes to 96 and from 261 μs to 132. 96 is not a smaller overhead, it is the floor: it is byte-for-byte what `ChangeEveryValueObservedWithoutAQuery` costs, which observes the same predicate against the same thousand values with no query in the picture. The query layer's contribution is now zero.

The subscriber arm is the control that makes this a result rather than a coincidence. Before the change it cost exactly what the unsubscribed arm cost, because the arguments were being built for an audience of nobody either way. It now costs 568 bytes more. **That difference appearing is the finding.**

## What it was

Three things were listening at all times, in two places.

`ScopedObservableDictionaryQuery` — which is what every caller actually holds — subscribed to the inner query's `CollectionChanged`, `DictionaryChanged` and boxed `DictionaryChanged` in its constructor. `ObservableDictionaryWhereQuery` subscribed to the same three events on its inner `ObservableDictionary` storage in `OnInitialization`. Both now attach on the first subscriber and detach on the last.

The base class already had the guard. `ObservableDictionaryQuery.OnChanged` has always wrapped its collection-argument construction in `if (CollectionChanged is not null)` and its boxed-argument construction in `if (DictionaryChangedBoxed is not null)`. Nothing was missing; the scope was standing on the switch. This is the collection side's position before `2026-09-01-notification-guard.md`, and it was found the same way — by measuring the subscriber arm rather than by reading.

The change was two steps and had to be in that order. `ResultCollectionChanged` and `ResultDictionaryChanged` did not only forward, they also called `DiscardSnapshots`. Detaching them would have left `Keys`, `Values` and `GetEnumerator` serving stale snapshots. So snapshot invalidation moved into the query's own mutation sites first, was tested green on its own, and only then did the forwarding become lazy.

`PropertyChanged` and `PropertyChanging` stay eager at both levels. They are raised with cached static arguments, so there is nothing to save and every reason not to put a `Count` notification at risk.

## What is still on the table, and it is most of it

**The `All` query did not move, and neither did the subscriber arm, and both should.**

I predicted the `All` arm would fall with the where arm because it wraps one. It did not, and the reason is worth writing down. `ObservableDictionaryAllQuery` subscribes to the where query's typed `DictionaryChanged` — and to nothing else. The benchmark's subscriber arm subscribes to the typed `DictionaryChanged` — and to nothing else. Neither wants the collection form or the boxed form. Both pay for all three.

That is because the counter is aggregate. `changeObservers` is one integer across all three events, so a subscriber to any one of them makes the where query attach all three handlers to its `ObservableDictionary` storage, and that dictionary then builds all three argument sets on every change. The 568 bytes are its production of arguments that two of the three subscriptions exist only to discard.

The fix is to count per event instead of in aggregate, so that a typed-only subscriber causes only `result.DictionaryChanged` to be attached. The backing field is already the count — `null` or not — so no counter is needed at all; the shape is `ScopedObservableCollectionQuery.CollectionChanged`'s, three times over. The cost is that the two hooks have to say which event they are about.

**`ObservableDictionarySelectQuery` has the same storage-plus-eager-subscription shape and has not been touched.** The base-class hook is general, so extending to it should be small. It has no arm in this instrument yet.

**The observed indexer is still 808 bytes against a control of zero** and is untouched by any of this. It remains the largest single unexplained figure on the dictionary side.

## The habit this run confirms

The subscriber arm was added for one purpose: to distinguish "nothing is subscribed" from "something always is" by measurement rather than argument. It answered that question, and then it answered a second one nobody asked — because it stayed flat when the arm beside it fell by 85%, which is what exposed the aggregate counter. A control that can only confirm is worth less than one that can also disagree.
