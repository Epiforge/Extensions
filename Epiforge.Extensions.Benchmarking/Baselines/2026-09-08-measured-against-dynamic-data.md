# Measured against DynamicData

*2026-09-08 — `DynamicDataComparisonBenchmarks`, DynamicData 9.4.33, DefaultJob, MemoryDiagnoser, 1000 elements*

## What is being compared

A live filtered view of a collection which stays correct as the elements' ranks change. Both libraries are asked for the same behavior and not for the same code: this one reads the dependency out of the predicate, and DynamicData is told it by `AutoRefresh`. Both bind their results into a collection. The unobserved arm is what the changes cost before either of them sees anything, and every figure below is quoted against it.

DynamicData is measured on both of its shapes, because they are not interchangeable and the difference between them turned out to be the largest number in the table.

| arm | mean | allocated |
|---|---|---|
| `ChangeEveryRankUnobserved` | 9.343 μs | 46.88 KB |
| `ChangeEveryRankWithExpressions` | 16.682 μs | **46.88 KB** |
| `ChangeEveryRankWithDynamicDataCache` | 201.698 μs | 640.81 KB |
| `ChangeEveryRankWithDynamicDataList` | 30,147.094 μs | 134,382.67 KB |
| `ConstructAndDisposeWithExpressions` | 293.912 μs | 965.19 KB |
| `ConstructAndDisposeWithExpressionsFromAFreshPredicate` | 358.435 μs | 973.35 KB |
| `ConstructAndDisposeWithDynamicDataCache` | 2,168.656 μs | 4,119.34 KB |
| `ConstructAndDisposeWithDynamicDataList` | 2,221.164 μs | 3,741.56 KB |

## The list path is a finding about DynamicData, not a comparison

**DynamicData's list path costs 210x what its own cache path costs** for the same thousand property changes — 134,382.67 KB against 640.81. At 134 KB per single change over a thousand elements, that is about 137 bytes per element per change, which is the signature of re-processing the whole collection every time. The first run of this instrument had only the list arms, and a result that lopsided was treated as a statement about the harness rather than about the library; adding the cache arms is what established which it was.

**It is the harness in the sense that matters for a comparison**: `ToObservableChangeSet()` on an `ObservableCollection<T>` followed by `AutoRefresh` and the bare `Func<T, bool>` overload of `Filter` is not how DynamicData is meant to be used, and the cache is the API it leads with. It is not the harness in the sense that would make the number wrong: that is what a caller who adapts an existing `ObservableCollection` this way will actually pay. **Report it as a caveat about an idiom, never as a scoreboard entry, and invite correction** — this was written by someone who does not use the library, and the harness is in the repository for anyone who wants to fix it.

## Propagation, against the cache

Subtracting the 46.88 KB and 9.343 μs both libraries pay before either sees anything:

| | allocated per change | time per change |
|---|---|---|
| this library | **0 bytes** | 7.3 ns |
| DynamicData's cache | 608 bytes | 192.4 ns |

**A property change which does not alter membership allocates nothing here and 608 bytes there**, and that difference is architectural rather than incidental. DynamicData's model is a stream of change sets, so a refresh materializes a change set object; this library's subscription re-evaluates the predicate in place and raises nothing when the result does not move. Neither is a defect. **The right sentence for a reader is that one library pays per change and the other pays per membership change**, not that one is faster.

**The workload is a mild one for both, and this should be said.** Ranks are exclusive-ored with 2, so only two elements of a thousand cross the `Rank > 0` boundary in each round. That is realistic — most property changes do not flip a filter — but a workload where every change flips membership would look different for both libraries and has not been measured.

## Construction

**DynamicData costs 4.3x the allocation and 7.4x the time to construct**, 4,119.34 KB against 965.19 and 2,168.656 μs against 293.912. Its two shapes cost about the same as each other here, the cache slightly more.

That figure is conservative in DynamicData's favour: **the cache arms build their pipeline over a cache already populated**, because holding a cache is the analogue of holding a collection rather than of building a query, while the list arms include adapting the collection because there is no way to hold a list-shaped change set without doing so. Populating the cache is not charged to anything.

**A predicate written inline rather than hoisted costs 8.16 KB and 64.5 μs more** — 973.35 against 965.19 KB, 358.435 against 293.912 μs. That is 22% more time and 0.8% more allocation for rebuilding, analyzing and compiling the expression tree at every construction. DynamicData has no equivalent figure, because its lambda is a delegate the compiler caches in a static field, and none was invented for it.

## The prediction, which was wrong in both directions

Recorded before the first run: *DynamicData is cheaper to construct, possibly much cheaper, because it has no expression analysis and no per-element observation object; the propagation arms are closer, and this library may be ahead on allocation there.*

**It is 4.3x more expensive to construct, not cheaper.** And propagation is not closer — it is the widest gap in the table, because this library's own share of it is zero and no ratio expresses that. Both halves were reasoned architecturally about a library nobody here uses, and both came out backwards. **That is the entire justification for building the harness rather than writing the paragraph.**

## What has not been measured

A workload where every change flips membership. Ordered and grouped views against DynamicData's `Sort` and `Group`. Any keyed workload where this library's dictionary queries would be the comparison. Whether a more efficient list idiom exists — `FilterOnProperty`, a throttled `AutoRefresh`, or a filter policy — which would raise the list figure off the floor it is currently on.
