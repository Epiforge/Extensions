# What a raise costs across many sources

*2026-09-09 — `SourceCountBenchmarks`, 1,000 source objects carrying six observations apiece, constructed and disposed, then raised at 100,000 times*

## The question

Every conclusion so far about how a source finds the attachments wanting a reported name was drawn from `NotificationFanOutBenchmarks`, whose subject is one object carrying a thousand attachments. That instrument said the graph handles a raise nobody wants in 6.3 μs where the fast path takes 3,500.8 — a factor of 555 — and a name index over the attachment list was proposed to close it.

The shape that instrument measures is not the shape a grid has. A grid is the mirror image: many objects, each carrying one attachment per column. A structure which makes a long list cheap to search can be pure overhead on a list of six, and it is paid on construction rather than on notification, which is where a grid spends its time. These arms exist so that a change justified by the fan-out instrument has somewhere to be disproved.

## The measurement

| arm | time | allocated |
|---|---:|---:|
| construct and dispose 6,000 observations, fast path | 1.117 ms | 3,536,024 B |
| construct and dispose 6,000 observations, graph | 20.540 ms | 13,725,956 B |
| 100,000 raises of a watched column, fast path | 2.618 ms | — |
| 100,000 raises of a watched column, graph | 8.281 ms | 2,400,000 B |
| 100,000 raises of a column nothing watches, fast path | 2.034 ms | — |
| 100,000 raises of a column nothing watches, graph | 3.812 ms | — |

## The fan-out result does not survive the change of shape

**The fast path wins every arm here, including both raise arms.**

| | fan-out shape | grid shape |
|---|---|---|
| a raise nothing wants | graph by **555x** | **fast path by 1.87x** |
| a raise something wants | graph by 1.85x | **fast path by 3.16x** |
| construction | not measured there | **fast path by 18.39x** |

The graph's advantage on an unwanted raise was never a better dispatch structure. It was node sharing. A thousand observations of the same property of the same object collapse to one member node, so the graph's list held one entry where the fast path's held a thousand; the fast path was walking a thousand names because it caches nothing across observations, and the graph was walking one because it caches everything. Change the shape so that nothing collapses — a thousand distinct objects — and both mechanisms carry six attachments apiece, at which point the graph loses, because what it does with each of them is invoke a handler which decides and returns where the fast path compares a name and moves on.

Per attachment, on a raise nothing wants: **3.39 ns to compare a name against 6.35 ns to invoke a handler**. That is the whole of the dispatch difference, and it runs the other way.

## Where the graph's cost actually sits

Subtracting the unwanted arm from the watched arm isolates what each mechanism does once it has decided a raise matters:

- fast path: **5.84 ns** to invoke the compiled delegate. The fan-out instrument put this at 6.61 ns, which is the same figure on a different subject and is the cross-check that these two instruments are measuring the same thing.
- graph: **44.69 ns** to re-read the member, re-evaluate the comparison, box the result and run the propagation scope.

The fan-out instrument priced a graph node at 5.45 ns, which looked like the graph being eight times cheaper per node than the fast path is per delegate. It is not the same quantity. **The graph's propagation has a fixed cost near 39 ns and a marginal cost near 5.45 ns per node.** In the fan-out shape that fixed cost is paid once per raise and divided across a thousand nodes; in the grid shape it is paid on every raise of every object. That single distinction explains both results, and it was invisible while only one shape was instrumented.

## Allocation

The graph allocates **24 bytes on every watched raise** — 2,400,000 B across 100,000 of them, one boxed value each, the identical figure the fan-out instrument reported. The fast path allocates nothing on any raise in either shape. A five-thousand-row grid ticking once a second costs the graph about 120 KB per second in boxes alone and the fast path zero.

On construction the fast path spends **589.3 bytes per observation** against the graph's 2,287.7. That is also 399.1 bytes below the 988.4 the fast path spends per observation in `FormulaShapeBenchmarks`, and the difference is the shared registration working as intended: one `DirectSubscriptionSource` per object amortized across the six columns attached to it, where an instrument giving every observation its own subject pays for one apiece.

## What this does to the case for a name index

It removes its only measured beneficiary from the shape an application actually has.

The index would make an unwanted raise O(1) on a source carrying many attachments. Below the threshold it changes nothing by construction, so on these arms it is worth **zero** — correctly, that is what the threshold is for. On the fan-out arm it is worth about 350x. So the index is a large win on exactly one shape and nothing anywhere else, and whether that shape exists outside a benchmark is now the whole question.

The shape it needs is specific: **one object carrying many attachments which raises names most of those attachments do not want.** A shared clock read by every row is the first half of it. Whether it is also the second half — whether such an object announces anything besides the property everything is watching — is not something an instrument can answer.

The cliff is real regardless. A library should not carry an O(N) unwanted raise on a plausible shape when removing it is contained in one class and provably free below a threshold. But the justification is now a scaling argument rather than a measured saving, and it should be labelled as one.

## Predictions scored

**The guard held and the headline was missed.** These arms were built to falsify a threshold — to catch a name index quietly regressing construction on the shape GS5 has. They were not built to overturn the fan-out conclusion, and no prediction was recorded about which mechanism would win them, because the fan-out result made it feel settled. It was not settled; it was one shape.

That is the same error as the two wrong fan-out predictions and the sixteen-byte regression: **reasoning about one layer while another decided the result.** Here the deciding layer was node sharing, one level below dispatch, and the instrument that would have exposed it was the one built as an afterthought to guard a change that now may not happen.

## What has not been measured

Whether any real application has an object carrying many attachments that raises names most of them do not want, which is the only shape the index pays for. What the index costs to build and maintain at the threshold. Anything above six columns per object or below a thousand objects — both counts are fixed here, so the crossover between the two shapes is unlocated.
