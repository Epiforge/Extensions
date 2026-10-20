# Caching only what is built

*2026-10-20 — `CachedConstructionBenchmarks` and `OperatorConstructionBenchmarks`, each against Expressions 7.1.3*

## The question

Grindstone hung opening a workspace. A select being built evaluates its selector while holding its source's changes, and a grouping's changes are its lookup's. A selector reaching a query another thread had cached and was still building waited for it, while that thread waited for the lookup. Expressions waited the same way on a node another thread was building, and a selector run for a change announced under the lookup's lock did too.

Every query and every expression node is now built outside its cache's lock and cached only once built. A thread finding nothing cached builds its own; where another thread cached one first, the one built is released and the cached one is used. So building a query costs a second acquisition of the cache's lock, and nothing waits on an expression node's initialization lock, which no longer exists.

The question is what that costs a build and what it saves a share.

## Observing and building where nothing is cached, and where one is

Each arm observes or builds 256 and disposes them.

| arm | 7.1.3 | current | | 7.1.3 | current |
|---|---:|---:|---:|---:|---:|
| observe on the graph, nothing cached | 541.81 μs | 521.20 μs | 0.96x | 630.68 KB | **592.33 KB** |
| observe on the graph, one cached | 277.14 μs | 286.68 μs | 1.03x | 231.45 KB | 231.44 KB |
| build a projection, nothing cached | 139.87 μs | 141.05 μs | 1.01x | 433.98 KB | 429.98 KB |
| build a projection, one cached | 18.61 μs | **17.15 μs** | **0.92x** | 20 KB | 20 KB |

**An observation built on the graph allocates 150 bytes less.** Each of its nodes no longer allocates the lock that serialized its building, which would account for it; allocation by type was not measured. **Observing a query already cached takes 8% less time**, since it no longer enters the query's initialization to find it already done; the first run of this round measured 8.4% on the same code. The graph arms moved 3–4% in opposite directions between runs with nothing changed beneath them (the released job's StdDev on the building arm was 46.5 μs), so neither is a difference.

## Building each operator Grindstone builds for a time slice

Each arm builds over 256 standing sources. `Slice` is the seven queries `TraverseTraitValues` builds.

| arm | elements | 7.1.3 | current | | per query |
|---|---:|---:|---:|---:|---:|
| Cast | 0 | 59.80 μs | 63.01 μs | 1.054x | +12.5 ns |
| Concat | 0 | 58.85 μs | 61.68 μs | 1.048x | +11.1 ns |
| Root | 0 | 36.24 μs | 39.62 μs | 1.093x | +13.2 ns |
| Select | 0 | 59.50 μs | 63.92 μs | 1.074x | +17.3 ns |
| SelectMany | 0 | 130.64 μs | 138.48 μs | 1.060x | +15.3 ns |
| Slice | 0 | 814.60 μs | 847.54 μs | 1.040x | +18.4 ns |
| Where | 0 | 58.15 μs | 62.60 μs | 1.077x | +17.4 ns |
| Cast | 1 | 101.02 μs | 102.30 μs | 1.013x | |
| Concat | 1 | 58.42 μs | 61.80 μs | 1.058x | |
| Root | 1 | 38.40 μs | 40.88 μs | 1.065x | |
| Select | 1 | 103.80 μs | 102.47 μs | 0.987x | |
| SelectMany | 1 | 184.08 μs | 189.23 μs | 1.028x | |
| Slice | 1 | 1,630.70 μs | 1,619.64 μs | 0.993x | |
| Where | 1 | 128.45 μs | 129.97 μs | 1.012x | |

**A build costs 11 to 18 ns more**, the second acquisition of the cache's lock. Over an empty source that is 4% to 9%; over one element the cost of observing the element dominates and the arms land between −1.3% and +6.5%. Allocation is unchanged over empty sources and 16 bytes a query less over one element (344 KB against 348 KB for 256 casts or projections); where those 16 bytes went was not measured.

The round's first version looked up a missed key three times, once before building and twice after, which cost Select over an empty source 30 ns a query (+12.7%) and a projection 7.4%. Adding with `CollectionsMarshal.GetValueRefOrAddDefault` brought it back to the two lookups 7.1.3 made. The caches keyed by `NullableKeyDictionary` (`ObserveAny`, `ObserveAppend`, `ObservePrepend` and the ordering's ranks) still look up three times, since it offers no such method.

## Predictions scored

- **Tests held:** 0 failures; Expressions 1,087 on every framework and 1,086 without dynamic code. The befores had the three new tests failing everywhere and nothing else.
- **Building on the graph allocates less, predicted:** held, 150 bytes an observation.
- **Sharing a query is faster, predicted:** held, 8%.
- **Building a query slightly slower, predicted:** held in direction; the first run's 7.4% for a projection became 0.8% once the third lookup went.
- **Grindstone's chain within a few percent, predicted:** missed on the first run (+4.6% for `Slice` over an empty source, up to +15.8% for `Root`); after the single lookup, `Slice` is +4.0% over an empty source and −0.7% over one element, and the single operators over an empty source still +4.8% to +9.3%.

## What has not been measured

Two threads building the same query at once, where one build is released: how often that happens in Grindstone and what it costs there. The three-lookup caches' share of a build. Where a one-element query's 16 bytes went.
