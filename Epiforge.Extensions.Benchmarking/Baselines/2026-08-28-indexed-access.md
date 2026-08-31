# Benchmark comparison, 28 August 2026 — indexed access

Measured later the same day as the 28 August baseline, against the same hardware and the same runtime, with one change to the libraries: `PrefixWeightedSequence` gained finger search, and `ObservableCollectionSelectQuery` and `ObservableCollectionWhereQuery` were simplified onto it.

This is not a release baseline. It measures unreleased code. The 27 August document remains the record of what shipped as Components 4.0.0, Collections 3.0.0 and Expressions 3.0.0.

It exists as a separate document rather than an addendum because it invalidates every indexer figure in the 28 August document and adds two benchmarks that did not exist there. The rest of that document still stands.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

Identical to 27 and 28 August, which is why the three runs may be compared at all.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveSelectBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveWhereSelectivityBenchmarks*
```

## Reliability

Four consecutive runs of these two classes were taken while the change was being tuned. At 10,000 elements, repeat runs of the same row agree within about three percent. At 1,000 elements they do not; the `ObserveSelect` `EnumerateByIndexer` row moved seventeen percent between two runs with no code change between them, and is called out below as a row not to draw conclusions from.

An earlier note in this series claimed a seventeen percent noise floor generally. That figure came from comparing two *different* benchmark classes measuring the same query, and it does not describe repeat runs of one class. It should not be used as a noise floor.

## What changed

`PrefixWeightedSequence<T>` gained two public methods, `NodeAtFrom` and `NodeAtWeightFrom`. Each takes a node the caller already holds along with that node's own position, and searches outward from it: up the parent chain until an ancestor's subtree spans the target, then down. The cost is the logarithm of the distance rather than the logarithm of the sequence, and on the weight-based variant a run of zero-weight items is climbed over rather than walked.

Both methods decide per call whether that is actually cheaper than starting at the root, and search from the root when it is not. The test is `d⁴ ≤ n`, and the section on thresholds below is the evidence for it.

`ObservableCollectionSelectQuery` and `ObservableCollectionWhereQuery` both kept a cursor before this change — the select query stepping to the successor when asked for `index + 1`, the where query doing the same across a bounded run of non-members. Both now hand their cursor to the sequence instead. Two tuning constants, `maximumCursorGap` and `maximumCursorSteps`, were deleted.

## The new benchmarks

`EnumerateByIndexerBackward` walks the result from the last position to the first. It could not have been written before this change: the sequence had no way to move backwards, so a reverse walk was a full descent per element.

`EnumerateByIndexerInterleaved` visits every position exactly once, alternating between the two halves of the range, so that consecutive accesses are always far apart. It exists as a control. Because it covers the same positions as `EnumerateByIndexer` in a different order, the pair isolates the effect of proximity with the working set held constant, and because every access is distant it always takes the descent from the root. It is the measurement of what indexed access costs without a finger.

`SweepByIndexerOutOfOrder` cannot serve that purpose and should not be read as though it does. It indexes with `Mix(i) % limit`, which is a hash reduced modulo the limit and not a permutation: it revisits some positions and never visits others, covering roughly sixty-three percent of them. It therefore touches a smaller working set than the sequential sweep and gains a cache advantage that grows as the collection shrinks. It measures scattered access, which is a real pattern worth having, but it is not a like-for-like control against a sequential sweep. Reading it as one produced a false regression that took two rounds to unwind.

## ObserveSelect

| Method                        | ElementCount | Mean            | Error        | StdDev       | Allocated |
|------------------------------ |------------- |----------------:|-------------:|-------------:|----------:|
| Enumerate                     | 100          |        86.47 ns |     0.745 ns |     0.697 ns |      64 B |
| EnumerateByIndexer            | 100          |     1,347.19 ns |     8.321 ns |     7.783 ns |         - |
| EnumerateByIndexerBackward    | 100          |     1,356.75 ns |    13.700 ns |    12.815 ns |         - |
| EnumerateByIndexerInterleaved | 100          |     1,343.06 ns |     5.212 ns |     4.352 ns |         - |
| EnumerateThenSweepByIndexer   | 100          |     1,450.57 ns |    13.840 ns |    12.946 ns |      64 B |
| SweepByIndexerOutOfOrder      | 100          |     1,351.75 ns |     7.338 ns |     6.864 ns |         - |
| Enumerate                     | 1000         |       621.01 ns |     2.218 ns |     1.852 ns |      64 B |
| EnumerateByIndexer            | 1000         |    15,883.09 ns |   135.924 ns |   113.502 ns |         - |
| EnumerateByIndexerBackward    | 1000         |    13,601.00 ns |    85.307 ns |    79.796 ns |         - |
| EnumerateByIndexerInterleaved | 1000         |    14,571.58 ns |   127.673 ns |   119.425 ns |         - |
| EnumerateThenSweepByIndexer   | 1000         |    14,224.29 ns |    91.322 ns |    85.423 ns |      64 B |
| SweepByIndexerOutOfOrder      | 1000         |    19,067.57 ns |   348.517 ns |   326.003 ns |         - |
| Enumerate                     | 10000        |     6,120.15 ns |   106.838 ns |   149.773 ns |      64 B |
| EnumerateByIndexer            | 10000        |   187,158.80 ns | 1,634.861 ns | 1,449.262 ns |         - |
| EnumerateByIndexerBackward    | 10000        |   192,340.37 ns | 1,306.532 ns | 1,222.131 ns |         - |
| EnumerateByIndexerInterleaved | 10000        |   423,598.11 ns | 2,038.585 ns | 1,807.152 ns |         - |
| EnumerateThenSweepByIndexer   | 10000        |   142,184.24 ns |   151.865 ns |   126.814 ns |      64 B |
| SweepByIndexerOutOfOrder      | 10000        | 1,222,332.07 ns |22,513.724 ns |19,957.830 ns |         - |

Per access, in nanoseconds:

| ElementCount | enumerate | finger, forward | finger, backward | root descent | snapshot | scattered |
|------------- |----------:|----------------:|-----------------:|-------------:|---------:|----------:|
| 100          |      0.86 |           13.47 |            13.57 |        13.43 |    14.51 |     13.52 |
| 1000         |      0.62 |           15.88 |            13.60 |        14.57 |    14.22 |     19.07 |
| 10000        |      0.61 |           18.72 |            19.23 |        42.36 |    14.22 |    122.23 |

## ObserveWhere by selectivity

10,000 source elements throughout; the query holds one element in `OneIn` of them.

| Method                        | OneIn | Mean          | Error        | StdDev       | Allocated |
|------------------------------ |------ |--------------:|-------------:|-------------:|----------:|
| Enumerate                     | 2     |   4,066.82 ns |    41.619 ns |    38.930 ns |      64 B |
| EnumerateByIndexer            | 2     | 192,841.79 ns | 1,199.058 ns | 1,062.933 ns |         - |
| EnumerateByIndexerBackward    | 2     | 177,932.36 ns | 1,150.346 ns | 1,076.034 ns |         - |
| EnumerateByIndexerInterleaved | 2     | 320,246.64 ns | 3,424.625 ns | 3,203.396 ns |         - |
| SweepByIndexerOutOfOrder      | 2     | 625,592.59 ns | 5,271.104 ns | 4,672.697 ns |         - |
| Enumerate                     | 8     |   1,034.28 ns |     6.381 ns |     5.657 ns |      64 B |
| EnumerateByIndexer            | 8     |  51,803.10 ns | 1,016.901 ns | 1,322.259 ns |         - |
| EnumerateByIndexerBackward    | 8     |  46,553.26 ns | 1,060.398 ns | 3,093.231 ns |         - |
| EnumerateByIndexerInterleaved | 8     |  55,691.27 ns |   854.733 ns |   757.698 ns |         - |
| SweepByIndexerOutOfOrder      | 8     |  90,477.53 ns | 1,758.669 ns | 2,407.286 ns |         - |
| Enumerate                     | 16    |     538.84 ns |     4.362 ns |     3.867 ns |      64 B |
| EnumerateByIndexer            | 16    |  33,947.67 ns |   532.888 ns |   498.464 ns |         - |
| EnumerateByIndexerBackward    | 16    |  31,261.48 ns |   449.096 ns |   420.085 ns |         - |
| EnumerateByIndexerInterleaved | 16    |  33,118.24 ns |   510.486 ns |   452.532 ns |         - |
| SweepByIndexerOutOfOrder      | 16    |  34,953.91 ns |   658.740 ns |   704.844 ns |         - |
| Enumerate                     | 32    |     285.46 ns |     2.248 ns |     2.102 ns |      64 B |
| EnumerateByIndexer            | 32    |  17,576.82 ns |   265.759 ns |   248.591 ns |         - |
| EnumerateByIndexerBackward    | 32    |  16,285.14 ns |   312.862 ns |   307.272 ns |         - |
| EnumerateByIndexerInterleaved | 32    |  17,504.70 ns |   349.635 ns |   359.049 ns |         - |
| SweepByIndexerOutOfOrder      | 32    |  13,997.04 ns |   278.088 ns |   380.650 ns |         - |
| Enumerate                     | 128   |      96.56 ns |     0.920 ns |     0.815 ns |      64 B |
| EnumerateByIndexer            | 128   |   2,719.40 ns |    53.617 ns |    73.392 ns |         - |
| EnumerateByIndexerBackward    | 128   |   2,644.00 ns |    52.418 ns |    93.174 ns |         - |
| EnumerateByIndexerInterleaved | 128   |   2,756.74 ns |    54.923 ns |   108.412 ns |         - |
| SweepByIndexerOutOfOrder      | 128   |   2,633.67 ns |    45.730 ns |    42.776 ns |         - |

Per access, in nanoseconds. The estimated distance is the number of positions the query expects to traverse to reach the next member, which is what the sequence uses to decide whether to search outward.

| OneIn | held | est. distance | finger? | forward | backward | root descent | scattered |
|------ |-----:|--------------:|:--------|--------:|---------:|-------------:|----------:|
| 2     | 5000 |             2 | yes     |   38.57 |    35.59 |        64.05 |    125.12 |
| 8     | 1250 |             8 | yes     |   41.44 |    37.24 |        44.55 |     72.38 |
| 16    |  625 |            16 | no      |   54.32 |    50.02 |        52.99 |     55.93 |
| 32    |  313 |            31 | no      |   56.16 |    52.03 |        55.93 |     44.72 |
| 128   |   79 |           126 | no      |   34.42 |    33.47 |        34.90 |     33.34 |

## What the numbers say

**The escape hatch does what it claims.** At the three selectivities where the sequence declines to search outward, the forward sweep and the interleaved control agree: 54.32 against 52.99, 56.16 against 55.93, 34.42 against 34.90. When the rule declines a finger, the access really is an ordinary descent from the root, and the decision itself costs nothing measurable.

**Proximity is worth between one and two and a quarter times.** Holding the working set constant, the forward sweep against the interleaved control gives 2.27x on `ObserveSelect` at 10,000, 1.66x on `ObserveWhere` at half selectivity, and 1.08x at one in eight. It is worth nothing at 1,000 elements or fewer, where the whole tree is cache-resident and a descent from the root is already free.

**The control reproduced two independent historical figures.** The interleaved row measures 42.36 ns per access on select, against the 40.82 recorded on 28 August before any cursor existed, and 64.05 on where against 64.34. Neither number was available to the benchmark that produced them.

**Backward is uniformly faster than forward by five to eight percent**, including at the selectivities where both are plain descents from the root and no finger is involved. Since it appears where the feature does not, it is not a property of the feature. It is not explained here.

## On the threshold

The sequence searches outward when `d⁴ ≤ n`, where `d` is the distance in positions and `n` the number of items — about ten positions at ten thousand items, about thirty at a million.

A naive step count argues for `d² ≤ n`: searching outward visits roughly `2·log₂ d` nodes against `log₂ n` for a descent, and those are equal at `d² = n`. Measured on a ten thousand node treap, the step counts do cross near `d = 72`, close to the `d = 100` that rule predicts.

Step counts are the wrong unit. A descent from the root re-treads the same upper nodes on every call and they stay in cache; a climb walks the ancestors of a moving finger and then descends into cold ones. Pricing a climb step at about twice a cached descent step moves the crossover to `d = n^¼`, and that is what the measurements show: worth 2.27x at a distance of one, 1.66x at two, 1.08x at eight, and declined from sixteen upward, where the collapsing trend puts it at break-even.

The rule is expressed without a tuned constant and scales with the collection. It is evaluated with multiplications; an earlier form used integer division to avoid overflow and put two divisions on every indexed access, which is not affordable on a path measured in tens of nanoseconds.

## What an indexed access is made of

On `ObserveSelect` at 10,000 elements, a sequential indexed access costs 18.72 ns. The `EnumerateThenSweepByIndexer` row is the same access served from a materialized snapshot with no tree involved, at 14.22 ns. The search is therefore about 4.5 ns and everything else — the interface dispatch, the lock the query takes for every access, the bounds check — is about 14.2.

**Roughly three quarters of an indexed access on a select query is now overhead that has nothing to do with finding the element.** Before this change the split was closer to even. Further work on the search alone cannot win back more than that quarter.

The where query has not reached that point. At half selectivity its sequential access costs 38.57 ns against a comparable snapshot floor of about 14.8, so the search is still roughly sixty percent of it. The difference from select is entirely the cost of climbing over non-members: both queries search a ten thousand node tree, but select's next element is always the adjacent node and where's is not.

## Rows not to trust

`ObserveSelect` `EnumerateByIndexer` at 1,000 elements reads 15.88 ns per access here and 13.53 in an earlier run of identical code. At that size every path is within a nanosecond or two of every other and the row does not reproduce. Nothing should be concluded from it in either direction.

`SweepByIndexerOutOfOrder` should not be compared against a sequential row at any size, for the reason given above.

## Open

`Epiforge.Extensions.Collections` carries two new public methods while its `Version` and `PackageValidationBaselineVersion` both read 3.0.0. The addition is binary compatible, so validation passes, but the shipped version will need a minor bump and a release note.
