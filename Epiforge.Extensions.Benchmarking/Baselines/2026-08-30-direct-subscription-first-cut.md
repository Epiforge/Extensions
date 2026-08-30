# Direct subscription: the first cut, measured

`DirectSubscriptionCeilingBenchmarks`, .NET 10.0.11, Intel Core Ultra 9 275HX, one launch. Three arms: **graph** (`UseDirectSubscription = false`), **fast** (the mechanism as built), **ceiling** (the hand-rolled stand-in from `2026-08-30-direct-subscription-ceiling.md`).

Per-observation figures throughout, N = 1000 unless noted.

## The headline

**Construction is a regression.** The fast path is slower and larger than the graph it replaces, against a ceiling that is sixty times faster than the graph. That has to be fixed before this can ship, and the option defaulting to on makes it urgent rather than academic.

| construction | graph | fast | ceiling |
|--- |---: |---: |---: |
| selector | 1,964 ns / 2,311 B | **2,752 ns / 3,740 B** | 32 ns / 180 B |
| comparison | 3,891 ns / 4,467 B | **6,601 ns / 6,737 B** | 284 ns / 2,254 B (at N=100) |

1.40× slower and 1.62× larger on the selector; 1.70× slower and 1.51× larger on the comparison. The ceiling says roughly 60× was available.

## What did work

**The registry killed the quadratic, exactly as intended.** The naive ceiling's per-observation construction cost over a shared closure source grows 12× for a tenfold increase in N — the `Delegate.Combine` blowup. The fast path's grows 1.26×, which is linear within noise. That was the one thing the ceiling measurement declared mandatory, and it is the one thing that came out right.

| comparison construction, per observation | N=100 | N=1000 | growth |
|--- |---: |---: |---: |
| ceiling (subscribes per observation) | 284 ns | 3,440 ns | 12.1× |
| fast (shares one subscription) | 5,252 ns | 6,601 ns | 1.26× |

The ceiling is still ahead at a thousand, but it is on a curve and the fast path is on a line. Extrapolating, they cross not far beyond it.

**Propagation is a real if modest win.**

| propagation | graph | fast | ceiling |
|--- |---: |---: |---: |
| selector | 49.3 ns / 72 B | 39.2 ns / 72 B | 11.9 ns / 48 B |
| comparison | 62.7 ns / 94 B | 42.4 ns / 70 B | 12.3 ns / 47 B |

1.26× and 1.48× faster than the graph. But the ceiling is 4.16× and 5.10× faster, so the mechanism captures only **27% and 40%** of the available speed gap.

Allocation on the selector is unchanged from the graph at 72 bytes against the ceiling's 48. That is the boxing, and it is exactly the 24 bytes conceded when the fast path was made an `ObservableExpression`. The concession was made deliberately and it cost precisely what was predicted; what was *not* predicted is that the reuse would also cost most of the speed, since the notification still travels registry → scope → intrusive list → node → `Evaluation` setter → `FastEqualityComparer` → intrusive list → wrapper → scope flush → compare → raise, where the ceiling does handler → invoke → compare → raise.

## Where the construction cost is: unknown, and it will be measured rather than guessed

A prediction made before the run **failed**, which is worth recording because it was the thing I was most confident about. I expected compile-per-iteration to dominate, since each iteration disposes every observation, drops the delegate refcount to zero, and evicts. That would have shown as a per-observation cost roughly ten times higher at N=100 than at N=1000. The actual figures are 2,106 ns and 2,752 ns — the cost is *higher* at the larger size. Compilation is not the problem.

Two suspects remain, both structural-equality work performed per observation, and each observation has a freshly built parameter-replaced tree so nothing is cached across them:

- `DirectSubscriptionAnalyzer.Plan` dedupes with a `HashSet<Expression>` keyed by `ExpressionEqualityComparer`, which generates a diagram — an allocated `IReadOnlyList<object?>` — for every node it hashes.
- `CompiledLambdaObserved` looks the delegate up in a dictionary keyed by the same comparer, diagramming the entire lambda.

The second was chosen deliberately over a `ConditionalWeakTable` to avoid a recompile cliff. That reasoning still holds; the fix is to do both, reference-keyed first and structural only as a backstop, so the query layer's reused lambda object never pays for a diagram.

