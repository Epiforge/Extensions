# The scope in the wrong place

*2026-09-07 — `RefusalPricingBenchmarks` and `ObservationShapeBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

Every graph node subscribed to its source object directly, and each handler opened a `PropagationScope` as its first act. So one event that N nodes care about produced **N propagations**, and each flushed an announcement before the later nodes had been told. A consumer saw values composed of inputs that were never simultaneously current.

```
p => p.Rank + p.Score        graph announced  12, 30      fast path announced  30
h => h.Numbers[0] + h.Numbers[1]   the same  12, 30                            30
```

Both cases move `1, 2` to `10, 20`. The `12` is the new first input added to the stale second. The fast path was right because `DirectSubscriptionRegistry` holds one subscription per source and fans out inside a single scope.

Nothing smaller could fix it. The announcement at flush reads the root's cached evaluation, which is composed from children's caches, so the flush cannot see past a stale sibling; and nothing wraps a source's event dispatch, so no handler can know another is still pending. That knowledge can only exist in a single subscription per source.

## What changed

`SourceNotificationRegistry`, built to the same shape as `DirectSubscriptionRegistry` — one handler per `(source object, event kind)` keyed on reference equality, an attachment linked list with `IsRemoved` flags because a node can dispose while a dispatch is walking it, and one `PropagationScope` opened around the whole fan-out. `ObservableMemberExpression` and `ObservableIndexExpression` attach through it instead of using `+=`, and their handlers no longer open scopes of their own.

## The readings

| arm | before | after |
|---|---|---|
| `RankComparisonGraph` | 2.62 MB / 2,352.1 μs | 2.86 MB / 2,561.5 μs |
| `ConditionalGraph` | 5.05 MB | 5.28 MB |
| `TwoObjectShortCircuitGraph` | 3.95 MB | 4.18 MB |
| `NotifyingChainGraph` | 3.39 MB | 3.85 MB |
| `IndexerReadGraph` | 15.24 MB / 10,640.5 μs | **4.86 MB / 5,027.5 μs** |
| `RankComparisonDirect` (control) | 1.18 MB / 361.6 μs | 1.18 MB / 361.5 μs |
| `IndexerReadDirect` (control) | 1.35 MB | 1.35 MB |

`ObservationShapeBenchmarks` did not move by a byte: 70.31, 93.75 and 117.19 KB, which are the recorded 72, 96 and 120 bytes per change. **The registry is construction-only and costs nothing on the propagation path.**

## What they say

**The registry costs about 230 bytes per distinct source object**, and the model closes across four arms with no residue. `RankComparison` and `Conditional` have one source per element and pay +0.23 MB per thousand; `TwoObjectShortCircuit` adds only a single shared `other` and also pays +0.23; `NotifyingChain` has two sources per element and pays +0.46, exactly twice the unit.

**The indexer arm fell 10.38 MB, and that is not a subscription-count saving.** A thousand index nodes shared one `table`. Every `+=` on a multicast delegate reallocates its invocation list, so attaching a thousand handlers to one event allocates arrays of 1, 2, 3 … 1000 — **the graph's subscription cost was quadratic in the number of nodes sharing a source.** The registry appends to a linked list. That is where 10 MB and half the time went.

So the graph pays 9% where every element is its own source and saves 68% where elements share one, propagation is untouched, and a correctness defect is gone.

## Contract change

The graph now places **one** handler on a consumer's event however many nodes are interested. A consumer with custom event accessors — which is exactly what the `Recorded` test fixture is — sees a different count. Two tests in `SubscriptionAgreement` were counting handlers:

- `TheGraphDoesNotSubscribeToAnUntakenBranchUntilItIsTaken` goes from `2, 3` to `1, 2`. Its deferred branch reaches a *different* object, so the count still rises when the branch is taken and the proposition is untouched.
- `TheGraphsSourcesForAndAlsoOverOneObjectDoNotChangeWhenTheBranchIsTaken` goes from `1, 2` to `1, 1`, because both operands reach the same object. Its named claim is carried by the `CollectionAssert` on distinct entries and survives — but the `1, 2` was quietly doing a second job, showing the right operand subscribed at all. **That evidence was replaced, not deleted**: the test now changes `s.Score` after taking the branch and asserts a notification arrives, which proves the operand is observed rather than that a delegate was added.

Changing those counts to match without replacing the lost evidence would have made the second test pass whether or not the branch ever attached — the vacuous-pass trap, deliberately introduced.

## What guards it

- `MultipleNodesOverOneSource.NoAnnouncedValueIsOneTheObjectWasNeverIn` — two plain properties, one `PropertyChanged(null)`, asserting `30` alone.
- `DifferentialFuzz` — 450 programs comparing both mechanisms on values and notification counts, which is what a change to how every graph notification propagates has to survive.
- `SubscriptionStability` and `SubscriptionAgreement` for attach and detach behavior.

## Left open

The 230 bytes per source is unreduced. A source with exactly one attachment — the common case — still pays for a dictionary entry, a source object and an attachment. Whether that can be collapsed without losing the fan-out is unexamined, and the case for chasing it is weak while the graph serves only refused shapes and explicit opt-outs.
