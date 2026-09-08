# The observation and the query

*2026-09-07 — `ObservationConstructionBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was asked

`RefusalPricingBenchmarks` prices an observation only as part of an `ObserveWhere` query, so the 815.9 bytes per element an observation costs when it subscribes to nothing could belong to the observation, to the query around it, or to both. This class constructs and disposes a thousand observations through `ExpressionObserver.Observe` with no collection query at all, using the same two predicates and the same element type, so the difference is the query.

## The measurement

| arm | mean | allocated | per element |
|---|---|---|---|
| `ConstantDirect` | 53.93 μs | 414.06 KB | **424.0 B** |
| `ConstantGraph` | 457.14 μs | 687.19 KB | **703.6 B** |
| `RankComparisonDirect` | 149.63 μs | 734.38 KB | **752.0 B** |
| `RankComparisonGraph` | 2,289.65 μs | 2722.57 KB | **2787.9 B** |

## The cross-check, which was the point

`RankComparisonDirect` minus `ConstantDirect` is 320.32 KB, which is **328.0 bytes per element** — the cost of a subscription to an object distinct per element, predicted at exactly that figure from an entirely different instrument, where it was the difference between two arms of a `Where` query. A constant which reproduces to the tenth of a byte across two instruments that share no code path but the one being measured is a property of the registry, not an artifact of the query.

## The floor, which now closes

Per element, on the fast path:

| term | figure |
|---|---|
| the observation itself | 424.0 B |
| the `Where` query's per-element share | 391.9 B |
| one subscription to an object distinct per element | 328.0 B |
| **total** | **1143.9 B** |

Measured floor, taking the 4.3 KB wrapper out of `RankComparisonDirect` in `RefusalPricingBenchmarks`: **1143.9 B**. The three terms account for all of it.

## The two mechanisms, with no query in the way

An observation of `person.Rank > 0` costs **752.0 bytes** on the fast path and **2787.9** on the graph, a factor of 3.7 with nothing else in the measurement. Taking each mechanism's own no-subscription arm away, the structure and subscription for that one member read cost **328.0** on the fast path and **2084.3** on the graph — a factor of 6.4. That is the clearest statement of the difference between the mechanisms recorded so far, because neither figure has a collection query or a shared source in it.

## The arithmetic that does not close

The `Where` query's per-element share should be a property of the query, not of the mechanism inside it. It is **391.9 bytes on the fast path and 159.2 on the graph** — the same subtraction, 233 bytes apart. One or both of those numbers is measuring something other than what it is being read as. Candidates, none confirmed: the registries' dictionaries grow to a thousand entries inside a query and to one in this class, where growth is amortized away, and the two registries key on tuples of different width; and an observation held live by a query may be charged for things a short-lived one is not. **This is a finding and neither figure should be used until it is settled.**

## What reading found while the run was in flight

`ScopedObservableExpression<TArgument, TResult>` passes `[argument]` to its base, which allocates an `object?[1]` per observation — 32 bytes, on every observation of every shape, on both mechanisms — solely to serve the public `Arguments` list. The typed subclass already holds the argument in its own `Argument` property, so the list duplicates what is there and is built whether or not anything reads it. Nothing inside the library reads it. Building it on demand would cost nothing where it is not read and preserve its identity where it is, and the class already has a field for it.

## What that change was worth, measured

The list is now made on first request. Every arm which constructs observations fell by **23.44 KB per thousand** and `SourceQueryOnly`, which constructs none, did not move at all — seventeen arms across two classes and both mechanisms, moving by one identical figure.

| arm | before | after |
|---|---|---|
| `RankComparisonDirect`, in a query | 1121.37 KB | 1097.93 KB |
| `ConstantPredicateDirect` | 801.05 | 777.62 |
| `NotifyingChainDirect` | 1504.18 | 1480.74 |
| `RankComparisonGraph`, in a query | 2882.28 | 2857.61 |
| `ConstantDirect`, no query | 414.06 | 390.63 |
| `RankComparisonDirect`, no query | 734.38 | 710.94 |
| `SourceQueryOnly` | 4.30 | 4.30 |

The graph arms fell between 23.42 and 27.19, which is the same figure inside their known 2.5 KB of run-to-run variation.

**The shape was exactly right and the size was wrong.** The prediction was 32 bytes per observation, being the size of a one-element array of references on this platform. The measurement is **24.0**. The change removed exactly one allocation per observation and added none, so whatever `[argument]` compiled to for an `IReadOnlyList<object?>` target was not a plain `object?[1]`. That is 8 bytes unaccounted for on a term whose whole size was predicted, and the same collection-expression form is used elsewhere in this library, so it is worth knowing what it produces before it is priced again.

## The decomposition after the change

| term | before | after |
|---|---|---|
| the observation itself | 424.0 B | **400.0 B** |
| the `Where` query's per-element share | 391.9 B | 391.9 B |
| one subscription to a distinct object | 328.0 B | 328.0 B |
| **total** | 1143.9 B | **1119.9 B** |

Measured floor after the change: **1119.9 B**. The distinct-source constant is now confirmed a third time, unchanged at 328.0, and the query share is unchanged at 391.9 — both as they should be, since neither term was touched.

## The discrepancy, settled

The 233 bytes were an artifact of the comparison, not a defect. Four arms were added which construct every observation before disposing any — which is what a query does — against the four which dispose as they go.

| arm | disposed as made | held alive |
|---|---|---|
| `ConstantDirect` | 390.63 KB | **390.63 KB** |
| `RankComparisonDirect` | 710.94 KB | **710.94 KB** |
| `ConstantGraph` | 663.13 KB | **434.62 KB** |
| `RankComparisonGraph` | 2698.51 KB | **2474.37 KB** |

**Holding costs the fast path nothing, to the byte, on both shapes.** That was the prediction and it landed on both arms rather than the one it was made for.

**The graph is cheaper held, by about 226 KB per thousand.** The reason is node sharing: the literal in `person.Rank > 0` is one `ConstantExpression` in the lambda every observation is built from, so the observer caches one node for it and a thousand living observations share it. Disposing each observation before making the next destroys that node and builds it again, a thousand times. The standalone loop was denying the graph the thing its cache exists for, and the query was not.

So the graph's query share has to be read against the held arm, not the other: 2921.8 minus 2533.8 is **388.0 bytes per element**, against the fast path's **391.9**. Two mechanisms, 3.9 bytes apart, on a term which ought to belong to the query alone. **The `Where` query costs about 390 bytes per element whichever mechanism is inside it**, and the earlier 233-byte gap was the measurement, not the library.

## What that corrects

An observation of `person.Rank > 0` on the graph costs **2533.8 bytes per element** where it coexists with others, not the 2787.9 recorded above, which is the figure for the unnatural case. The mechanisms compared on that footing: **728.0 against 2533.8**, and with each mechanism's own no-subscription arm removed, **328.0 against 2088.8** for the structure and subscription of one member read.

## What the instrument cannot see

Both observers are made once in `GlobalSetup` and live for every iteration, so their registries and caches reach their working size during warmup and are never grown again inside a measured operation. **Nothing in these figures includes the one-time growth of a dictionary to a thousand entries**, on either mechanism. That is the right choice for measuring steady state, and it means these numbers cannot be used to argue about table growth at all.
