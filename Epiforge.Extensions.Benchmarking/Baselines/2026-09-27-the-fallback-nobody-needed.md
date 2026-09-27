# The fallback nobody needed

*2026-09-27 — `TryExpressionBenchmarks` and `InvocationDeferralBenchmarks`, after try observation and the reducer's deferral fix in 6.1.0, from the first run of the whole suite under the new job*

## The job

Every class now runs three launches of ten 250 ms iterations after four warmup iterations, set once in `Program.cs`, apart from `DictionaryMutationBenchmarks` and the four `RangeCollection*` classes, which keep 40 iterations across three processes. Each error column therefore carries the variance between processes as well as within one. Times from before this date were taken under per-class jobs and are not comparable with these; allocations are.

## Try observation

A thousand elements, each observed through `ObserveWhere` and disposed of, or a thousand standing observations toggled between a rank of 50 and another.

| arm | mean | error | allocated |
|---|---:|---:|---:|
| `BodyDirect` — the body alone, control | 295.07 μs | 3.943 μs | 988,752 B |
| `FallingBackValuedDirect` | 298.48 μs | 4.132 μs | **988,752 B** |
| `FallingBackFaultedDirect` | 1,693.66 μs | 12.893 μs | 1,244,752 B |
| `BodyGraph` — the body alone, control | 2,890.64 μs | 192.807 μs | 3,653,040 B |
| `FallingBackValuedGraph` | 4,554.83 μs | 311.618 μs | 5,274,484 B |
| `FallingBackFaultedGraph` | 7,263.29 μs | 476.379 μs | 5,638,128 B |
| `ToggleBodyDirect` — control | 22.88 μs | 0.204 μs | 0 B |
| `ToggleBodyFaultingDirect` | 1,576.38 μs | 8.529 μs | 360,000 B |
| `ToggleFallingBackDirect` | 1,291.48 μs | 6.391 μs | 256,000 B |
| `ToggleBodyGraph` — control | 88.67 μs | 1.160 μs | 96,000 B |
| `ToggleBodyFaultingGraph` | 1,795.09 μs | 35.297 μs | 456,000 B |
| `ToggleFallingBackGraph` | 1,813.75 μs | 13.612 μs | 456,000 B |

**On the fast path, a try whose body has a value costs nothing: the same 988,752 bytes as the body alone, and 1.2% in time.** A faulted body adds 256 bytes an element, which is the exception, and its time is the throw. On the graph the try is a node and its handler is a second one, taking its subscriptions only once a fault selects it.

## Invocation deferral

A thousand elements whose predicate invokes a literal lambda reading its argument only inside a branch.

| arm | before | after |
|---|---:|---:|
| `BranchedArgumentDefault` | 344.5 μs / 965.58 KB | 8,080.9 μs / **9,277.33 KB** |
| `BranchedArgumentGraph` | 15,207.1 μs / 9,289.17 KB | 7,731.8 μs / 9,303.99 KB |
| `EagerArgumentDefault` — control | 338.8 μs / 965.58 KB | 305.5 μs / 965.58 KB |

The befores' times were taken under the old job. **The fast path no longer reduces the invocation, so it builds the graph, as the graph and compiled .NET both evaluate the argument first and report its fault.** That costs 9.61x the memory of the eager arm, and that is what correctness costs here. An argument which is a constant or a parameter cannot fault and is still reduced wherever it is read.

## Predictions scored

- **Held.** `FallingBackValuedDirect` allocates exactly what `BodyDirect` does: 988,752 B.
- **Held.** `FallingBackFaultedDirect` adds about 256 B an element: 1,244,752 against 988,752.
- **Held.** `ToggleBodyDirect` allocates 0 and `ToggleFallingBackDirect` 256,000 B.
- **Held.** `FallingBackValuedGraph` below the trial's 5,502,192 B: 5,274,484.
- **Held.** `FallingBackValuedDirect` within 9% of `BodyDirect` in time: 1.2%.
- **Held.** `EagerArgumentDefault` at 965.58 KB, and the graph arm within 0.5%: 9,289.17 to 9,303.99 KB, 0.16%.
- **Missed, by drift.** `BranchedArgumentDefault` exactly equal to `BranchedArgumentGraph`: 9,277.33 against 9,303.99 KB, 0.29% apart. Both build the same graph, and graph arms move by that much between runs.

## What the rest of the suite showed

Against each class's previous report, 462 arms allocated identically, 74 moved less than 0.5% and 241 moved more. The previous reports date from 29 August to 23 September, so that is a month of library changes rather than drift. The largest movers, `ConcatIndexerPenaltyBenchmarks`, `ObservableExpressionBenchmarks` and `DirectSubscriptionOverheadBenchmarks`, allocate identically under 6.0.7 and 6.1.0 outside the suite with tiered compilation off, so none of their movement is this round's.

- **`ConcatIndexerPenaltyBenchmarks`**, the maximum arms, 1.3 to 79 KB: the report predated the comparison query keeping its distinct values in order, at 88 bytes each, and there are a thousand here.
- **`ObservableExpressionBenchmarks`**, 4.3 to 8.3 KB: the report predated direct subscription becoming the default, and the arm writes its lambda inline, so each call pays for the analysis and the compiler.
- **`QueryLambdaReuseBenchmarks`**: `WhereRebuilt`, the predicate written inline, measured 14,228.80 μs ± 3,174.25, against 9,784.08 μs ± 194.20 before. Compilation varies between processes in a way that one process never shows.

## Retention

`QueryFootprintReport`, run with `--footprint` after the suite: every control read as it must, nothing built reading zero and this library's five build-and-drop cycles leaving at most 192 B, and no query or expression stayed cached after disposal. A filtered view of a thousand elements retains 935.1 B an element against DynamicData's 1,865.0, and a grouped view of ten thousand 1,019.4 B against 1,953.5. The readmes quote these.
