# Benchmark comparison, 31 August 2026 — what a graph observation spends on structural hashing

Three arms in one class, so the subtraction is within-run.

| | mean | allocated |
|--- |---: |---: |
| `BuildNormalizedOnly` | 59.70 ns | 184 B |
| `BuildThenDiagramEveryNode` | 1,072.69 ns | 1,171 B |
| `GraphObservation` | 2,565.57 ns | 3,271 B |

Three launches, `person => person.Rank * 2`, `UseDirectSubscription = false`.

Diagram generation is the difference between the first two: **1,012.99 ns and 987 bytes**. Against a whole graph observation that is **39.5% of the time and 30.2% of the allocation**.

Both figures are floors. The arm hashes only the two subtrees the node caches probe through `ExpressionEqualityComparer` — the binary and its member operand. The constant caches probe through `ConstantExpressionExpressionEqualityComparer`, which this does not count.

## Why the memo cannot help here

`ExpressionEqualityComparer` memoizes each diagram in a static `ConditionalWeakTable<Expression, Diagram>`, keyed by expression instance. That makes a repeated probe with the *same* instance nearly free, which is why a `Where` cache hit over a held selector costs 135 ns in total and cannot contain a diagram generation at all.

The graph gets no such relief. Its node caches are keyed by the **parameter-replaced** tree, and `ReplaceParameters` builds a fresh one for every observation. Every probe is therefore a guaranteed miss on a brand-new instance, and the memo does worse than nothing there: it allocates a table entry for a diagram used once and then collected with its key.

So the cost is not structural hashing in general. It is structural hashing of throwaway trees, which is a property of how the graph is keyed rather than of the comparer.

## Three claims about this, scored

The first, from `BuildThenHashSelectorLambda` at 681 ns against `GraphSelectorObserve` at 1,797 ns, was that structural hashing dominates graph construction. Right about the place, silent about the size.

The second was that the first was wrong, because a `Where` cache hit costs 135 ns and cannot hold a 681 ns hash. Right about the query layer, and it correctly relocated the cost to the graph's per-observation probes rather than abandoning it.

The third is this measurement: 39.5% and 30.2%, floors. The direction survived twice and now has a number.

## Where a fix would go

`GetHashCode` builds and memoizes a complete `Diagram` — a materialized `IReadOnlyList<object?>` with boxed node types — when only the hash is wanted. The elements are needed by `Equals`, but only for the operands that actually get compared, and on the graph's path the probe side is transient while the cached side persists and memoizes usefully.

A streaming hash that materializes and memoizes nothing for the transient side, deferring the element list to the comparisons that need it, would take most of the 987 bytes and some of the 1,013 nanoseconds. It changes the internals of a public type without changing its surface.

That belongs on its own branch. It benefits every graph observation, every query, and every expression shape that stays ineligible no matter how far direct subscription is widened — which makes it worth measuring properly rather than appending to a branch about eligibility.
