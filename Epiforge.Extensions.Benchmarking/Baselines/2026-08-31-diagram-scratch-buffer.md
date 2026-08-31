# Benchmark comparison, 31 August 2026 — a reused buffer for diagram generation

`ExpressionDiagramGenerator.GenerateDiagram` allocated a default-capacity `List<object?>`, appended to it as it walked the tree — reallocating at 4, 8, 16, 32 — and handed the oversized list out as the diagram. Node types and booleans were already pre-boxed into shared instances, so the boxing was not the cost. The list was.

It now appends into a `[ThreadStatic]` scratch list, reused across calls, and copies out to an exact-size array. The scratch stops growing after the first few diagrams; each diagram keeps only what it needs. It is cleared at both ends, the trailing clear mattering because a thread-static would otherwise root the last diagram's elements — including constant values — for the life of the thread.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *GraphConstructionShareBenchmarks*
```

## Result

The shipped code was measured twice, in separate sessions, for reasons the last section explains. Both replications are reported; neither is discarded.

| | before | after, first | after, second |
|--- |---: |---: |---: |
| `BuildNormalizedOnly` (control) | 59.70 ns / 184 B | 59.43 ns / 184 B | 61.00 ns / 184 B |
| `BuildThenDiagramEveryNode` | 1,072.69 ns / 1,171 B | 773.54 ns / 643 B | 747.74 ns / 643 B |
| `GraphObservation` | 2,565.57 ns / 3,271 B | 2,210.83 ns / 2,689 B | 2,127.13 ns / 2,689 B |

**Allocation is byte-identical across the two replications**, which is what makes them replications of the same code and not of two builds. The times differ by 3.3% and 3.8%, which is the between-run spread of this instrument and is why the derived time figures below are given as a range.

By subtraction against the control, diagram generation for two probes went from **1,012.99 ns and 987 bytes** to **687–714 ns and 459 bytes** — 29.5% to 32.2% of the time and **53.5% of the allocation** gone.

As a share of a whole graph observation, diagram generation falls from 39.5% of time to **32.3%** — the same figure in both replications, because the two arms drifted together — and from 30.2% of allocation to **17.1%**.

The observation saved 582 bytes where the two-probe arm saved 528. The difference is the probes this arm does not count — the constant caches use a different comparer and generate diagrams too — which is the floor caveat from `2026-08-31-graph-construction-share.md` showing up as a number.

## The prediction

Written before the run: two diagrams of roughly eighteen and eight elements, whose list objects and growth arrays of about 608 and 176 bytes would become exact arrays of about 168 and 88, saving **about 528 of the 987 bytes**.

**Measured saving: 528 bytes, in both replications.**

That is the arithmetic class — a count of allocations multiplied by their known sizes — rather than a story about which code is hot. Every prediction of that class has held in this series and every prediction of the other kind has failed, and this is the most exact instance of it so far. The time estimate was deliberately left as "modestly", and the measured 29.5–32.2% is consistent with that but was not predicted.

## A second change, made and withdrawn

The `IterationState` allocated per call was then made thread-static with a `Reset()`, on the same reasoning as the scratch list. It saved 74 bytes against a prediction of 64, and it is not in the shipped code, because it coincided with the instrument going bad.

| run | `BuildThenDiagramEveryNode` | `GraphObservation` |
|--- |---: |---: |
| original | 1,072.69 ns, 1.3% StdDev | 2,565.57 ns, 1.4% |
| scratch list | 773.54 ns, 2.0% | 2,210.83 ns, 1.7% |
| + thread-static `IterationState` | 1,006.19 ns, 1.8% | 2,110.38 ns, 4.7% |
| unchanged code, run again | 1,396.59 ns, **18.8%** | 2,144.13 ns, 5.1% |
| `IterationState` reverted | 747.74 ns, 2.4% | 2,127.13 ns, 1.3% |

An instrument that answers 774, 1,006 and 1,397 for code that got strictly cheaper is not reporting on the code. The revert was made as a diagnostic with the outcome stated in advance: if the second change was the cause, the arms return to roughly 774 ns at about 2% and 2,211 ns at about 1.7% with allocation back to 643 and 2,689; if the machine had drifted instead, the times stay high and loose, and every timing figure recorded that evening would need re-reading.

They returned — 747.74 ns at 2.4% and 2,127.13 ns at 1.3%, allocation exactly 643 and 2,689. Three tight runs without the change, two anomalous runs with it, one clean intervention. The association is as good as this instrument gets.

**No mechanism is claimed.** Reusing a small object and clearing a dictionary should not cost 18% variance, and nothing in the change suggests why it would. What is recorded is the association and the fact that the shipped code measures tight; 74 bytes is not worth spending an unexplained instrument failure on. The wider conclusion the failed branch of the prediction would have forced — that the evening's other timing figures were suspect — does not apply. `GraphObservation` did settle about 3.8% below its scratch-list reading, so the means carry that much drift, but the noise did not follow it.

## What remains in the 459 bytes

Roughly 256 of it is the two exact-size arrays, which are the diagram and cannot be avoided while `Equals` compares elements. The rest is an `IterationState` per call — allocated even for expressions with no parameters, which is most of them — a `Diagram` object, and a `ConditionalWeakTable` entry per diagram.

That last one is pure waste on the graph path, since the expression it keys is transient and the entry is garbage with it. Removing it would mean knowing at generation time whether the caller intends to keep the expression, which the comparer cannot see. Deferring materialization instead — hashing without elements, and building them only when `Equals` is reached — trades one walk for two on a hit, and hits are the steady state here.

So the cheap remainder is the `IterationState`, it is small, and one attempt at it has already been withdrawn.
