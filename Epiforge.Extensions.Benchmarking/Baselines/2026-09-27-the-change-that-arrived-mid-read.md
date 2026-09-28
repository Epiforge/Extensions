# The change that arrived mid-read

*2026-09-27 — `ConcurrentChange`, `TraceFailures`, `ConcurrencyStressReport` and six benchmark classes, before and after 6.1.1 made observation safe against changes raised on other threads*

## The report

GS5's `ViewTests.CoreManipulationAsync` hung about once in fifteen runs under load, stuck in `ConditionAsync` after a `Reset` raised on a thread-pool thread while the wait was being set up, with the condition true by the time it timed out. GS5 reproduced it with Extensions alone on both settings of `UseDirectSubscription`, and asked whether a change raised on another thread was meant to be supported. The readme already said evaluation happens on whatever thread raised the change, so it was: the promise had never been tested.

`ConditionAsync`, like every `Observe` without an argument, builds the graph whatever `UseDirectSubscription` says, so both of GS5's rows measured the graph. The fast path failed the same way through an observation taking an argument.

## What was wrong

Each row of `ConcurrentChange` holds a read, a call or a disposal part way through on one thread until the test has acted on another, so every failure below happened on every run of it.

- **A change lost during construction.** A source changed after the observation subscribed to it but after its first read: the handler's evaluation on the changing thread wrote the new value and the builder's then wrote the old one over it. On the graph a handler called `Evaluate` with no guard at all, and a dependency's request was turned away by `EvaluateOnce` because the builder was evaluating, a refusal whose remark had argued it safe on the assumption that both were on one thread.
- **A change lost during another change's evaluation**, for the same two reasons.
- **A node evaluated before it was built.** A node subscribed to its first operand before it had built the others, and an announcement from another thread evaluated it half-built: `ArgumentException` from an indexer with no arguments yet, and `NullReferenceException` from a binary node with no delegate yet, whose null result was compared as a value type. Those are GS5's two faults.
- **Releases lost.** A disposal ignores a call made while another is under way, so of two observations sharing a node released at once, one release did nothing and the node stayed cached and attached. The same defect was fixed for queries on 23 September.
- **What an evaluation made, disposed of wrongly.** An observation disposed of while another thread evaluated it had its value disposed of twice and the one that evaluation made never.
- **A logger's fault became the observation's.** GS5's logger threw while formatting a list another thread was changing, inside `Evaluate`'s catch.

One more was found by the stress run and has no row, because nothing public can hold it: the observation handed back read the node's evaluation before subscribing to it, so a change between the two was never reported to it.

## The change

- A node's evaluations are made one at a time. A request from another thread while one is under way is never refused: it is recorded, and the thread evaluating evaluates again before it lets go, so the last evaluation always begins after the last change it was told of. A request from the same thread is still declined, as it was, except from a source announcing during the read, which is evaluated after it rather than inside it. A node being built holds its evaluation the same way, so an announcement from any thread becomes one evaluation once it is built. Source handlers and the fast path's change handler go through the same door.
- An evaluation is held as one reference, the result itself or the fault wrapped, so no thread reads a fault with another evaluation's result.
- The observation handed back subscribes before it reads, and keeps whichever read came last.
- A shared node's releases are made one at a time. An observation made directly is never shared and skips it.
- A node's last release tears it down at once unless another thread is evaluating it, in which case that thread tears it down when it lets go. Waiting instead would deadlock a handler marshalling to a user interface thread which is disposing of the observation.
- An invocation rebuilds its body only inside its own evaluation, and subscribes to its arguments before building it.
- The observer writes its traces through a logger which cannot throw.

Nothing public changed, so this is 6.1.1.

## Tests

| | before | after |
|---|---:|---:|
| Expressions, each of five frameworks | 903 of 915 | **915 of 915** |
| Collections, each of five | 647 of 647 | 647 of 647 |
| Components, each of five | 194 of 194 | 194 of 194 |

The twelve which failed before are exactly the new rows: ten of `ConcurrentChange` and both of `TraceFailures`.

## Stress

`--stress`, 200,000 waits a row, eight at a time, 24 logical processors. A wait is unmet only if it has not completed two seconds after the reset finished, polled rather than awaited.

