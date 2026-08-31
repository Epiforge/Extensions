# Benchmark comparison, 31 August 2026 — arrays allocated to hold nothing

Constructing a direct observation built two arrays unconditionally: `object?[fixedSubexpressions.Length]` for the values frozen at construction, and `DirectSubscriptionAttachment[sites.Length]` for the subscriptions. Neither length was checked, so an expression with no fixed subexpressions and no subscription sites allocated two arrays of length zero — twenty-four bytes each — for every observation.

`[]` on an array target compiles to `Array.Empty<T>()`, so the guards cost nothing. They are safe because neither array is mutated after construction: `values` is only written when its length is above zero, and `attachments` is only iterated for disposal and length-checked by `CanChange`. The slice taken when some sites resolve to nothing gets the same treatment when nothing resolves at all.

This was found while pricing the `person => true` regression, and it turned out not to be about that shape.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Three launches, a thousand elements.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *EligibilityMatrixBenchmarks*
```

## Result

| arm | before | after | per element |
|--- |---: |---: |---: |
| `ArgumentIndependentDirect` | 213.5 μs / 895.02 KB | **204.5 μs / 848.14 KB** | **−48 B** |
| `RankComparisonDirect` | 362.9 μs / 1,269.99 KB | **356.5 μs / 1,246.55 KB** | **−24 B** |
| `StaticFieldTargetDirect` | 1,348.34 KB | 1,348.34 KB | control |
| `StaticPropertyValueDirect` | 1,300.96 KB | 1,300.96 KB | control |
| `ShortCircuitDirect` | 4,766.05 KB | 4,766.05 KB | control |

Three controls byte-identical, and every graph arm unchanged.

The two controls that matter are the static ones. Both freeze something — a field on a constant, a static property — so their values array is not empty, and both take a subscription, so their attachments array is not empty either. Neither guard fires and neither moved. `ShortCircuit` is ineligible and never reaches this code.

**The more important row is the second one.** `person => person.Rank > 0` is not a degenerate shape; it is the most ordinary predicate the library has. A property read on the argument is not a fixed subexpression — only a field on a fixed target or a static property is — so that predicate has no frozen values at all, and has therefore been allocating an empty array per element since the fast path was built.

## The prediction

Written before the run: 48 bytes per element off the argument-independent arm, 24 off the rank comparison, and the other three unmoved.

**Measured: 48.0 and 24.0 bytes per element, with all three controls byte-identical.**

The stated totals were wrong, and in a way worth recording. 48,000 bytes over a thousand elements is 46.88 KB, not 48 KB; the prediction table converted per-element bytes to kilobytes as though a kilobyte were a thousand bytes, and named 847.02 and 1,245.99 where the arithmetic gives 848.14 and 1,246.55 — the measured figures. The per-element claim, which is the claim being tested, was exact. The presentation of it was not.

## What is left of the regression, and why to stop

`person => true` allocated 10.0% more than the graph and now allocates **4.3% more** — 35.6 bytes per element, which is the observation object itself. It is also **2.98× faster**, up from 2.85×.

Recovering the rest means what `2026-08-30-queries-after-direct-subscription.md` identified: the graph caches nodes by structural equality, so an expression which never mentions its argument is one shared node for every element, and the fast path builds one per element. Matching that means a cache of argument-independent observations keyed per lambda, with refcounting and lifetime management.

Not recommended. Four percent of memory on a shape nobody writes deliberately, bought with shared mutable lifetime state, is the wrong trade — and it is the same class of machinery that produced the static-property freezing bug recorded in `2026-08-31-static-members-and-disposal.md` earlier the same day.

The framing this inherited was also wrong and should stop being repeated. Three times the speed for four percent of the memory is not a regression awaiting a fix. It is a trade, and it has moved decisively in the fast path's favor since it was first recorded from a cross-run comparison that put the time difference at thirteen percent.
