# Two safe cuts to the ordered reposition

*2026-09-08, second sitting — `OrderByKeyChangeStageBenchmarks` and `OrderByMoveDistanceBenchmarks`, before and after, 1000 elements*

## What was changed

Both changes are provably behavior-identical and both were kept.

**The left operand's comparables are taken once per search instead of once per comparison.** `ObservableCollectionOrderingComparer.Compare(x, y)` did two dictionary lookups; across a binary search `x` never changes. `ComparablesOf(element)` and `CompareWithComparablesOf(x, ref xComparables, y)` were added and the public `IComparer<TElement>.Compare` now delegates to them, so the three `OrderBy(..., comparer)` sites are untouched.

**Both binary searches find each node from the one before it rather than from the root**, through `PrefixWeightedSequence.NodeAtFrom`, which decides for itself whether the finger is nearer than the root and descends from the root when it is not. No search can therefore reach a different node than it did before.

`IsFingerCloser` was not touched. Its `d⁴ ≤ n` guard looks conservative, but that is analysis rather than measurement, and it belongs to a type with its own instrument.

## What it bought

| | before | after | |
|---|---|---|---|
| an ordered key change | 1,337.5 ns | **1,279.8 ns** | −57.7 |
| the same, moving 2 positions | 1,336.8 ns | **1,275.9 ns** | −60.9 |
| the same, moving 32 positions | 1,422.7 ns | **1,330.1 ns** | −92.6 |
| the same, moving 512 positions | 1,381.2 ns | **1,294.4 ns** | −86.8 |

**About 4–7%.** Allocation is unchanged at 332.03 KB, exactly as predicted — nothing that allocates was removed. The three earlier stages are unchanged within noise (`KeyAlone` 101.96 against 101.94, `KeyBoxed` 99.13 against 97.21, `KeyPaired` 129.64 against 129.47), which is the control confirming nothing else moved.

The crossover against DynamicData moves from about 1,470 elements to about **1,390**. At a thousand elements this library still loses, by about 1.29x rather than 1.35x.

## The falsifier fired

Recorded before the run: *the win should be larger at short move distances than long ones, because that is where the finger applies; if it improves equally at 2 and 512, the finger is doing nothing and the whole win is the dictionary lookups.*

**The saving is flat across distance and is in fact smallest at distance 2** — 60.9, 92.6, 86.8. **The finger contributed nothing measurable.** Nearly the whole win is the hoisted lookups, and even that came in under the low end of what was predicted for it alone.

Why: a binary search over the whole left or right portion probes far from the finger until its last two or three steps, so `IsFingerCloser` declines almost every probe. The change is still worth keeping — it is free, it can only reduce work, and it will matter more as `n` grows — but it is not what moved the number.

## The predictions, scored, and what that says

- **Predicted 200–350 ns saved. Measured 58–93.** Wrong by three to four times.
- **Predicted the finger would dominate the win. It contributed nothing.**
- Predicted allocation unchanged. Correct.
- Predicted this library would still lose at a thousand elements. Correct.

That is the second consecutive miss on this same question: the stage decomposition was predicted at 350 / 50 / 80 / 850 and measured at 92.8 / −4.7 / 32.2 / 1,217.2. **Two wrong models in a row of where time goes inside one method is not bad luck, it is evidence that this code cannot be optimized by reading it.**

The remaining roughly 1,070 ns of fixed cost is somewhere that reasoning about the structure has now twice failed to locate. **The next attempt should be a profiler against the reposition, not another benchmark built on another guess** — each of those costs a build, a run and a wrong answer.

## What has not been measured

Where the remaining fixed cost actually goes, which now needs sampling rather than arithmetic. Whether `IsFingerCloser`'s `d⁴ ≤ n` guard is well calibrated, which is a question about `PrefixWeightedSequence` and belongs to its own instrument. Whether the finger begins to pay at ten thousand elements or beyond, where the root descent is longer and more of a search's probes fall inside the guard.
