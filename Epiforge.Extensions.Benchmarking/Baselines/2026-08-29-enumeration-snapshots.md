# Benchmark comparison, 29 August 2026 — enumeration snapshots

Six query classes were caching nothing: every enumeration, and every read of `Keys` or `Values`, built a fresh copy of the entire collection. Four other classes — `Select`, `SelectMany`, `Where` and `OrderBy` — already cached a snapshot and discarded it on change. This propagates that idea to the six, in its simplest form.

Two benchmarks were written for this work, because none existed. `ObserveDictionaryWhereBenchmarks` covers the dictionary family, `ObserveGroupByBenchmarks` the collection family.

This is not a release baseline. It measures unreleased code.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveDictionaryWhereBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveGroupByBenchmarks*
```

## What changed

Each affected class gained nullable snapshot fields, filled on first read and set to null in the change handler it already owned. `Keys`, `Values` and `GetEnumerator` return the cached snapshot instead of building one.

The four classes that already cached go further: they *patch* their snapshot on small changes, so they track live enumerations to know when a patch would be visible to someone mid-walk. Nothing here patches. On any change the field is nulled and the next reader builds a fresh list; an enumerator holding the old list keeps walking the old list. That is exactly what happened before, when every enumerator got its own private copy. **The semantics are unchanged — only the number of copies.**

## ObservableDictionaryWhereQuery

| | before | after |
|--- |---: |---: |
| `Count`, 10,000 | 12.79 ns, 0 B | 12.91 ns, 0 B |
| `ReadKeys`, 100 | 53.30 ns, 480 B | 11.85 ns, 0 B |
| `ReadKeys`, 1,000 | 404.77 ns, 4,080 B | 11.85 ns, 0 B |
| `ReadKeys`, 10,000 | 8,869.07 ns, 40,080 B | **11.82 ns, 0 B** |
| `ReadValues`, 100 | 126.82 ns, 880 B | 12.40 ns, 0 B |
| `ReadValues`, 1,000 | 1,236.03 ns, 8,080 B | 12.10 ns, 0 B |
| `ReadValues`, 10,000 | 19,899.07 ns, 80,080 B | **11.85 ns, 0 B** |
| `Enumerate`, 100 | 428.03 ns, 1,704 B | 278.46 ns, 48 B |
| `Enumerate`, 1,000 | 4,131.40 ns, 16,104 B | 2,529.74 ns, 48 B |
| `Enumerate`, 10,000 | 73,345.09 ns, 160,103 B | **25,837.44 ns, 48 B** |

`ReadValues` at ten thousand entries is **1,679× faster** and allocates nothing. Both property reads now cost less than `Count`, which still takes the lock and asks the dictionary; the ratio column puts them at 0.92 against it.

`Enumerate` still scales linearly in time, and must — a `foreach` visits every entry regardless. What changed is the per-item cost, from 7.3 ns to 2.6, and the allocation, from 160 KB to a flat 48 bytes for the boxed enumerator no matter the size.

The before-figures were linear to the byte: four bytes per `int` key, eight per reference value, sixteen per pair, plus eighty bytes of fixed overhead. `Count` did not move, which is what makes the rest readable.

## The other five classes, and a null control

`GroupBy`, `IndividualChanges`, `Lookup`, `ToDictionary` and `DictionarySelect` received the same treatment afterwards. `ObserveDictionaryWhereBenchmarks` was re-run against that change even though none of it touches `ObservableDictionaryWhereQuery`:

| at 10,000 | before the five | after the five |
|--- |---: |---: |
| `Count` | 12.91 ns | 12.71 ns |
| `Enumerate` | 25,837.44 ns, 48 B | 26,048.70 ns, 48 B |
| `ReadKeys` | 11.82 ns, 0 B | 11.82 ns, 0 B |
| `ReadValues` | 11.85 ns, 0 B | 11.83 ns, 0 B |

Unchanged, as it should be. A class nobody edited that had nevertheless moved would have meant the caching was reaching further than intended.

## ObservableCollectionGroupByQuery, and what invalidation costs

The key selector is `person.Rank % 16`, so there are sixteen groupings at every element count. An uncached enumeration would allocate in proportion to groupings and rebuild on each call; a cached one is flat.

| | 100 | 1,000 | 10,000 |
|--- |---: |---: |---: |
| `Count` | 12.73 ns, 0 B | 12.66 ns, 0 B | 14.07 ns, 0 B |
| `Enumerate` | 23.75 ns, 40 B | 23.78 ns, 40 B | 26.91 ns, 40 B |
| `KeyChange` | 498.71 ns, 1,183 B | 626.67 ns, 1,169 B | 2,274.51 ns, 1,169 B |
| `SourceAddAndRemove` | 6,842.87 ns, 10,867 B | 7,506.53 ns, 10,910 B | 9,723.75 ns, 10,992 B |

Forty bytes flat, one and a half nanoseconds per grouping. The mechanism works in the collection family too.

The two change rows are the ones worth the ink. Until this benchmark existed, the cost of invalidation could only be argued analytically — three null assignments in a handler that already runs. `KeyChange` allocates 1,183, 1,169 and 1,169 bytes across the three sizes and `SourceAddAndRemove` 10,867, 10,910 and 10,992. Flat, and dominated by the regrouping work they already did. **Invalidation costs nothing measurable.**

## What was deliberately left alone

`ObservableCollectionLookupQuery` has four copy sites and only one was taken. Its `Keys`, `Values` and second enumerator all read `groupingByKey`, an `IObservableDictionaryQuery` for which this class subscribes to no change event. It is likely that `GroupingsCollectionChanged` covers it, since `groupingByKey` derives from the same groupings — but a cache invalidated on a likelihood serves stale data, and a stale read is worse than a slow one. Those three sites stay uncached until someone can name the event that clears them.

The four `UsingSynchronizationContext` wrappers also copy, inside a `Send` across a thread boundary. That is a different problem and the snapshot there is probably load-bearing.

## The trade

One retained copy per query between changes, where before it was one allocated copy per read. A query read more than once between mutations wins; one read once and abandoned loses slightly. The library had already made this bet in four classes, so this follows precedent rather than setting it. Nothing here measures resident set — the instrument for that does not exist, and the one attempt at building it was withdrawn.

## A note on method

Three claims made while surveying this work were wrong, and all three failed the same way: they were derived from a staging directory assembled from whenever each file had last been copied, and treated as if it were the repository. The benchmark-coverage correlation, the eighty kilobytes per enumeration, and the list of eleven classes needing the fix were all artifacts of stale files. `ObservableCollectionOrderByQuery` had already been fixed; its measured allocation was forty bytes, flat, the whole time.

A rule about re-reading from disk before *editing* had been in force for a day. It did not cover reading before *concluding*, which is where conclusions come from. It does now.
