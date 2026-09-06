# Benchmark results, 6 September 2026 — the shapes that qualify

`UseDirectSubscription` has defaulted to `true` since the fast path was written, and the orientation has said from the start that the fraction of real expressions left on the graph was unmeasured and must not be asserted. Nine of the open work items price themselves against that fraction. This measures it, for the only corpus this repository actually has: the expression shapes its own benchmarks and reports pass to queries.

`ShapeEligibilityBenchmarks` is new. It adds the six shapes no eligibility class covered — the four key and projection selectors behind `ObserveSelect`, `ObserveAverage`, `ObserveGroupBy` and `ObserveOrderBy`, a predicate closing over a live object, and `RankComparison` as the baseline the three existing eligibility classes share. Paired `Direct` and `Graph` arms, `ConstructAndDispose` over a thousand elements, in the same form as `EligibilityMatrixBenchmarks`, `OperatorEligibilityBenchmarks` and `MethodCallEligibilityBenchmarks`.

This is not a release baseline. Nothing in the libraries changed to produce it.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## The instrument

Allocation is the reading, not time. Where a pair's two arms allocate the same, the direct path declined the shape and the observation fell back to the graph, so both arms are measuring the same machinery. Where they differ, the direct path took it.

What makes this sound is that the refusals are not small differences to be argued over. They are identity:

| | Direct | Graph |
|--- |---: |---: |
| `ShortCircuit` | 4,595.68 KB | 4,595.68 KB |
| `DeferredBranch` | 6.67 MB | 6.67 MB |

Byte for byte, at four significant figures, on separately compiled arms. Nothing else in these four classes comes within two percent of its partner.

## ShapeEligibilityBenchmarks

| Method | Mean | Allocated | Alloc ratio |
|--- |---: |---: |---: |
| `CapturedTargetDirect` | 418.7 μs | 1.27 MB | 0.49 |
| `CapturedTargetGraph` | 2,657.8 μs | 2.95 MB | 1.13 |
| `CastSelectorDirect` | 358.0 μs | 1.25 MB | 0.48 |
| `CastSelectorGraph` | 2,151.3 μs | 2.64 MB | 1.01 |
| `GroupKeySelectorDirect` | 363.7 μs | 1.25 MB | 0.48 |
| `GroupKeySelectorGraph` | 2,316.8 μs | 2.71 MB | 1.04 |
| `OrderKeySelectorDirect` | 363.4 μs | 1.25 MB | 0.48 |
| `OrderKeySelectorGraph` | 1,359.9 μs | 1.97 MB | 0.75 |
| `ProductSelectorDirect` | 361.8 μs | 1.25 MB | 0.48 |
| `ProductSelectorGraph` | 2,302.4 μs | 2.71 MB | 1.04 |
| `RankComparisonDirect` | 350.1 μs | 1.17 MB | 0.45 |
| `RankComparisonGraph` | 2,149.8 μs | 2.62 MB | 1.00 |

Every pair separates. All six shapes are eligible.

`OrderKeySelector` is `person => person.Rank`, a bare member read, and its graph arm is the cheapest of the six at 1.97 MB against 2.62–2.95 for the rest — one node fewer than the shapes that compare or multiply. The direct arms are flat at 1.25 MB whatever the shape, which is what a fast path that subscribes to one property and evaluates a compiled delegate should look like.

## The whole corpus

Thirteen distinct shapes, every one transcribed from a lambda this project already passes to a query.