The first is harder, because the dedup exists to stop the plan claiming subscriptions the graph does not make. It may also now be less necessary than when it was added: the registry collapses duplicate subscriptions to one real `+=` regardless, so an undeduped plan costs extra attachments rather than extra subscriptions. That is a design question, not a tuning one.

**But which of the two dominates is not yet known, and this document is not going to guess.** The standing lesson holds — every prediction that multiplied a count by a measured unit cost has held, and every prediction that told a story about which code was hot has failed, including the one at the top of this section. `DirectSubscriptionAnalyzer.Plan` and `ExpressionEqualityComparer.GetHashCode` are both public, so both can be benchmarked directly and cheaply. That is the next measurement.

## The larger question this raises

If the diagramming turns out to dominate, the deeper fix is not to make the per-observation work faster but to stop doing it per observation. The plan for `person => person.Rank` is the same shape for every argument; only which constant the subscription resolves to differs. A plan computed once per lambda, with sources expressed as paths relative to the argument rather than as expression objects, would make per-observation work a couple of field reads and a dictionary insert — which is what the ceiling actually does, and why it costs 32 ns.

That is a redesign of the plan's representation, not a tuning pass, and it should not be started before the measurement says it is where the money is.

## Second cut: the dedup set removed

Re-run after replacing the planner's `ExpressionEqualityComparer`-keyed dedup with reference identity, the registry having made structural dedup redundant.

**The regression is gone.** Per-observation selector construction fell from 2,752 ns to 1,840 ns — a drop of 912 ns against the 1,097 ns the isolation benchmark attributed to the dedup set. That is arithmetic holding to within run-to-run noise, which is the kind of prediction that has held all along.

| construction, per observation | graph | fast (first cut) | fast (now) | ceiling |
|--- |---: |---: |---: |---: |
| selector, N=100 | 1,509 ns | 2,106 ns | **1,292 ns** | 20 ns |
| selector, N=1000 | 1,819 ns | 2,752 ns | **1,840 ns** | 30 ns |
| comparison, N=100 | 2,474 ns | 5,252 ns | **3,423 ns** | 284 ns |
| comparison, N=1000 | 3,480 ns | 6,601 ns | **3,169 ns** | — (quadratic) |

**But the honest word is parity, not victory.** At a thousand selector observations the two mechanisms are indistinguishable — 1,840 against 1,819, inside a 4.5% standard deviation. At a hundred the fast path is 14% ahead. Allocation is still worse than the graph's: 3,285 bytes against 2,323 on the selector.

Propagation is untouched by this change, as expected, and remains the clearer win: 1.29× on the selector and 1.59× on the comparison, capturing 29% and 46% of the ceiling's advantage.

## What the remaining gap is made of

Selector construction is 1,840 ns against a ceiling of 30. Two named components account for most of the difference, and neither is a mystery:

**The compiled-delegate cache still hashes structurally, ~707 ns per observation.** Measured directly. The fix is to try a reference-keyed weak table first and fall back to the structural dictionary only on a miss, so the query layer — which holds its predicate in a field and reuses the object — never pays. It was not done in this pass because a weak-table hit skips the reference count, and the release would then over-decrement. There is a clean design with a holder object carrying the count in both tables; it has a resurrect-after-eviction race that wants care rather than haste.

**Expression normalization, which the fast path should not need at all.** `ReplaceParameters` rebuilds the whole tree per observation, and the graph pays it too — which is precisely why the fast path cannot beat the graph by much while it also pays. The ceiling never normalizes anything: it holds a delegate and an argument. A plan computed once per lambda, with subscription sources expressed as paths relative to the argument rather than as expression objects, would let an observation skip normalization entirely. That is where the remaining order of magnitude is, and it is the redesign this document's first cut already gestured at.

The order is clear: the delegate cache is a bounded fix worth taking first, because it is measured, contained, and buys roughly 40% of what remains. The normalization redesign is larger and should follow it, with its own measurement.

## Status

The mechanism is correct — the test suite is green, including the propagation and subscription-agreement work — and it is now at parity on construction and 1.3 to 1.6 times faster on propagation. It is not yet worth the machinery it costs; two named, measured changes stand between here and that.