| waiting through | `UseDirectSubscription` | before: unmet / faults / cached | after |
|---|---|---|---|
| `ConditionAsync` | false | 11 / ArgumentException 56, ArgumentOutOfRangeException 56, NullReferenceException 124 / 2 | **0 / none / 0** |
| `ConditionAsync` | true | 6 / ArgumentException 68, ArgumentOutOfRangeException 26, NullReferenceException 57 / 2 | **0 / none / 0** |
| an observation with an argument | false | 4 / ArgumentException 134, ArgumentOutOfRangeException 15, NullReferenceException 68 / 2 | **0 / none / 0** |
| an observation with an argument | true | 2 / ArgumentOutOfRangeException 61 / 0 | **0 / none / 0** |

A zero here is an absence of evidence, not a proof; the tests are the proof. The `ArgumentOutOfRangeException`s are reads of `ObservableRangeCollection` in the middle of a reset on another thread, which is a race in the source rather than in the observer, and they were predicted to remain. **None appeared.** The likely account, not separately measured, is that a read which faults while an observation is being built is now read again before `Observe` returns, because the reset's notification arrives while the builder still holds the evaluation. A reset which lands after construction has finished can still fault an observation until its notification arrives, which the readme now says.

## What it costs

The times below come from the run after the change, `BenchmarkRun-20260927-185141`, against the overnight run of 6.1.0, `BenchmarkRun-20260927-031028`, under the same job; the unobserved control arms agree between the two to within 6%. The before run taken in the afternoon is not used for time: its control arms ran 7% to 19% slower than both, and the after run which followed it had another program competing for the machine.

Every evaluation now takes two interlocked operations, where the graph took one and a source's change on the fast path none. That is about 5 to 8 ns an evaluation:

- a filtered view of a thousand costs **13.7 ns a property change against 8.6**, above the floor, and DynamicData's cache 206.2 ns, so the lead falls from 23x to 15x; at ten thousand, 15.9 ns against 9.3, and at a hundred thousand 65.0 against 59.7;
- a thousand observations of one object cost **12.9 ns each a raise against 5.1** on the fast path, its worst shape, which is the arm where the evaluation is the whole of the cost;
- the graph's filtered change costs 11.6 ns more, and its fan-out arms 5.4 ns more a node;
- a change which moves an element between groups or within a sorted view costs within 2% of what it did, and within 5% where the view is read afterward, because the query's own work dwarfs the evaluation.

Construction on the fast path moved from 3% faster to 19% slower, most arms 2% to 8% slower; the three slowed most are the smallest observations, `ConstantDirect`, `ConstantDirectHeld` and `ParameterOnlyDirect` at 14% to 19%, where the fixed cost of building is the largest share. On the graph it moved from 11% faster to 20% slower. Construction is 16 bytes lighter an observation on the fast path, exactly, and 8 bytes a node on the graph. A faulted evaluation allocates 24 bytes: `ToggleBodyFaultingDirect` rose by exactly 24,000 bytes, and the graph's by 48,000, two faulting nodes a toggle. A live filtered view of a thousand retains 919.1 bytes an element against 935.1.
### DynamicDataComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeEveryRankUnobserved` | 8.910 μs / 46.88 KB | 9.443 μs / 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache` | 206.669 μs / 640.81 KB | 215.649 μs / 640.81 KB |
| `ChangeEveryRankWithDynamicDataList` | 30,113.799 μs / 134382.67 KB | 31,526.100 μs / 134382.67 KB |
| `ChangeEveryRankWithExpressions` | 17.521 μs / 46.88 KB | 23.120 μs / 46.88 KB |
| `ConstructAndDisposeWithDynamicDataCache` | 2,315.170 μs / 4118.33 KB | 2,419.071 μs / 4119.8 KB |
| `ConstructAndDisposeWithDynamicDataList` | 2,006.050 μs / 3742 KB | 2,183.024 μs / 3741.22 KB |
| `ConstructAndDisposeWithExpressions` | 296.771 μs / 965.58 KB | 314.787 μs / 949.95 KB |
| `ConstructAndDisposeWithExpressionsFromAFreshPredicate` | 383.278 μs / 973.86 KB | 376.282 μs / 958.24 KB |

