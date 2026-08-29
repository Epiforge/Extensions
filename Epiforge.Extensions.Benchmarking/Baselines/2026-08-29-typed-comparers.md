# Benchmark comparison, 29 August 2026 — typed comparers

`FastEqualityComparer` and `FastComparer` cached a `MethodInfo` for `EqualityComparer<T>.Default.Equals` and `Comparer<T>.Default.Compare` and reached them through `FastInvoke`. That takes `params object?[]`, so every comparison allocated a two-element argument array and boxed the returned `bool` or `int` — sixty-four bytes and about fourteen nanoseconds to answer a question the runtime can answer in a fraction of one.

`ObservableExpression`'s evaluation setter calls the equality comparer once per attempted change, on every node. That put the cost squarely in the middle of the hot notification path of the whole Expressions library, where it had been hiding behind the word "Fast".

This is not a release baseline. It measures unreleased code on the `typed-comparers` branch.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-OEWQXS : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *FastComparisonBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFanOutBenchmarks*
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObserveOrderByBenchmarks*
```

## What changed

Each class now holds a single field: a shim instantiated once per type. An abstract `TypedComparer` declares the operations against `object`; a `sealed TypedComparer<T>` implements them by calling `EqualityComparer<T>.Default` or `Comparer<T>.Default` directly, and the constructor builds one with `Activator.CreateInstance` over a `MakeGenericType`. A call is then a virtual dispatch and two unboxes. No argument array, no boxed return, no reflection at the call site, and no IL emit or expression compilation that would trouble trimming.

The public surface did not move, so package validation had nothing to say.

## FastComparisonBenchmarks

The default rows call `EqualityComparer<int>.Default` and `Comparer<int>.Default` directly. They fold to nothing measurable, so read them as a floor that does not exist rather than as a comparison.

| | before | after |
|--- |---: |---: |
| `EqualityComparerEquals` | 14.0015 ns, 64 B | **0.4439 ns, 0 B** |
| `ComparerCompare` | 14.4462 ns, 64 B | **0.4444 ns, 0 B** |
| `EqualityComparerGetHashCode` | 10.8071 ns, 56 B | **0.2049 ns, 0 B** |
| `DefaultEquals` | 0.0093 ns | 0.0032 ns |
| `DefaultCompare` | 0.0011 ns | 0.0070 ns |

Thirty-two times faster on the two comparisons, fifty-three on the hash code, and every byte gone. The sixty-four was a forty-byte `object[2]` plus a twenty-four-byte boxed return; the hash code's fifty-six was a thirty-two-byte `object[1]` plus the same. The residual four hundred picoseconds is the virtual call and the unboxes.

## QueryFanOutBenchmarks

| | before | after |
|--- |---: |---: |
| `ChangeTheSharedValue` | 21.40 μs, 86.26 KB | **7.400 μs, 23.7 KB** |
| `ConstructAndDisposeWithFanOut` | 2,958.78 μs, 4,074.11 KB | 2,609.76 μs, 3,889.54 KB |
| `ConstructAndDisposeWithoutFanOut` | 2,211.75 μs, 3,498.13 KB | 2,416.67 μs, 3,323.68 KB |

**Sixty-five percent of the time and seventy-three percent of the allocation**, on the benchmark that has stood in all day for a thousand elements reacting to one shared value. A thousand comparison nodes re-evaluate on that change and each one asked the reflection comparer whether its answer had moved.

Construction shed about a hundred and eighty kilobytes on both rows, which is each node's first evaluation paying the same toll once. The without-fan-out row reads slower in time, but its standard deviation is 113.74 μs against the difference of 205 — that row has never been trustworthy for timing and this does not change it.

Every figure recorded in `2026-08-29-wrapper-deferral.md` was measured with this cost included. Those comparisons remain valid against each other, since the toll was constant across them, but the absolute numbers there are now historical.

## ObserveOrderByBenchmarks

`FastComparer` needed a consumer measured rather than a microbenchmark. `ObserveOrderBy` compares through `ObservableCollectionOrderingComparer`, and the most recent recorded figures for it are from 28 August.

| | before | after |
|--- |---: |---: |
| `KeyChange`, 100 | 1,717.1 ns, 1341 B | 1,550.48 ns, 1085 B |
| `KeyChange`, 1,000 | 2,657.5 ns, 1344 B | 2,477.09 ns, 1088 B |
| `KeyChange`, 10,000 | 7,181.5 ns, 1344 B | 6,574.77 ns, 1088 B |
| `Enumerate`, 10,000 | 8,244 ns, 40 B | 8,261.48 ns, 40 B |

Two hundred and fifty-six bytes at every size — four comparisons at sixty-four bytes each — and seven to ten percent of the time. `Enumerate` did not move, which it must not, since enumerating compares nothing.

The flatness is the finding. A key change does not re-sort; the ordering repairs locally, and a repair costs four comparisons whether the collection holds a hundred elements or ten thousand. The obvious story before measuring — that sorting compares O(n log n) times, so this should be enormous — was wrong, and no number was predicted for this row precisely because that story could not be checked without reading code that had not been read.

The recorded before predates the `wrapper-deferral` merge, so the interval contains another change. What makes the attribution sound is not the size of the delta but its shape: four times sixty-four, identically, at three collection sizes.

## A note on method

The prediction that this would take `ChangeTheSharedValue` to around seven microseconds and twenty-two kilobytes was written down before the run, and flagged at the time as the one most likely to be wrong, because predictions about where time goes had failed repeatedly during the previous branch. It was the most accurate of the day: fourteen microseconds of savings predicted against fourteen measured, sixty-four kilobytes predicted against sixty-three.

The distinction that actually separates the successes from the failures is not *where* against *why*. It is arithmetic against narrative. Every prediction that multiplied a count by a measured unit cost has held — sixty-four bytes a call times a thousand calls, sixteen bytes a wrapper times a thousand wrappers, forty bytes for an array plus twenty-four for a box. Every prediction that told a story about which code was hot has failed. The lesson is to reach for a number to multiply before reaching for an explanation.
