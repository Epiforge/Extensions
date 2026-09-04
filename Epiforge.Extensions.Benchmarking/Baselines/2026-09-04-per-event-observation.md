# 4 September 2026 — counting per event, and the select query walks into the same trap

`2026-09-04-dictionary-lazy-attachment.md` closed the unsubscribed case and left the subscribed one open: a subscriber to any one of a dictionary query's three change events was charged for all three, because the count was aggregate. It is now per event. A subscriber pays for what it asked for.

## The measurement

Nine arms, `DefaultJob`, a thousand elements. Two arms are new and have no before.

| arm | before | after | time |
|--- |---: |---: |---: |
| `ChangeEveryValueWithNoObservation` | 48,000 B | 48,000 B | 8.46 μs |
| `ChangeEveryValueObservedWithoutAQuery` | 96,000 B | 96,000 B | 66.9 μs |
| `ChangeEveryValueInAWhereQuery` | 96,000 B | 96,000 B | 132.9 μs |
| `ChangeEveryValueInAWhereQueryWithASubscriber` | 664,000 B | **256,000 B** | 166.6 μs (was 263) |
| `ChangeEveryValueInAnAllQuery` | 664,000 B | **256,000 B** | 176.4 μs (was 262) |
| `ChangeEveryValueInASelectQuery` | — | 1,080,000 B | 367.1 μs |
| `ChangeEveryValueInASelectQueryWithASubscriber` | — | 1,080,000 B | 369.6 μs |
| `ReplaceOneKeyWithNoObservation` | 0 B | 0 B | 4.77 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 808,000 B | 808,000 B | 152.7 μs |

A subscribed where query fell from 664 bytes to 256 and from 263 μs to 167. The `All` query, which subscribes to the typed `DictionaryChanged` and nothing else, fell identically — as it should, since it is a where query with one subscriber.

The unsubscribed where query did not move, which is the arm that had to stay still. Nothing about it changed and a shift there would have meant the two paths were entangled.

## What the 568 was made of

The dictionary query layer cost 568 bytes per value change before any of this work. It is now decomposed:

- **408 bytes** were the `NotifyCollectionChangedEventArgs` and the boxed `NotifyDictionaryChangedEventArgs` — including its two `Select` projections over the key-value pairs and the boxing of each key — built for two events that no chained query in the library subscribes to. Gone.
- **160 bytes** are the typed `NotifyDictionaryChangedEventArgs`, which a subscriber to `DictionaryChanged` genuinely asked for and receives.

The premise was checked before the change this time rather than after. `ObservableDictionary.OnChanged` guards each form separately — `if (CollectionChanged is not null)` around the collection arguments, `if (DictionaryChangedBoxed is not null)` around the boxed ones, and the typed `OnDictionaryChanged(e)` unconditionally. Attaching only `result.DictionaryChanged` therefore makes it build only the typed form. Had it built all three regardless, the split would have bought nothing, and reading it first is the difference between this run and `LambdaInvocationRewriter`.

## The shape of the fix

`changeObservers` is gone. Each event's backing field is already its own count — `null` or not — so the base class calls `OnChangeObservationBegan` and `OnChangeObservationEnded` with an `ObservableDictionaryChangeObservation` naming which of the three, and `ObservableDictionaryWhereQuery` attaches exactly the one subscription asked for. Two hooks rather than six virtuals, at the cost of one small internal enum.

## `ObservableDictionarySelectQuery`, priced for the first time

The two new arms cost **1,080,000 bytes and 367 μs**, and they are **identical to each other**. That is the eager-subscription signature, verbatim: a query whose cost does not change when somebody starts listening is a query that was already producing everything for nobody. It is the same reading that identified the where query on 2 September, and this time it is a recognized pattern rather than a discovery.

`ObservableDictionarySelectQuery` has the same shape the where query had — an `ObservableDictionary` for storage, three subscriptions to it taken in `OnInitialization`, and `DiscardSnapshots` called from inside two of the forwarding handlers rather than from the mutation sites. The fix is the one already built: move snapshot invalidation to the mutation sites, then override the two hooks. The base-class machinery is in place, so only the select query changes.

**What this instrument cannot yet say** is how much of the 1,080 bytes is the query layer and how much is the two observations a select query maintains per pair — a key selector and a value selector, against the where query's one predicate. There is no floor arm for that shape. One should be added in the same pass, so the after-run has a control that ought not to move.

## Note on the two remaining figures

`ReplaceOneKeyObservedByAnIndexer` is still 808 bytes against a control of zero and has been untouched by every change in this arc. It is now the largest unexplained figure on the dictionary side.

The typed 160 bytes per notification is real work for a real subscriber and is not obviously reducible, but it has never been looked at.
