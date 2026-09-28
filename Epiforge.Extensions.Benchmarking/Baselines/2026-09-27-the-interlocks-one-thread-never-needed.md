# The interlocks one thread never needed

*2026-09-27 — `ThreadConfinementBenchmarks` and `ThreadConfinement`, after 7.0.0 let an observer be told that its observations are confined to one thread at a time*

## The option

6.1.1 made every evaluation take two interlocked operations so that a change raised on another thread could not be lost. That is about 5 to 8 ns an evaluation, and on a thousand observations of one object it made a raise 2.5x as expensive. An application whose observations are built, changed and disposed of on one thread, as on a user interface thread, pays that for nothing.

`ExpressionObserverOptions.IsThreadSafe` is `true` by default, so dropping the library into a multithreaded application still gets 6.1.1's guarantees. Set to `false`, the observer changes an observation's evaluation state with a plain compare and write rather than an interlocked one, releases a shared node without taking its lock, and builds the handle it gives back without a compare-exchange. The protocol is otherwise the same, so on one thread an observation declines and reruns re-entry exactly as it does when safe.

`IExpressionObserver` gains `IsThreadSafe` too. An abstract member rather than a default implementation, because a default would assert thread safety on behalf of implementers who never declared it. Package validation reports CP0006 on all five frameworks, which is suppressed, and the version is 7.0.0.

## The arms

Every arm runs under both settings in one run. Four of them are the same shapes as arms in the overnight 6.1.0 run, `BenchmarkRun-20260927-031028`, and the clean 6.1.1 run, `BenchmarkRun-20260927-185141`, which the last two columns quote.

| arm | confined | safe | 6.1.0 | 6.1.1 |
|---|---:|---:|---:|---:|
| `ChangeEveryRankUnobserved` — control | 9.462 μs | 9.253 μs | 8.910 μs | 9.443 μs |
| `ChangeEveryRankFilteredDirect` | **19.930 μs** | 23.643 μs | 17.521 μs | 23.120 μs |
| `ChangeEveryRankFilteredGraph` | **40.908 μs** | 46.782 μs | | |
| `RaiseUnderAThousandObservationsDirect` | **6,093.064 μs** | 12,119.134 μs | 5,084.717 μs | 12,909.737 μs |
| `RaiseUnderAThousandObservationsGraph` | **6,920.745 μs** | 12,285.177 μs | 7,141.074 μs | 12,553.763 μs |
| `ConstructAndDisposeFilteredDirect` | 334.281 μs / 972,752 B | 338.418 μs / 972,752 B | 296.771 μs / 965.58 KB | 314.787 μs / 949.95 KB |
| `ConstructAndDisposeFilteredGraph` | 2,317.893 μs / 2,730,062 B | 2,359.571 μs / 2,725,146 B | | |

The 6.1.0 and 6.1.1 figures come from `NotificationFanOutBenchmarks.SharedWatchedPropertyDefault` and `DistinctWatchedPropertyGraph` and from `DynamicDataComparisonBenchmarks.ChangeEveryRankWithExpressions` and `ConstructAndDisposeWithExpressions`, which observe the same shapes.

**Confinement takes back most of what 6.1.1 cost, and on the graph all of it.**

- A thousand observations of one object on direct subscription cost **6.1 ns each a raise confined against 12.1 safe**, and 5.1 on 6.1.0: 86% of the increment this run measured, and 77% of the one the clean run measured.
- A thousand distinct comparisons on the graph cost 6.9 ns each confined against 12.3 safe, and 7.1 on 6.1.0: all of it.
- A property change in a filtered view of a thousand costs **10.5 ns above the floor confined against 14.4 safe**, and 8.6 on 6.1.0: 68% of this run's increment, 77% of the clean run's.
- The graph's filtered view saves 6.1 ns a change.
- Construction is 1.2% and 1.8% faster confined, both within the error, and allocates the same: to the byte on direct subscription, and within 0.18% on the graph, whose arms drift by that much between runs.

What confinement leaves above 6.1.0, about 1 ns a raise on direct subscription and 1.9 ns a change in a filtered view, is the rest of the protocol: reading the thread's identity, the state checks and the calls around the evaluation, which re-entry on one thread still needs.

## Tests

`ThreadConfinement` has nine rows: the default on the options and the observer, the option reaching the observer, and on both mechanisms a confined observation following changes, faulting and recovering, releasing shared nodes, handling re-entry exactly as a safe one does, and disposing of what it made once each. Throwing in the confined branch fails exactly the seven rows which reach it and nothing else. `ConcurrentChange` runs with the default, since nothing can make threads meet without breaking the promise the option asks for.

## What misuse costs

In the sandbox, with confinement made the default for the experiment only: the suite passed once and failed only the row releasing shared nodes from eight threads once, and the stress report at 20,000 iterations a row on two cores met every wait but left two nodes cached on three of its four rows. The option's remarks and the readme name what it gives up: lost changes, observations evaluated before they are built, and parts of observations left cached.

## Predictions scored

- **Wrong, in the better direction.** Direct fan-out confined at about 8.5 ns: 6.1.
- **Close.** Graph fan-out confined at about 7.5 ms: 6.9.
- **Held.** Filtered view on direct subscription confined at about 11 ns above the floor: 10.5.
- **Wrong.** Most of the graph's filtered increment recovered: 6.1 ns, about half of the 11.6 which 6.1.1 added to the graph's nearest comparable arm.
- **Held.** Construction within 3% on direct subscription, 1.2%, and up to 5% faster on the graph, 1.8%.
- **Held.** Allocation the same under both settings, and the graph within 2%: 0.18%.
- **Missed, narrowly.** The safe rows within 6% of the clean 6.1.1 run: the graph fan-out and the filtered view held at 2.1% and 2.3%, direct fan-out ran 6.1% faster and direct construction 7.5% slower. The comparison the decision rests on is between the two settings within this run, which differ by up to 2x.

## The decision

The rule set before the run was to ship if confinement recovered at least half of 6.1.1's increment on both fan-out arms and the filtered view, and construction was not slower. It recovered 68% to all of it on every one of them, and construction was 1.2% to 1.8% faster, so the option ships.