| Shape | Expression | Verdict |
|--- |--- |--- |
| `ArgumentIndependent` | `person => true` | eligible |
| `RankComparison` | `person => person.Rank > 0` | eligible |
| `StringComparison` | `person => person.Name == "Emily"` | eligible |
| `MethodCall` | `person => string.IsNullOrEmpty(person.Name)` | eligible |
| `StaticFieldTarget` | `person => person.Rank > staticThreshold.Rank` | eligible |
| `StaticPropertyValue` | `person => person.Rank > Environment.ProcessorCount` | eligible |
| `CapturedTarget` | `person => person.Rank > liveThreshold.Rank` | eligible |
| `ProductSelector` | `person => person.Rank * 2` | eligible |
| `CastSelector` | `person => (double)person.Rank` | eligible |
| `GroupKeySelector` | `person => person.Rank % 16` | eligible |
| `OrderKeySelector` | `person => person.Rank` | eligible |
| `ShortCircuit` | `person => person.Rank > 0 && person.Rank < 100` | **ineligible** |
| `DeferredBranch` | `person => person.Rank % 2 == 0 && person.Name.Length > 1` | **ineligible** |

Eleven of thirteen take the fast path. The two that do not are the same cause: `&&`.

`ArgumentIndependent` is the one eligible pair whose arms nearly touch — 801.05 KB direct against 813.17 KB graph, one and a half percent. `person => true` has nothing to subscribe to, so neither mechanism builds much, and the reading rests on the time (193 μs against 586) rather than the bytes.

## What this reprices

The refused shapes are also the expensive ones. `ShortCircuit` costs 4,595.68 KB and `DeferredBranch` 6.67 MB, against a graph baseline of 2.62 MB and an eligible-and-taken cost of 1.17–1.27 MB. The single most expensive thing a caller can write in this corpus is `&&`, and it is expensive precisely because it is refused: it pays the graph in full, and the graph pays more for it than for anything else.

That reorders the board. Work that shaves the graph — the boxing of non-boolean results, the round trips already removed — now has a measured population: expressions the analyzer refuses. In this corpus that population is `&&` and nothing else. Making `&&` eligible would be worth more than any further shaving of what it currently falls back to, and it is the shape most likely to appear in a real predicate.

## Two limits on the claim

This describes the shapes **this repository** writes. It says nothing about what users of these packages write, which nothing here observes, and the sample was chosen by whoever wrote the benchmarks rather than drawn from anything. Eleven of thirteen is a fact about `Baselines\`; it is not a fact about the field.

The four selectors were measured through `ObserveSelect` so that the query was held constant and the expression shape was the only thing varying. That establishes the shapes are eligible. It does not establish that `ObserveAverage`, `ObserveGroupBy` and `ObserveOrderBy` hand their selectors to the observer the same way `ObserveSelect` does, which is unmeasured.

## The baseline across four classes

`RankComparisonGraph` is run by all four eligibility classes and is the only figure directly comparable between them.

| Class | Mean | Allocated |
|--- |---: |---: |
| `ShapeEligibilityBenchmarks` (today) | 2,149.8 μs | 2.62 MB |
| `EligibilityMatrixBenchmarks` (today) | 2,089.7 μs | 2,679.46 KB |
| `OperatorEligibilityBenchmarks` (recorded earlier) | 2,351.8 μs | 2.69 MB |
| `MethodCallEligibilityBenchmarks` (recorded earlier) | 2,366.0 μs | 2.69 MB |

The two run today agree at 2.62 MB and the two recorded earlier agree at 2.69 MB. The split is by date, not by class, so it is a real change in the libraries between the two runs rather than noise — the interval contains the boxing round trip removed on 6 September. The direction is right and the magnitude is not yet decomposed, so no per-observation figure is claimed from it.

The 13% spread in time across the four is larger than any one class's standard deviation and is consistent with the suite noise already calibrated at up to about 7% between runs; time is not the reading here.

## A note on method

No prediction was recorded before this run, and the reason was written down at the time: there was no measured unit cost to multiply, only a story about which shapes ought to qualify, and every prediction of that kind has failed this month. Declining to predict cost nothing here — the arms answer the question whether or not a guess preceded them.

The census this replaced would have reported the analyzer's `DirectSubscriptionIneligibility` reason codes alongside these verdicts. Under benchmarks alone those reasons are not observable, so `ShortCircuit` and `DeferredBranch` are known to be refused and not known to be refused *for* `DeferredBranch` rather than something else. Both contain `&&` and nothing else in the corpus does, which is suggestive and is not proof.
