# The payload walked with LINQ

*2026-09-08 — `OrderByKeyChangeScalingBenchmarks` and `OrderByKeyChangeShareBenchmarks`, DefaultJob, MemoryDiagnoser*

## What was read

`2026-09-08-what-one-key-change-buys.md` left about 520 bytes per key change with no account, and named it the largest unexplained per-change figure in the library. It was not found by measuring further. It was found by reading the two handlers a key change wakes and noticing that both walk a payload of one item with LINQ.

`ObservableCollectionOrderingComparer.SelectionCollectionChanged` searched its own selections with `FindIndicies(t => ReferenceEquals(t.selection, sender))`. The predicate closes over the sender, so every call allocates a display class and a `Predicate<T>`; `selectionsAndDirections` is a `ReadOnlyCollection<T>`, which misses both of `FindIndicies`'s typed fast paths and takes the iterator, which allocates itself and then an enumerator. Four objects, per key change, for the life of the query, to search a list which is nearly always one selection long.

`ObservableCollectionOrderByQuery.SelectionCollectionChanged` delivered the single changed element through `newItems.OfType<Tuple<TElement, IComparable>>().Select(t => t.Item1)` — two more iterators and the enumerators they take over a payload of one.

Both are now indexed walks. Nothing else changed.

## The scaling arms

| elements | mean before | mean after | allocated before | allocated after |
|---|---|---|---|---|
| 250 | 1.115 μs | 1.035 μs | 775 B | **407 B** |
| 1000 | 1.499 μs | 1.382 μs | 776 B | **408 B** |
| 4000 | 2.131 μs | 2.051 μs | 776 B | **408 B** |

**368 bytes come off every arm, and the figure does not move across a sixteenfold change in element count.** That is what a per-notification allocation looks like, and it is the reason the earlier document's flat-allocation observation was worth writing down.

## The share arms, which are the controls

| arm | mean before | mean after | allocated before | allocated after |
|---|---|---|---|---|
| `FarKeyChange` | 1,471.4 ns | 1,365.6 ns | 776 B | **408 B** |
| `NearKeyChange` | 476.2 ns | 359.4 ns | 616 B | **248 B** |
| `FarKeyChangeWithSubscriber` | 1,485.1 ns | 1,364.5 ns | 776 B | **408 B** |
| `FarKeyChangeOnAStringKey` | 1,667.8 ns | 1,547.5 ns | 728 B | **360 B** |

**368 bytes off all four, identically.** The saving is independent of whether the element moves, of whether anything is subscribed, and of whether the key needed boxing — which is exactly what a cost paid once per notification should be, and it is the strongest evidence in this document that the right thing was removed.

Every decomposition the earlier run established survives untouched:

- **Reordering is still 160 bytes** — 408 less 248, against 776 less 616 before.
- **Reordering is still about a microsecond** — 1,006.2 ns, against 995.2 before.
- **The key is still boxed twice at 48 bytes** — 408 less 360, against 776 less 728 before.
- **Subscribing still costs nothing**, to the byte.

## The prediction, which was wrong

The prediction recorded before the run was 200 to 320 bytes, flat across element count, with a small time saving. Flat was right and the time was right. **The size was under by 48 bytes**: the estimate priced an iterator state machine at about 56 bytes and there are six or seven objects here, not five. At about 72 bytes apiece the arithmetic closes at 384 against a measured 368, which is the model to carry forward. Under-predicting a saving is still a miss.

## What is left

Of the 248 bytes a key change costs when nothing moves, 48 are the `PropertyChanging` and `PropertyChanged` arguments the changed object raises before the library sees anything, and 48 are the two key boxes a generic `ObserveOrderBy` overload would remove. **That leaves 152 bytes with no account**, down from about 520. It is no longer the largest unexplained per-change figure in the library.

The comparer still builds a LINQ lookup over `e.OldItems` and again over `e.NewItems` on the add and remove paths, for payloads which are also usually one item. A key change returns before reaching that code, so nothing here prices it, and it needs an arm of its own before it is touched.
