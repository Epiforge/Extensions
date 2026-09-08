# The handler converted twice for every element

*2026-09-08 — `RefusalPricingBenchmarks`, `ObservationConstructionBenchmarks`, `AllocationShapeBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

A query which observes an expression for each of its elements attached to each observation's change notification by converting a method group — `observableExpression.PropertyChanged += ObservableExpressionPropertyChanged` — and detached by converting it again. A method group conversion of an instance method yields a fresh delegate every time it is evaluated; the compiler caches only static ones. So a filtered query over a thousand elements built and threw away two thousand delegates where one would have served for all of them.

Reading the whole query layer found the pattern in five classes rather than one: `ObservableCollectionWhereQuery`, `ObservableCollectionSelectQuery`, `ObservableDictionaryWhereQuery`, `ObservableDictionarySelectQuery` and `ObservableDictionaryToCollectionQuery`, twenty-five sites between them. Each now holds one handler in a lazily-made field. Nineteen other query classes convert method groups too, and were left alone: their subscriptions are to an inner query, made once per query rather than once per element.

## The prediction, and the measurement

Two delegates per element at 64 bytes each is 128 bytes per element, or 125.0 KB per thousand, on every arm which builds a query and none which does not.

| arm | before | after | fall |
|---|---|---|---|
| `ConditionalDirect` | 1097.93 KB | 973.00 KB | **124.93** |
| `ConstantPredicateDirect` | 777.62 | 652.69 | **124.93** |
| `IndexerReadDirect` | 1254.59 | 1129.66 | **124.93** |
| `NotifyingChainDirect` | 1480.74 | 1355.81 | **124.93** |
| `RankComparisonDirect` | 1097.93 | 973.00 | **124.93** |
| `SharedSourceRepeatedDirect` | 1199.68 | 1074.75 | **124.93** |
| `TwoObjectConditionalDirect` | 1269.90 | 1144.97 | **124.93** |
| `TwoObjectShortCircuitDirect` | 1269.90 | 1144.97 | **124.93** |
| `TwoSourceSumDirect` | 1191.87 | 1066.94 | **124.93** |
| `SourceQueryOnly` | 4.30 | 4.30 | **0** |

Nine arms to the hundredth of a kilobyte against a prediction of 125.0, and the one arm which constructs no observations did not move. The seven graph arms fell 118.7 to 131.2, which is that same figure inside their run-to-run band. `ObservationConstructionBenchmarks` is the other control: it observes without a query, so it has no handler to cache, and its direct arms held at 390.63 and 710.94 exactly.

## Where the floor is now

| term | at the start of 7 September | now |
|---|---|---|
| the observation itself | 424.0 B | 400.0 B |
| the `Where` query's per-element share | 391.9 B | **263.9 B** |
| one subscription to an object distinct per element | 376.0 B | 328.0 B |
| **the floor** | **1191.9 B** | **991.9 B** |

**16.8% off the cost of building a filtered query over a thousand elements**, and the query's own share is down by a third. `ConstantPredicateDirect` checks the split independently: 664.0 bytes per element, which is 400.0 for the observation and 264.0 for the query, with no subscription in it.

## What else the run settled

**A one-element collection expression targeting `IReadOnlyList<T>` allocates 24 bytes, not 32.** `AllocationShapeBenchmarks` puts `new object[1]` and `object?[] x = [e]` both at 32 and `IReadOnlyList<object?> x = [e]` at **24** — the compiler emits a single-element read-only type, one header and one field, rather than an array. That is exactly the 8 bytes by which the arguments-list prediction missed on 7 September, and the account is now closed.

**The same form for two elements costs 64 bytes where an array of two costs 40.** `[a, b]` to an `IReadOnlyList<T>` wraps an array rather than replacing it. The two- and three-argument scoped observations use that form; they are rare, and this is recorded rather than acted on.

**An observation of nothing but the argument costs exactly what an observation of a constant costs.** `ParameterOnlyDirect` and `ConstantDirect` are both 390.63 KB, so the 400 bytes an observation costs before it reads anything is the shell and not the node. On the graph the two differ by 177 bytes per element, the parameter being the more expensive.

**A boxed result costs 24 bytes on the construction path too.** `RankReadDirect` (`person.Rank`, an `int`) is 734.35 KB against `RankComparisonDirect` (`person.Rank > 0`, a `bool`, whose box is shared) at 710.94 — **24.0 bytes per element**, measured directly on the fast path for the first time.

**A deferred group costs 79.9 bytes, confirmed a second way.** `TwoObjectShortCircuitDirect` over the new `NonShortCircuitDirect` — the same two subscriptions written with `&` instead of `&&`, so one defers an operand and the other does not — is 78.03 KB, the same 79.9 bytes per element the earlier subtraction gave. `NonShortCircuitDirect` and `TwoSourceSumDirect` are identical at 1066.94, which is the control: two ways of naming two sources without deferring cost the same.
