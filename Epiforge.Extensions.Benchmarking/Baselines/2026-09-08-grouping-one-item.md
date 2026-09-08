# Grouping one item

*2026-09-08 — `ObserveOrderByBenchmarks`, DefaultJob, MemoryDiagnoser, 100 / 1000 / 10000 elements*

## The instrument was already there

`ObserveOrderByBenchmarks.SourceAddAndRemove` adds one element to the source of an ordered query and removes it again. Nothing had looked at it. It priced one add plus one remove at **6,704 bytes** at a thousand elements — 16x what a key change costs after the morning's work, and the largest per-notification figure in the record. The search for an instrument to write should always begin by reading the instruments that exist.

## What was wrong

Four sites grouped a payload which is almost always one item:

- `ObservableCollectionOrderingComparer.SelectionCollectionChanged`, over `e.OldItems` and again over `e.NewItems`, as `OfType(...).GroupBy(t => t.Item1, t => t.Item2)`.
- `ObservableCollectionOrderByQuery.SourceCollectionChanged`, over `e.OldItems` and again over `e.NewItems`, as `Cast<TElement>().GroupBy(element => element)`.

Grouping exists to handle one element appearing more than once in a single payload, which an add or a remove of one element never does. Each site pays a lazy wrapper, a `Lookup`, its array of groupings, the grouping itself and the array of its one element, plus the `OfType` or `Cast` iterator and the enumerator it takes.

All four now take a single-item path and keep the grouping for payloads which can carry a repeat. The bodies moved into four named methods so that the grouped path and the single path provably do the same thing rather than the same thing written twice. The insert path needs an enumerable to hand `InsertRange`, so the query reuses a one-element buffer it holds and clears after each use, which is safe because every caller holds the query's lock and `ObservableRangeCollection.InsertRange` copies what it is given before it announces anything.

## The measurement

| elements | mean before | mean after | allocated before | allocated after | fall |
|---|---|---|---|---|---|
| 100 | 3,905.27 ns | 3,486.37 ns | 6,710 B | **5,161 B** | **1,549** |
| 1000 | 5,423.01 ns | 4,855.91 ns | 6,704 B | **5,261 B** | **1,443** |
| 10000 | 8,989.15 ns | 8,235.01 ns | 6,860 B | **5,473 B** | **1,387** |

**21.5% of what an add and a remove cost at a thousand elements, and 10.5% of the time.** The prediction recorded before the run was 1,000 to 1,700 bytes, flat across element count; all three fell inside the band.

**The controls are exact.** `KeyChange` is 405, 408 and 408 bytes before and after, identical at all three sizes, which is what it should be: a key change reaches the comparer's replace branch and returns before any of the changed code. `Enumerate` is 40 bytes before and after at all three sizes.

## Where the prediction was loose

"Flat across element count" was approximately right and not exactly right: the saving is 1,549, 1,443 and 1,387 across a hundredfold change in size, a spread of 162 bytes. The grouping machinery is size-independent, so the spread is not in what was removed — it is in what remains. **The residual now grows with element count more visibly than the total did before**: 5,161 to 5,473 across the same range, where the total went 6,710 to 6,860. Taking a constant out of a figure makes whatever is left of its size-dependence a larger share of it. Nothing here says what that component is.

## What the remaining 5,261 bytes are, and what is not known

An add constructs an observation of the wrapped key selector for the new element and a remove disposes it, which by the figures already on record is about 400 bytes for the observation, 320 for its subscription, the query's own per-element share, the `Tuple` at 32 and the key box at 24. `InsertRange` and `RemoveRange` each allocate a `List<T>` of their own before announcing, and each announcement is a `NotifyCollectionChangedEventArgs`. **None of that has been separated by measurement.** This instrument has no unobserved control arm, so the source collection's own share of the figure is unknown, and until it has one the 5,261 cannot be decomposed — only reduced and re-measured.

## The day, on this path

| | one key change | one add and remove |
|---|---|---|
| at the start of 8 September | 816 B | 7,105 B |
| now | **408 B** | **5,261 B** |

**50% and 26%.**
