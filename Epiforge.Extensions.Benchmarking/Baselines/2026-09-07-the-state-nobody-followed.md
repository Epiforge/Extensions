# The state nobody followed

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

Admitting the notifying chain gave `DeferringDirectObservableExpression` four new reference fields — `attachedLinks`, `linkAttachments`, `links` and `linkSites` — and every observation of a deferred operand began carrying them whether or not it followed anything. Two of the twelve arms hold that shape and nothing else: `TwoObjectConditionalDirect` and `TwoObjectShortCircuitDirect` each defer an operand over a second object and have no link at all. Both rose from 1.31 MB to 1.34 in the chain commit, which is 32 bytes per element on the nose: four references at 8 bytes, one observation per element, a thousand elements, 32,000 bytes, 0.031 MB, printed as 0.03.

The state was split in two. `DeferringDirectObservableExpression` now holds only the group bitmask and the reached flags; `LinkingDirectObservableExpression` derives from it, adds the four link fields, and overrides the two members which touch them. `DirectObservation` picks among three classes rather than two.

## What was measured

Three runs of the same twelve arms. The middle column is the commit two below the chain admission, checked out to settle where an unexplained movement in the graph arms came from; the outer two are before and after the split.

| arm | at the optimizer commit | chain admitted | after the split |
|---|---|---|---|
| `ConditionalDirect` | 1.14 MB | 1.14 MB | 1.14 MB |
| `ConditionalGraph` | 5.21 MB | 5.24 MB | 5.24 MB |
| `IndexerReadDirect` | 1.29 MB | 1.29 MB | 1.29 MB |
| `IndexerReadGraph` | 4.79 MB | 4.83 MB | 4.83 MB |
| `NotifyingChainDirect` | 3.74 MB | 1.56 MB | 1.56 MB |
| `NotifyingChainGraph` | 3.71 MB | 3.74 MB | 3.74 MB |
| `RankComparisonDirect` | 1.14 MB | 1.14 MB | 1.14 MB |
| `RankComparisonGraph` | 2.78 MB | 2.81 MB | 2.81 MB |
| `TwoObjectConditionalDirect` | 1.31 MB | 1.34 MB | **1.31 MB** |
| `TwoObjectConditionalGraph` | 5.19 MB | 5.22 MB | 5.22 MB |
| `TwoObjectShortCircuitDirect` | 1.31 MB | 1.34 MB | **1.31 MB** |
| `TwoObjectShortCircuitGraph` | 4.11 MB | 4.14 MB | 4.14 MB |

## The prediction, which held

Two arms fall by 0.03 MB and the other ten do not move at all. Ten of ten controls held to the printed digit across the last pair, including all six graph arms, and the two target arms landed exactly where the object layout said they would. An account which predicts a figure and then predicts its removal is worth more than one which only fits afterwards; this is the second such account on this path in two days.

## What the middle column settled, and what it did not

`NotifyingChainDirect` at the optimizer commit reads 3.74 MB against its graph twin's 3.71 and 3,540 μs against 3,750. Exact agreement between a `Direct` and a `Graph` arm means the fast path declined the shape, so the middle column really is the older mechanism and not a mislabeled rerun.

The movement it was run to explain is not explained. Every graph arm rose 0.03–0.04 MB between the optimizer commit and the chain admission, and the rise had reproduced to the byte across an identical pair beforehand, which had been taken as proof it was real. The checkout back rewrote exactly nine files in the two libraries and nothing under `Observable\Query\` or in Collections; a diff of all eight code files put every change inside the fast path except two statements in `SourceNotificationOwner`, where `Volatile.Write(ref last.Next, …)` became `last.Next = …`. Those allocate nothing, and they run only when a second attachment is made to one source and one event — which `person => person.Rank > 0`, the arm that moved 2.78 to 2.81, never does. **The code that arm executes is identical at both commits.**

So reproducibility across a pair of runs is evidence about that pair and not about the build, and the movement is recorded as an excursion rather than a regression. The instrument is the next thing to change, not the code: `MemoryDiagnoser` prints megabytes to two decimals, so at a thousand elements this whole question is about a third of one printing step. It should be asked again at a resolution that can answer it, in a pass of its own.

## Times

Nothing is readable. On byte-identical arms the last pair drifted from −14.6% (`TwoObjectShortCircuitGraph`) to +4.9% (`RankComparisonGraph`), a band of nearly twenty points, and `NotifyingChainDirect` moving from 498.1 to 519.0 μs sits inside it. The split does add two virtual calls per evaluation on the deferring path, so there is a mechanism available if a future pair with a tighter band still shows it; this pair cannot say.

## What was left

A link-only observation still derives from the deferring class, so it holds a group bitmask and a reference to an empty flag array it never reads — 16 bytes. Removing those means a third copy of the evaluation loop, and it was judged not worth it. `NotifyingChainDirect` at 1.56 MB carries them.
