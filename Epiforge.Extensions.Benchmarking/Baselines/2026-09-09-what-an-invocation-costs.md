# What an invocation costs

*2026-09-09 — `InvocationCeilingBenchmarks`, 1,000 coexisting observations under a filtered query, constructed and disposed*

## The question

`DirectSubscriptionAnalyzer.AnalyzeNode` has no arm for an `InvocationExpression`, so one falls to the default refusal and the whole subtree with it. An application whose formula engine emits invocations therefore runs on the graph however its observer is configured. Before widening the analyzer — the largest eligibility change it would have taken, on the axis where a wrong admission costs correctness rather than speed — the question is what admitting it could possibly be worth.

Two arms replicate figures already on record so the instrument can be checked before the other six are read.

## The measurement

Per element, construction and disposal:

| arm | allocated | time | bytes per element |
|---|---:|---:|---:|
| a comparison, fast path — control | 965.19 KB | 295.4 μs | **988.4** |
| the same shape inlined, fast path | 965.19 KB | 300.6 μs | **988.4** |
| nested and inlined, fast path | 965.19 KB | 295.8 μs | **988.4** |
| a comparison, graph — control | 2,729.55 KB | 2,280.2 μs | 2,795.1 |
| a method call, graph | 3,932.67 KB | 4,067.5 μs | 4,027.1 |
| one invocation, graph | 6,818.35 KB | 7,138.7 μs | 6,982.0 |
| one invocation, default options | 6,818.33 KB | 7,360.9 μs | 6,982.0 |
| two invocations, graph | 10,560.05 KB | 12,218.9 μs | 10,813.5 |

## The refusal is proved, not assumed

**The default-options arm lands on the graph arm at 6,818.33 KB against 6,818.35** — two hundredths of a kilobyte apart on an arm whose own deviation is 4%. The analyzer refuses the shape and the default observer runs the graph, so every figure here is a ceiling rather than a saving already taken.

## The fast path charges for sources, not for nodes, and the arms say so to the byte

**Three shapes of different node count allocate 965.19 KB — the same figure to the hundredth of a kilobyte.** A bare comparison, a method call over it, and a nested pair of operations all cost the fast path exactly what one member read costs, because what it charges for is the subscription to a source and there is one source in each. That was predicted and it held on all three arms.

## The graph charges per invocation, and the cost is additive

- One invocation over the bare comparison: **4,186.9 bytes per element**.
- The second invocation: **3,831.5**, slightly less because its body is a unary where the first is a method call.
- For contrast, an ordinary method-call node is **1,232.0**.

**So an invocation costs the graph roughly 3,800 to 4,200 bytes per element, about 3.3x what a method call costs**, and it is paid again for every invocation in the expression. That additivity was the load-bearing prediction and it held; it is what makes the figure multipliable rather than a single data point.

## What it is worth

| | memory | time |
|---|---:|---:|
| one invocation, graph against fast path | **7.06x** | **23.7x** |
| two invocations | **10.94x** | **41.3x** |

Extrapolating the measured per-invocation cost — arithmetic on two points, not a measurement at depth — an expression carrying five invocations would cost the graph about 22,000 bytes per element against a flat 988.4, and one carrying ten about 41,000. The graph arms all reach Gen2; none of the fast path arms do.

## Predictions scored

**The two that mattered held.** The default arm landing on the graph arm to the printed digit, and the fast path staying flat across node counts — predicted at "within 0.02 of each other" and measured identical to the byte on three arms.

**The two magnitudes were both wrong, and both low.** One invocation was predicted at about 5.48 MB from a recorded 2,700-byte node cost, and measured 6.66 — the invocation is not one node but a node plus the sub-observation of the body it reduces to. The inlined graph arm was predicted at 3.0 to 3.4 MB and measured 3.93; that band was labelled a guess when it was made, because no construction-path unit cost for a method-call node was on record. It is now 1,232.0 bytes per element.

**A note on the controls.** The fast path control reproduces its recorded floor within 0.5%. The graph control comes in about 4% below its last recorded figure, which is more than the drift band for a graph arm; the likely cause is the construction-path work landed since that figure was taken, and it has not been verified.

## What has not been measured

What the reduction costs to perform, which is paid once per observation construction and is subtracted from every figure above. Whether the subscription set a reduced expression takes is the same one the graph takes for the invocation — the premise the whole widening rests on, and a test rather than a benchmark. Anything at a depth beyond two.

## The after — the ceiling was taken in full

An invocation of a literal lambda is now reduced to the body it would have evaluated, between the optimizer and the analysis, so what is planned and what is compiled are one tree containing no invocation. Same instrument, same day.

| arm | before | after |
|---|---:|---:|
| one invocation, default options | 6,818.33 KB / 7,360.9 μs | **965.19 KB / 293.0 μs** |

**It lands on all three fast path arms at 965.19 KB, to the hundredth of a kilobyte**, which is the same figure a bare `person.Rank > 0` costs. The prediction recorded before the run was 965.19 and about 300 μs.

**7.06x less memory and 25.1x less time** for the shape a formula engine emits. The arm also leaves Gen2 entirely — it reported 15.6250 collections before and none after, so these observations stop being promoted.

**Seven of eight arms are controls and held.** Six are byte-identical; `InvocationGraph` moved 2.52 KB, which is 0.037% and inside the drift band for a graph arm. Every other arm pins its observer explicitly, so only the default-options arm could move, and only it did.

## What this means at the depth an application actually reaches

The graph charges 4,186.9 bytes per element for the first invocation and 3,831.5 for the second. The reduced form charges nothing for either: the fast path allocates 965.19 KB for a bare comparison, for a method call over it, for a nested pair, and now for an invocation. **An expression carrying ten invocations was costing roughly 41,000 bytes per element and now costs 988.4**, flat, because what the fast path charges for is the subscription to a source and the number of those did not change.

**One gap, named rather than glossed:** the nested case has no default-options arm. That it also lands at 965.19 is an inference from the hand-inlined nested arm plus the test proving the nested invocation is now served by the fast path — sound, but not measured. One arm would close it.

## Predictions scored

Both exact. The figure and the arm it would land on were predicted to the hundredth of a kilobyte, and the controls were predicted not to move. **That is the third consecutive prediction on this instrument built by adding measured unit costs, and the third to land** — against the two magnitude predictions earlier in this document which reasoned from a node count and were both wrong and both low.