### GraphEvaluation

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `FlipEveryNameObservedAsAMemberComparison` | 57.624 μs / 70.31 KB | 69.235 μs / 70.31 KB |
| `FlipEveryNameObservedAsAMethodCall` | 40.934 μs / 70.31 KB | 47.618 μs / 70.31 KB |
| `FlipEveryNameWithNoObservation` | 9.248 μs / 46.88 KB | 8.991 μs / 46.88 KB |

### GroupedComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeEveryRankUnobserved` | 9.010 μs / 46.88 KB | 9.058 μs / 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache` | 628.162 μs / 1937.5 KB | 642.579 μs / 1937.5 KB |
| `ChangeEveryRankWithExpressions` | 249.787 μs / 625 KB | 252.573 μs / 625 KB |
| `ConstructAndDisposeWithDynamicDataCache` | 2,342.613 μs / 4116.33 KB | 2,559.013 μs / 4116.61 KB |
| `ConstructAndDisposeWithExpressions` | 361.666 μs / 1171.5 KB | 374.241 μs / 1155.88 KB |

### GroupedScaleComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeAThousandRanksUnobserved 1000` | 9.101 μs / 46.88 KB | 9.119 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 1000` | 630.885 μs / 1937.5 KB | 633.846 μs / 1937.5 KB |
| `ChangeAThousandRanksWithExpressions 1000` | 247.780 μs / 625 KB | 252.476 μs / 625 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 1000` | 641.044 μs / 1937.5 KB | 634.280 μs / 1937.5 KB |
| `ChangeAThousandRanksThenReadWithExpressions 1000` | 250.046 μs / 627 KB | 253.419 μs / 627 KB |
| `ChangeAThousandRanksUnobserved 4000` | 9.268 μs / 46.88 KB | 10.388 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 4000` | 646.313 μs / 1984.45 KB | 645.258 μs / 1984.45 KB |
| `ChangeAThousandRanksWithExpressions 4000` | 402.930 μs / 635.41 KB | 399.861 μs / 635.41 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 4000` | 652.528 μs / 1984.45 KB | 640.322 μs / 1984.45 KB |
| `ChangeAThousandRanksThenReadWithExpressions 4000` | 399.288 μs / 637.59 KB | 420.020 μs / 637.59 KB |
| `ChangeAThousandRanksUnobserved 10000` | 9.253 μs / 46.88 KB | 9.327 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 10000` | 637.659 μs / 1937.5 KB | 658.446 μs / 1937.5 KB |
| `ChangeAThousandRanksWithExpressions 10000` | 756.849 μs / 625 KB | 753.769 μs / 625 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 10000` | 643.788 μs / 1937.5 KB | 689.466 μs / 1937.5 KB |
| `ChangeAThousandRanksThenReadWithExpressions 10000` | 756.735 μs / 627 KB | 777.518 μs / 627 KB |

### NotificationFanOut

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `SharedWatchedPropertyDefault` | 5,084.717 μs / 0 B | 12,909.737 μs / 0 B |
| `SharedWatchedPropertyGraph` | 25.732 μs / 24000 B | 37.180 μs / 24000 B |
| `SharedUnwatchedPropertyDefault` | 3,625.825 μs / 0 B | 3,561.741 μs / 0 B |
| `SharedUnwatchedPropertyGraph` | 6.256 μs / 0 B | 6.272 μs / 0 B |
| `SharedMultiRaiseDefault` | 8,999.900 μs / 0 B | 16,931.794 μs / 0 B |
| `SharedMultiRaiseGraph` | 34.513 μs / 24000 B | 45.602 μs / 24000 B |
| `DistinctWatchedPropertyDefault` | 10,406.802 μs / 0 B | 16,956.204 μs / 0 B |
| `DistinctWatchedPropertyGraph` | 7,141.074 μs / 24000 B | 12,553.763 μs / 24000 B |
| `DistinctUnwatchedPropertyDefault` | 3,508.999 μs / 0 B | 3,449.488 μs / 0 B |
| `DistinctUnwatchedPropertyGraph` | 6.335 μs / 0 B | 6.393 μs / 0 B |
| `DistinctMultiRaiseDefault` | 14,221.752 μs / 0 B | 20,922.758 μs / 0 B |
| `DistinctMultiRaiseGraph` | 7,206.814 μs / 24000 B | 12,694.726 μs / 24000 B |

### ObservationConstruction

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ConstantDirect` | 58.61 μs / 398.44 KB | 67.45 μs / 382.81 KB |
| `ConstantDirectHeld` | 67.04 μs / 398.44 KB | 76.17 μs / 382.81 KB |
| `ConstantGraph` | 283.16 μs / 671.3 KB | 339.76 μs / 646.67 KB |
| `ConstantGraphHeld` | 275.78 μs / 435.35 KB | 286.45 μs / 426.55 KB |
| `ParameterOnlyDirect` | 52.75 μs / 398.44 KB | 62.55 μs / 382.81 KB |
| `ParameterOnlyGraph` | 627.24 μs / 844.25 KB | 661.09 μs / 818.99 KB |
| `RankComparisonDirect` | 152.77 μs / 710.94 KB | 164.78 μs / 695.31 KB |
| `RankComparisonDirectHeld` | 184.23 μs / 710.94 KB | 190.04 μs / 695.31 KB |
| `RankReadDirect` | 155.36 μs / 734.35 KB | 164.95 μs / 718.73 KB |
| `RankReadDirectHeld` | 182.63 μs / 734.35 KB | 191.89 μs / 718.73 KB |
| `RankReadGraphHeld` | 1,148.45 μs / 1752.04 KB | 1,206.92 μs / 1710.04 KB |
| `RankComparisonGraph` | 2,142.95 μs / 2733.37 KB | 1,914.34 μs / 2665.69 KB |
| `RankComparisonGraphHeld` | 1,996.93 μs / 2498.37 KB | 2,171.83 μs / 2456.93 KB |

### OrderedComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeEveryRankUnobserved` | 9.187 μs / 46.88 KB | 9.500 μs / 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache` | 1,014.473 μs / 451.17 KB | 1,014.125 μs / 451.17 KB |
| `ChangeEveryRankWithExpressions` | 1,289.637 μs / 332.03 KB | 1,297.525 μs / 332.03 KB |
| `ConstructAndDisposeWithDynamicDataCache` | 2,434.920 μs / 3935.95 KB | 2,538.033 μs / 3935.33 KB |
| `ConstructAndDisposeWithExpressions` | 690.777 μs / 1768.52 KB | 707.266 μs / 1752.89 KB |

### OrderedScaleComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeAThousandRanksUnobserved 1000` | 8.982 μs / 46.88 KB | 9.099 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 1000` | 1,006.322 μs / 451.17 KB | 1,007.863 μs / 451.17 KB |
| `ChangeAThousandRanksWithExpressions 1000` | 1,296.565 μs / 332.03 KB | 1,310.047 μs / 332.03 KB |
| `ChangeAThousandRanksThenReadWithExpressions 1000` | 1,346.863 μs / 339.94 KB | 1,321.122 μs / 339.94 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 1000` | 999.109 μs / 451.21 KB | 998.304 μs / 451.21 KB |
| `ChangeAThousandRanksThenReadUnobserved 1000` | 10.312 μs / 46.91 KB | 10.226 μs / 46.91 KB |
| `ChangeAThousandRanksUnobserved 4000` | 9.331 μs / 46.88 KB | 9.243 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 4000` | 3,673.936 μs / 468.75 KB | 3,707.407 μs / 468.75 KB |
| `ChangeAThousandRanksWithExpressions 4000` | 2,051.086 μs / 351.56 KB | 2,080.201 μs / 351.56 KB |
| `ChangeAThousandRanksThenReadWithExpressions 4000` | 2,069.216 μs / 382.91 KB | 2,097.926 μs / 382.91 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 4000` | 3,705.982 μs / 468.79 KB | 3,714.139 μs / 468.79 KB |
| `ChangeAThousandRanksThenReadUnobserved 4000` | 14.106 μs / 46.91 KB | 14.399 μs / 46.91 KB |
| `ChangeAThousandRanksUnobserved 10000` | 9.368 μs / 46.88 KB | 9.508 μs / 46.88 KB |
| `ChangeAThousandRanksWithDynamicDataCache 10000` | 9,472.175 μs / 468.75 KB | 9,295.831 μs / 468.75 KB |
| `ChangeAThousandRanksWithExpressions 10000` | 3,111.467 μs / 351.56 KB | 3,133.227 μs / 351.56 KB |
| `ChangeAThousandRanksThenReadWithExpressions 10000` | 3,184.404 μs / 429.78 KB | 3,200.937 μs / 429.78 KB |
| `ChangeAThousandRanksThenReadWithDynamicDataCache 10000` | 9,277.686 μs / 468.79 KB | 9,262.840 μs / 468.79 KB |
| `ChangeAThousandRanksThenReadUnobserved 10000` | 22.680 μs / 46.91 KB | 21.260 μs / 46.91 KB |

### ScaleComparison

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `ChangeEveryRankUnobserved 1000` | 9.124 μs / 46.88 KB | 9.020 μs / 46.88 KB |
| `ChangeEveryRankWithDynamicDataCache 1000` | 209.952 μs / 640.81 KB | 209.812 μs / 640.81 KB |
| `ChangeEveryRankWithExpressions 1000` | 17.291 μs / 46.88 KB | 22.966 μs / 46.88 KB |
| `ConstructAndDisposeWithDynamicDataCache 1000` | 2,343.728 μs / 4118.33 KB | 2,558.481 μs / 4118.33 KB |
| `ConstructAndDisposeWithExpressions 1000` | 307.676 μs / 965.58 KB | 315.671 μs / 949.95 KB |
| `ChangeEveryRankUnobserved 10000` | 91.242 μs / 468.75 KB | 91.474 μs / 468.75 KB |
| `ChangeEveryRankWithDynamicDataCache 10000` | 2,327.057 μs / 6406.44 KB | 2,336.087 μs / 6406.44 KB |
| `ChangeEveryRankWithExpressions 10000` | 184.291 μs / 468.75 KB | 250.096 μs / 468.75 KB |
| `ConstructAndDisposeWithDynamicDataCache 10000` | 42,587.512 μs / 41074.7 KB | 43,791.933 μs / 41076.5 KB |
| `ConstructAndDisposeWithExpressions 10000` | 11,698.287 μs / 9543.48 KB | 11,354.095 μs / 9387.23 KB |
| `ChangeEveryRankUnobserved 100000` | 985.241 μs / 4687.5 KB | 1,005.438 μs / 4687.5 KB |
| `ChangeEveryRankWithDynamicDataCache 100000` | 36,109.479 μs / 64062.69 KB | 34,103.467 μs / 64062.69 KB |
| `ChangeEveryRankWithExpressions 100000` | 6,955.802 μs / 4687.5 KB | 7,503.970 μs / 4687.5 KB |
| `ConstructAndDisposeWithDynamicDataCache 100000` | 797,435.597 μs / 406497.48 KB | 822,259.483 μs / 406498.16 KB |
| `ConstructAndDisposeWithExpressions 100000` | 165,456.157 μs / 94207.9 KB | 164,312.652 μs / 92645.52 KB |

### SteadyState

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `CachedRankComparisonDefaultConstruct` | 301.5 μs / 965.58 KB | 313.0 μs / 949.95 KB |
| `CachedRankComparisonDefaultSteady` | 446.6 μs / 965.58 KB | 510.1 μs / 949.95 KB |
| `CachedRankComparisonGraphConstruct` | 1,947.3 μs / 2708.17 KB | 2,112.9 μs / 2671.86 KB |
| `CachedRankComparisonGraphSteady` | 2,413.2 μs / 2958.08 KB | 2,701.9 μs / 2905.32 KB |
| `HeldFormulaDefaultConstruct` | 480.1 μs / 1449.95 KB | 492.2 μs / 1434.33 KB |
| `HeldFormulaDefaultSteady` | 744.5 μs / 1918.7 KB | 790.7 μs / 1903.08 KB |
| `HeldFormulaGraphConstruct` | 11,994.8 μs / 10565.34 KB | 13,019.2 μs / 10471.55 KB |
| `HeldFormulaGraphSteady` | 14,354.6 μs / 11507.41 KB | 13,184.6 μs / 11413.63 KB |
| `RankComparisonDefaultConstruct` | 306.2 μs / 965.58 KB | 321.5 μs / 949.95 KB |
| `RankComparisonDefaultSteady` | 495.7 μs / 1434.33 KB | 581.2 μs / 1418.7 KB |
| `RankComparisonGraphConstruct` | 2,107.8 μs / 2714.82 KB | 2,186.5 μs / 2661.36 KB |
| `RankComparisonGraphSteady` | 2,595.7 μs / 3440.82 KB | 2,974.0 μs / 3393.12 KB |
| `UnheldCallDefaultConstruct` | 310.6 μs / 996.83 KB | 323.3 μs / 981.2 KB |
| `UnheldCallDefaultSteady` | 507.4 μs / 1465.58 KB | 595.7 μs / 1449.95 KB |
| `UnheldCallGraphConstruct` | 3,602.8 μs / 4077.22 KB | 3,673.5 μs / 4027.3 KB |
| `UnheldCallGraphSteady` | 4,445.6 μs / 5031.76 KB | 5,077.2 μs / 4979.02 KB |

### TryExpression

| arm | 6.1.0, overnight | 6.1.1 |
|---|---:|---:|
| `BodyDirect` | 295.07 μs / 988752 B | 318.58 μs / 972752 B |
| `BodyGraph` | 2,890.64 μs / 3653040 B | 3,075.36 μs / 3603849 B |
| `FallingBackFaultedDirect` | 1,693.66 μs / 1244752 B | 1,758.33 μs / 1228752 B |
| `FallingBackFaultedGraph` | 7,263.29 μs / 5638128 B | 7,530.86 μs / 5583176 B |
| `FallingBackValuedDirect` | 298.48 μs / 988752 B | 319.90 μs / 972752 B |
| `FallingBackValuedGraph` | 4,554.83 μs / 5274484 B | 5,195.54 μs / 5181983 B |
| `ToggleBodyDirect` | 22.88 μs / 0 B | 36.72 μs / 0 B |
| `ToggleBodyFaultingDirect` | 1,576.38 μs / 360000 B | 1,627.93 μs / 384000 B |
| `ToggleBodyFaultingGraph` | 1,795.09 μs / 456000 B | 1,827.25 μs / 504000 B |
| `ToggleBodyGraph` | 88.67 μs / 96000 B | 111.48 μs / 96000 B |
| `ToggleFallingBackDirect` | 1,291.48 μs / 256000 B | 1,330.71 μs / 256000 B |
| `ToggleFallingBackGraph` | 1,813.75 μs / 456000 B | 1,856.92 μs / 480000 B |

## Predictions scored

- **Held.** Tests: 915 of 915 on all five, Collections 647, Components 194.
- **Held.** Stress: no unmet wait, no `ArgumentException`, no `NullReferenceException` and nothing cached, on every row.
- **Wrong, in the better direction.** `ArgumentOutOfRangeException` was predicted to remain at about its old rate; none appeared.
- **Held.** Every fast-path observation 16 bytes lighter: 965.58 to 949.95 KB on `ConstructAndDisposeWithExpressions` and the `*DefaultConstruct` arms.
- **Held.** Faulting arms 24 bytes a fault heavier; unfaulted change arms unmoved.
- **Close.** About 8 ns more a change on the fast path: 5.1 ns more against the overnight run.
- **Wrong.** Construction time within noise. On the fast path it rose by as much as 19% on the smallest observations, which the sandbox's noise hid.
- **Not predicted.** The fan-out arm, 2.5x on the fast path, where nothing but the evaluation is measured.

## What stays the caller's

A source must be safe to read while another thread changes it, and while changes arrive on several threads at once an observation can briefly report a value composed of reads taken at different moments. Both are in the readme.

## What has not been measured

The rest of the suite. Whether observers confined to one thread could skip the interlocked operations profitably is the next round's question; in a sandbox, doing so recovered about 1 ns of the cost.
