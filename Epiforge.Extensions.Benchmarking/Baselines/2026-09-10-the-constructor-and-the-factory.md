# The constructor and the factory

*2026-09-10 — `FormulaShapeBenchmarks`, 1,000 coexisting observations under a filtered query, constructed and disposed*

## The question

A method call whose return value the observer disposes of has been admitted since 9 September and held: made once, kept, disposed once. A constructor of a disposable type was refused outright, so `values.Open(person).Value` ran on the fast path and `new BenchmarkValueQuery(person).Value` ran on the graph — the same value by two roads, at two prices, for no reason either mechanism gave. The refusal has been narrowed to the same invariance gate the call has. These arms price what that was worth, and more importantly say whether the shape is *served* rather than merely *admitted*, which the tests cannot: they count constructions, and a refused shape also constructs once.

## The measurement

Per element, construction and disposal:

| arm | time | allocated |
|---|---:|---:|
| constructed, fast path | **359.1 μs** | **1,188.4 B** |
| the same through a factory method, fast path | 375.5 μs | 1,220.4 B |
| the same through a non-disposable type, fast path | 362.7 μs | 1,196.3 B |
| a bare comparison, fast path — floor | 292.7 μs | 996.4 B |
| constructed, graph | 9,002.7 μs | 7,839.4 B |
| the same through a factory method, graph | 8,561.6 μs | 8,174.8 B |

**25.07x the time and 6.60x the memory**, which is what the asymmetry was costing an expression reaching its value through `new` rather than through a factory.

## Served, not merely admitted

The load-bearing prediction was that the constructed arm would land on the fast path rather than the graph, because admitting a shape and reaching it with a slot are different things and only the second one is worth anything. It landed at 359.1 μs against a graph arm of 9,002.7. Served.

**It also came in cheaper than either shape it was compared against, which was not predicted.** The prediction was "within a few percent of the disposable arm", without a direction; it is 4.4% faster and 32.0 bytes per element lighter than the factory-call shape, and 8.0 bytes lighter than the plain shape which holds nothing at all. A constructor has no invocation target to keep — the factory shape holds a reference to the captured core it calls through and the constructed shape holds none — which accounts for the direction, and was visible in advance to anyone who looked. Nobody looked.

## A floor that moved, and is not explained here

Every fast-path control rose by **exactly 7.81 KB, which is 8.0 bytes per element, on all three of them**:

| control | allocation shift |
|---|---:|
| bare comparison, fast path | +8.0 B/element |
| plain formula, fast path | +8.0 B/element |
| disposable formula, fast path | +8.0 B/element |

Three arms of different shapes moving by the identical amount to the hundredth of a kilobyte is one reference-sized field added to every observation, not drift. `2026-09-09-what-an-invocation-costs.md` records the fast path's floor as 988.4 bytes per element for a bare comparison; **it is now 996.4**, and every figure in this family measured against that floor is 8 bytes per element stale.

The change made this round adds no field, so the cause is earlier and this instrument only just caught it, the previous run of it predating several library changes. The leading candidate is the `evaluating` field added to `ObservableExpression` for the re-entry guard, which `DirectObservableExpression` inherits — **this is a candidate and not a finding**, because whether that field predates the earlier run of this instrument cannot be read from the reports, and the graph controls do not settle it either: two of three moved consistently with eight bytes per node and the third moved 64 bytes per element in the opposite direction.

## Predictions scored

- **Exact on the one that mattered.** The constructed arm is served by the held slot rather than merely admitted by the analyzer.
- **Right, direction unforeseen.** "Within a few percent of the disposable arm" — it is 4.4% below it, and is the cheapest of the three formula shapes rather than the equal of the dearest.
- **Loose.** The constructed graph arm was predicted near the disposable graph arm; it is 5.2% slower and 4.1% lighter, which is at the edge of those arms' deviation rather than inside it.
- **Wrong.** "The six controls do not move." Their timings held within 2.1%, but all three fast-path allocations rose by the same eight bytes per element, which is the most interesting number in the run and was predicted not to exist.

## What has not been measured

Where the eight bytes come from. Whether the constructed shape's advantage over the factory shape survives a target that is not a captured constant. Any depth beyond one construction.
