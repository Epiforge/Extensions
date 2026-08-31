# Benchmark comparison, 31 August 2026 — a reused buffer for diagram generation

`ExpressionDiagramGenerator.GenerateDiagram` allocated a default-capacity `List<object?>`, appended to it as it walked the tree — reallocating at 4, 8, 16, 32 — and handed the oversized list out as the diagram. Node types and booleans were already pre-boxed into shared instances, so the boxing was not the cost. The list was.

It now appends into a `[ThreadStatic]` scratch list, reused across calls, and copies out to an exact-size array. The scratch stops growing after the first few diagrams; each diagram keeps only what it needs. It is cleared at both ends, the trailing clear mattering because a thread-static would otherwise root the last diagram's elements — including constant values — for the life of the thread.

## Result

| | before | after | |
|--- |---: |---: |---: |
| `BuildNormalizedOnly` | 59.70 ns / 184 B | 59.43 ns / 184 B | control |
| `BuildThenDiagramEveryNode` | 1,072.69 ns / 1,171 B | **773.54 ns / 643 B** | |
| `GraphObservation` | 2,565.57 ns / 3,271 B | **2,210.83 ns / 2,689 B** | 1.16× / 1.22× |

By subtraction against the unchanged control, diagram generation for two probes went from **1,012.99 ns and 987 bytes** to **714.11 ns and 459 bytes** — 29.5% of the time and **53.5% of the allocation** gone.

As a share of a whole graph observation, diagram generation falls from 39.5% to **32.3%** of time and from 30.2% to **17.1%** of allocation.

The observation saved 582 bytes where the two-probe arm saved 528. The difference is the probes this arm does not count — the constant caches use a different comparer and generate diagrams too — which is the floor caveat from `2026-08-31-graph-construction-share.md` showing up as a number.

## The prediction

Written before the run: two diagrams of roughly eighteen and eight elements, whose list objects and growth arrays of about 608 and 176 bytes would become exact arrays of about 168 and 88, saving **about 528 of the 987 bytes**.

**Measured saving: 528 bytes.**

That is the arithmetic class — a count of allocations multiplied by their known sizes — rather than a story about which code is hot. Every prediction of that class has held in this series and every prediction of the other kind has failed, and this is the most exact instance of it so far. The time estimate was deliberately left as "modestly", and 29.5% is consistent with that but was not predicted.

## What remains in the 459 bytes

Roughly 256 of it is the two exact-size arrays, which are the diagram and cannot be avoided while `Equals` compares elements. The rest is an `IterationState` per call — allocated even for expressions with no parameters, which is most of them — a `Diagram` object, and a `ConditionalWeakTable` entry per diagram.

That last one is pure waste on the graph path, since the expression it keys is transient and the entry is garbage with it. Removing it would mean knowing at generation time whether the caller intends to keep the expression, which the comparer cannot see. Deferring materialization instead — hashing without elements, and building them only when `Equals` is reached — trades one walk for two on a hit, and hits are the steady state here.

So the cheap remainder is the `IterationState`, and it is small.
