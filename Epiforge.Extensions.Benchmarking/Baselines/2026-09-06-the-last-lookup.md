# Benchmark comparison, 6 September 2026 — the last lookup

`2026-09-06-the-argument-array.md` gave a cached `FastInvoker` to the three node types which build argument lists. It missed a fourth: `ObservableMemberExpression` evaluates a property read as `getMethod.FastInvoke(result, [])`, where the empty array is `Array.Empty` and free, but the `ConcurrentDictionary` lookup in front of it is not. That lookup was measured at 4.73 ns, and a property read is the most common node in the library.

A sweep of `Observable\` for `FastInvoke` found it, and found nothing else. This is unreleased work.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

`ObservationShapeBenchmarks`.

## The measurement

| arm | before | after |
|--- |---: |---: |
| `ChangeEveryValueMemberRead` | 45.808 μs | **40.712 μs** |
| `ChangeEveryValueMethodCalled` | 44.815 μs | 40.437 μs |
| `ChangeEveryValueMethodCalledForAPair` | 71.698 μs | 67.598 μs |
| `ChangeEveryValueMethodCalledForATuple` | 67.170 μs | 63.874 μs |
| `ChangeEveryValueProjected` | 74.188 μs | 68.935 μs |
| `ChangeEveryValueProjectedOverAConstantKey` | 90.395 μs | 68.502 μs |
| `ChangeEveryValueProjectedWithDirectSubscriptionAllowed` | 71.799 μs | 68.160 μs |
| `ChangeEveryValueWithNoObservation` | 8.466 μs | 9.002 μs |

Allocation was byte-identical on all eight arms, which is what it had to be: nothing about what gets allocated changed, and that was the strong control.

`ChangeEveryValueMemberRead` fell **11.1%** against a prediction of about 10%, made by dividing the measured 4.73 ns lookup into the arm's 45.8 ns per change. Every arm reads `pair.Value.Rank` somewhere, so every arm improved.

**The control moved 6.3%, and it moved the wrong way.** `ChangeEveryValueWithNoObservation` observes nothing and executes no changed code, and it went from 8.466 to 9.002 μs. That is the drift for this pair of runs, and it is ten times what the same two controls showed in the previous pair. So the three smallest improvements here — 4.9%, 5.1%, 5.7% — decide nothing on their own; only the member read at 11.1%, the method call at 9.8% and the projection at 7.1% clear it.

## The arm which resolved itself

`ChangeEveryValueProjectedOverAConstantKey` was the open anomaly from the last document: 77.039, then 81.386, then 90.395 μs across three runs on byte-identical allocation, while its structural twin fell. It now reads **68.502 μs**, level with that twin at 68.935.

**The characterization was wrong, and it was wrong in a way this project has already recorded once.** Three ascending points were described as climbing, which implies a trend; a fourth point erases it. The right reading of three points on an arm with no code path of its own was that one of them was an excursion, and the previous document should have said so instead of narrating a direction. The orientation already carries this lesson from `ChangeEveryValueInAWhereQueryWithASubscriber`, where "that arm is the noisy one" was a generalization from a single instance and was wrong when a second arm did the same thing. Same error, opposite sign.

What was right was the decision not to spend a run chasing it. It cost nothing to wait, and the answer arrived free with the next run.

## Where the graph's evaluation has got to today

Per underlying property change, over a thousand elements:

| | start of day | now |
|--- |---: |---: |
| member read | 72 B, 42.408 μs | 72 B, 40.712 μs |
| method call returning an `int` | 128 B, 50.693 μs | 96 B, 40.437 μs |
| projection producing a key-value pair | 256 B (before the boxing round trip), 82.095 μs | 120 B, 68.935 μs |

The member read never allocated an argument array, so its whole gain is the lookup. The projection lost a boxing round trip, an argument array and a lookup, in that order.
