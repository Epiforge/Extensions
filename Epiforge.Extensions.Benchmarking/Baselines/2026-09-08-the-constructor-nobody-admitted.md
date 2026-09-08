# The constructor nobody admitted

*2026-09-08 — `OrderByMembershipShareBenchmarks` and `ObserveOrderByBenchmarks`, DefaultJob, MemoryDiagnoser*

## What was wrong

`DirectSubscriptionAnalyzer.AnalyzeNode` handled constants, parameters, members, indexes, method calls, binaries, conditionals, type tests and unaries, and dropped everything else through to `UnsupportedExpressionKind`. There was no case for a constructor. `ObservableCollectionOrderByQuery` wraps every key selector as `Expression.New(typeof(Tuple<TElement, IComparable>).GetConstructor(...), parameter, key)`, so **no ordered query had ever used the fast path for the projection it is built on** — nor had any grouped or lookup query, which wrap the same way.

`AnalyzeNew` now follows `AnalyzeMethodCall` but **does not copy its guard**, and that distinction is the change. A method's declared return type is an upper bound, so it must be sealed before a value of it is known not to be disposable; `CannotBeDisposed` tests exactly that. A constructor's value is of the constructed type exactly and never of a type derived from it, so the sealed test does not apply — and must not, because `Tuple<,>` is not sealed and copying the guard verbatim would have refused the very shape this exists for while looking correct. The guard is whether the type implements either disposal interface, now named `ExpressionObserverOptions.IsDisposable`, with `CannotBeDisposed` written in terms of it.

## The layers, before and after

| arm | before | after |
|---|---|---|
| `AddAndRemoveUnobserved` | 16.65 ns / 144 B | 16.42 ns / 144 B |
| `AddAndRemoveWithSourceQuery` | 24.82 ns / 144 B | 25.25 ns / 144 B |
| `AddAndRemoveWithSelectQuery` | 4,179.70 ns / 4,792 B | **568.67 ns / 1,568 B** |
| `AddAndRemoveWithOrderedQuery` | 4,858.80 ns / 5,261 B | **1,586.70 ns / 2,024 B** |

**The projection layer went from 4,648 bytes and 4,155 ns to 1,424 and 543 — 69% of the allocation and 87% of the time.** The ordering layer is 456 bytes against 469, unchanged as it must be, since nothing in it was touched. Both controls held: the collection alone and the observed source over it are the same to the byte.

**One add and one remove: 5,261 bytes to 2,024, 61.5%, and 4,858.80 ns to 1,586.70, 67% faster.**

## The prediction, which held

Recorded before the run: 2,900 to 3,600 bytes off the 5,261, with the projection landing near 1,000 to 1,500 and a large time drop. **Measured 3,237 off, with the projection at 1,424.** Both inside the band. This is the first prediction of the day to come out right in magnitude and direction on a change of this size, after two which were inverted and one which was under.

## What the second instrument added

| arm | before | after |
|---|---|---|
| `SourceAddAndRemove` at 100 | 3,486.37 ns / 5,161 B | 1,067.62 ns / **2,024 B** |
| `SourceAddAndRemove` at 1000 | 4,855.91 ns / 5,261 B | 1,597.22 ns / **2,024 B** |
| `SourceAddAndRemove` at 10000 | 8,235.01 ns / 5,473 B | 3,554.36 ns / **2,024 B** |
| `KeyChange` at 100 | 1,231.22 ns / 405 B | 1,161.86 ns / **357 B** |
| `KeyChange` at 1000 | 2,026.61 ns / 408 B | 1,908.62 ns / **360 B** |
| `KeyChange` at 10000 | 4,811.38 ns / 408 B | 4,161.96 ns / **360 B** |
| `Enumerate` | 40 B at every size | 40 B at every size |

**An add and a remove now cost 2,024 bytes at every element count, exactly.** Before this they were 5,161, 5,261 and 5,473 — and `2026-09-08-grouping-one-item.md` recorded that growth as a residual with size-dependence nobody could name. **It was the graph**, and it is gone. A figure which is now flat across a hundredfold change in size is the strongest statement available that nothing left in this path scales with the collection.

**A key change fell 48 bytes**, to 360, flat. That figure is the same 48 the generic `ObserveOrderBy<TKey>` item claims for the two key boxes, which makes it suspect: **that item must be re-measured before it is believed**, because the string-against-int comparison it rests on was taken while the projection was on the graph. No string-key arm was run today.

## Where the ordered query stands

| | at the start of 8 September | now |
|---|---|---|
| one key change | 816 B | **360 B** |
| one add and one remove | 7,105 B | **2,024 B** |

**56% and 71.5%.** Of the 2,024 that remain, 144 is the collection itself, 0 is observing it, 456 is the ordering machinery, and 1,424 is constructing and disposing one observation — which the construction floor puts near 1,045 for an element of a bulk query, so the projection is now close to what an observation simply costs.

## What has not been measured

`ObserveGroupBy` and `ObserveLookup` wrap their selectors the same way and were refused for the same reason, so they are on the fast path now too. `ObserveGroupByBenchmarks` exists and would show it. Nothing here says by how much.
