# Benchmark comparison, 30 August 2026 — observable queries after direct subscription

The first measurement of the query layer against released code. Expressions 4.0.0 and Components 4.1.1 are on NuGet; everything below was run against the tagged release commit.

Four suites were chosen to answer one question — how much of the direct-subscription win reaches a query, whose selectors and predicates become observable expressions one per element. The answer turned out to depend entirely on a distinction the run itself exposed.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

`QueryFanOutBenchmarks` runs at three launches; the other three at one. Launch counts were deliberately left as recorded, because the figures being compared against were taken at those settings and changing them would have muddied the comparison rather than sharpened it.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveWhereBenchmarks* *ObserveSelectBenchmarks* *QueryFanOutBenchmarks* *QueryFootprintBenchmarks*
```

## The prediction, and the error in it

Before the run, every lambda in the four suites was classified eligible or ineligible from the analyzer's rule. Six were called eligible and one — `person => person.Rank % 2 == 0 && person.Name.Length > 1` — ineligible, on the grounds that `&&` is a deferred branch.

**Three of the six were wrong.** Every lambda of the form `person => person.Rank > threshold.Rank` was called eligible because `threshold` looked like a captured variable. It is not: `threshold` is an *instance field of the benchmark class*, so the expression tree holds `Field(Constant(benchmarkInstance), "threshold")`, and `IsClosureField` requires the target's type to be compiler-generated. `QueryFootprintBenchmarks` does not begin with `<`. Those three are ineligible.

The error was found by the data disagreeing with the prediction, which is the only reason the prediction was written down.

It also makes the run a cleaner experiment than it was designed to be, since the suites now contain both populations under otherwise identical conditions.

## QueryFootprint: eligible and ineligible shapes, side by side

Allocation, which is deterministic and has been the trustworthy column in this series throughout. The recorded figures are the "after" column of `2026-08-29-cumulative.md`, the last measurement before this one.

| shape | eligible | recorded | now | |
|--- |:---: |---: |---: |---: |
| `person => person.Rank % 2 == 0` | yes | 48,512.30 KB | **12,528.00 KB** | **3.87×** |
| `person => person.Rank > 0` | yes | 36,146.43 KB | **12,527.15 KB** | **2.89×** |
| `person => person.Rank > threshold.Rank` | no | 41,859.18 KB | 38,656.43 KB | 1.08× |
| `person => person.Rank % 2 == 0 && person.Name.Length > 1` | no | 97,672.27 KB | 90,953.98 KB | 1.07× |
| `person => true` | yes, no sources | 7,752.26 KB | 8,775.88 KB | **0.88×** |

At ten thousand elements. Time at the same size: `FiveNodes` 97,185.7 → 20,639.4 μs (**4.71×**), `ThreeNodes` 75,005.4 → 20,097.0 μs (**3.73×**), `ElevenNodes` 195,442.5 → 191,026.8 μs (1.02×).

The two eligible shapes with real per-element expressions shed roughly two thirds to three quarters of their allocation and three to five times their time. The two ineligible shapes moved seven percent, which is about what typed comparers alone account for and is the same seven percent visible on every arm.

## QueryFanOut: one variable, two arms

The cleanest evidence in the run, because these two benchmarks differ in nothing but their predicate.

| | eligible | recorded | now |
|--- |:---: |---: |---: |
| `ConstructAndDisposeWithoutFanOut` — `person => person.Rank > 0` | yes | 2,416.67 μs / 3,323.68 KB | **486.86 μs / 1,278.06 KB** |
| `ConstructAndDisposeWithFanOut` — `person => person.Rank > threshold.Rank` | no | 2,609.76 μs / 3,889.54 KB | 2,730.38 μs / 3,890.13 KB |
| `ChangeTheSharedValue` | no | 7.400 μs / 23.7 KB | 7.445 μs / 23.7 KB |

**4.96× the speed on 2.60× less memory** where the predicate qualifies. Where it does not, 3,889.54 KB became 3,890.13 KB — the same to within a kilobyte on nearly four megabytes.

## The regression, recorded rather than buried

`person => true` allocates **13% more** than it did: 7,752.26 KB against 8,775.88 KB at ten thousand elements. Time improved 13% over the same interval, so this is memory only.

The cause is structural and worth stating, because it names the one thing the graph does that the fast path does not. The graph caches nodes by structural expression equality, so a predicate that does not mention its argument is *one shared node* for all ten thousand elements. The fast path caches the compiled delegate per lambda but builds a node per observation, so it makes ten thousand. For every other shape this costs nothing, because a predicate that does mention its argument produces a structurally distinct tree per element and the graph builds ten thousand nodes too.

So the exposure is precisely: expressions with no dependence on the argument, observed many times. That is degenerate in a query predicate and was not worth defending against, but it is the shape of the only case where the new mechanism is behind.

## What this measures, and what it does not

The recorded figures for these four suites were taken at different points relative to typed comparers and wrapper deferral, so a raw before-and-after spans up to three changes. What is *not* subject to that is the contrast between eligible and ineligible arms within this single run, on one machine, in one process each — and that contrast is the whole finding.

## The opportunity this hands to the next branch

`IsFixed` admits a field only when its declaring type is compiler-generated. That rules out reading through any field of an ordinary object, which is how a great deal of real code is written — a threshold on a view model, a setting on a service.

The refusal looks conservative rather than necessary. A field raises no `PropertyChanged`, so the graph does not subscribe to one either; both mechanisms already freeze such a value for the life of the observation. Admitting fields on ordinary constants would move `SharedChangeableSubexpression` and the fan-out arms — the most expensive shapes in this suite at ten to fourteen times the control — onto the fast path.

Recorded as a candidate rather than a plan. Two claims here are about the graph's behavior rather than the fast path's, and claims of that kind have been wrong repeatedly in this series.
