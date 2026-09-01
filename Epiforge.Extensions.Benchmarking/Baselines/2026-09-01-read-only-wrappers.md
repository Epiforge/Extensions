# Benchmark comparison, 1 September 2026 — a wrapper nobody can see, and a copy nobody needs

`OrderedHashSet.CopyTo(array, arrayIndex, count)` built an intermediate list, wrapped it in a read-only view, and copied out of the wrapper. Seven such wrappers were removed across the libraries earlier today, all of them created and discarded inside the same expression. This measures what that was worth, and whether removing the intermediate list as well is worth doing next.

Three arms, `DefaultJob`, no parameters. An eight-arm version of this benchmark was written first and rejected as roughly an order of magnitude too expensive for what it would settle; four of its arms existed to confirm that a `ReadOnlyCollection<T>` is one small object per call, which is a constant rather than a behavior and does not need an instrument.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

Ten thousand elements, five thousand copied.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ReadOnlyWrapperBenchmarks*
```

## Result

| arm | mean | standard deviation | ratio | allocated |
|--- |---: |---: |---: |---: |
| `CountedCopyToByWalk` | **4.334 μs** | 0.0474 μs | 0.47 | **0 B** |
| `CountedCopyToThroughTheSet` | 9.160 μs | 0.0968 μs | 1.00 | 20,160 B |
| `CountedCopyToWrapped` | 9.505 μs | 0.2088 μs | 1.04 | 20,160 B |

`ThroughTheSet` is the public method as it now stands, not a reconstruction of it. `Wrapped` is the expression it replaced. `ByWalk` is a linked-list walk writing straight into the destination, which is not in the library.

Every standard deviation is under 2.2% of its mean, so all three are quotable.

## What it decides

**The walk is worth writing.** It is 2.11× faster and allocates nothing at all, against 20,160 bytes for a five-thousand-element copy. That is the whole of the intermediate list, and it is the only figure here large enough to matter.

## The predictions, two of which were wrong

Recorded before the run.

**"`CountedCopyToByWalk` allocates zero and is several times faster."** Half right. Zero allocation held exactly. *Several times faster* did not — it is 2.11×, which is a real win but not what "several" claims. The copy it avoids is a bulk `Array.Copy` of contiguous ints, which is close to the fastest thing a machine does; what the walk actually saves is the allocation and the pointer-chasing that fills the list, not a large multiple of work.

**"`ThroughTheSet` allocates roughly 40 KB."** Wrong by a factor of two. It allocates 20,160 bytes. Five thousand `int` values are 20,000 bytes, and the remaining 160 is the `List<int>` and the LINQ iterators. I evidently reasoned in eight-byte units for a four-byte type. This is the corollary about doing the arithmetic, failed on the easiest possible case.

**"`Wrapped` minus `ThroughTheSet` is 24 bytes, being the `ReadOnlyCollection<T>`."** Not confirmed. **The two arms allocated identically — 20,160 bytes each.** The wrapper did not show up at all.

That figure was the reason the arm existed, so its absence is the most interesting thing here and it is recorded unexplained rather than explained away. Two candidates:

- .NET 10 stack-allocated the `ReadOnlyCollection<T>`, which never escapes the expression that creates it. This would fit precisely: the wrappers removed today were selected for exactly the property — not escaping — that would make them eligible.
- The instrument did not resolve 24 bytes against 20,160. This is the weaker candidate, since BenchmarkDotNet reports whole bytes per operation and 20,184 would have been distinguishable from 20,160.

The first is a claim about the runtime rather than about this codebase, which is the category this series has been wrong about most often, so it stays a hypothesis. An arm allocating a wrapper and returning it would settle it, and has not been run.

The 3.8% time difference between the two arms is 1.65 standard deviations of the noisier one. It is not evidence of anything.

## What this says about the seven removals

They are not measurably faster. At this scale the wrapper is either free or below resolution, and the same will be true at every scale where the thing being wrapped is a copy of the data.

They were still worth making — a wrapper that protects a private local from nobody is dead work, and the `Cast<T>()` removed alongside it was applied to a `LinkedList<T>` that already satisfies `IEnumerable<T>`. But no performance claim should be attached to them, and the release note for them, if one is written, should say what they are rather than what they save.

The prize was always the intermediate list, and the benchmark says so plainly.
