# What one key change buys

*2026-09-08 — `OrderByKeyChangeShareBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## The measurement

Four arms, each varying one thing against the same move. The keys are ten apart so that a change of one provably cannot reorder anything.

| arm | mean | allocated |
|---|---|---|
| `FarKeyChange` — one element crosses the collection | 1,471.4 ns | **776 B** |
| `NearKeyChange` — the key changes, nothing moves | 476.2 ns | **616 B** |
| `FarKeyChangeWithSubscriber` — the same move, something listening | 1,485.1 ns | **776 B** |
| `FarKeyChangeOnAStringKey` — the same move, key already a reference | 1,667.8 ns | **728 B** |

## What it separates

**Reordering costs 160 bytes and 995 nanoseconds.** Far over near. Two thirds of the time of a key change is the move itself, and only a fifth of the allocation.

**Subscribing costs nothing — 776 bytes either way, to the byte.** This is the finding. The record says a collection query "costs the query nothing where nothing is subscribed, and exactly the 72 bytes of a `NotifyCollectionChangedEventArgs` where something is". **That does not hold for the ordered query.** Attaching a handler to its `CollectionChanged` changes its allocation by zero, which leaves two possibilities: it builds its notification whether or not anyone wants it, or it builds none at all for this change. The first is the same species as the thirty-seven construction sites already recorded — an event made for nobody — and `ObservableCollectionOrderByQuery` was not among them. The second would be a defect of a different kind. **Which one it is has not been established here and should be read before anything is proposed.**

**The key is boxed twice, at 48 bytes.** A `string` key, which is already an `IComparable` and needs no box, costs 48 bytes less than an `int` key on the identical move. Not 24 — 48. So a key change boxes its key twice, once more than the single box the signature obviously forces. The string arm is slower, at 1,667.8 ns against 1,471.4, which is string comparison against integer comparison and is not interesting.

## What is left unexplained

Of the 616 bytes a key change costs when nothing moves, 48 are the `PropertyChanging` and `PropertyChanged` arguments the changed object raises before the library sees anything, and 48 are the two key boxes. **That leaves about 520 bytes for updating one key in an ordered query without reordering it**, which is larger than everything else in this document put together and has no account at all.

## The two candidates this raises

**A generic `ObserveOrderBy` overload constrained to `IComparable<TKey>`** would remove 48 bytes from every key change of every ordered query over a value-type key. It is a public API addition.

**Whatever the ordered query produces unconditionally** — established here only as costing the same subscribed and unsubscribed — is worth reading for, because a notification built for nobody is a defect this library has already found and fixed in three other places.

Both are in `ObservableCollectionOrderByQuery`, and neither is priced beyond what is above.
