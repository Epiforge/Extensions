# Direct subscription: the execution path, measured in three cuts

> **Read the third cut for the current state.** The first two sections record a regression and its diagnosis, kept because the reasoning that found the cost is worth more than the numbers it corrected.

`DirectSubscriptionCeilingBenchmarks`, .NET 10.0.11, Intel Core Ultra 9 275HX, one launch. Three arms: **graph** (`UseDirectSubscription = false`), **fast** (the mechanism as built), **ceiling** (the hand-rolled stand-in from `2026-08-30-direct-subscription-ceiling.md`).

Per-observation figures throughout, N = 1000 unless noted.

## First cut

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

## Third cut: the weak table, and a benchmark of my own making

Two changes. The compiled-delegate cache became a `ConditionalWeakTable` keyed by the lambda instance, deleting the reference counting, eviction and locking that existed only to serve repeatedly rebuilt lambdas. And the benchmark's lambdas were hoisted to fields, so it measures what `ObservableCollectionWhereQuery` actually does — hold a predicate and reuse the object — rather than rebuilding a tree per element.

The first attempt at the second change hoisted the lambdas in the `Observe` methods and forgot `Setup`, which still built one per standing observation. With reference-keyed caching that gave each of a thousand observations its own compiled delegate. Propagation appeared to regress from 36.5 ns to 53.4 ns while nothing in the propagation path had changed, and the regression was ten times larger at N=1000 than at N=100 — the signature of a call site going megamorphic, not of an edit. Sharing one delegate restored it to 38.0 ns, which confirms the diagnosis.

### Where it lands

| per observation, N=1000 | graph | fast | ceiling | fast vs graph |
|--- |---: |---: |---: |---: |
| selector construction | 1,919 ns / 1,785 B | **908 ns / 1,702 B** | 30 ns / 180 B | **2.11× faster, 0.95× memory** |
| comparison construction | 2,820 ns / 3,610 B | **1,217 ns / 2,231 B** | — | **2.32× faster, 0.62× memory** |
| selector propagation | 47.2 ns / 72 B | **38.0 ns / 72 B** | 11.0 ns / 48 B | **1.24× faster** |
| comparison propagation | 60.5 ns / 94 B | **37.5 ns / 70 B** | 11.3 ns / 47 B | **1.61× faster, 0.75× memory** |

The selector rows are the cleanest comparison. The comparison-construction figure for the graph moved 17% between runs, which is not noise: hoisting the lambda in `Setup` means the standing observations now share subexpressions with the benchmark's, so the observer's node cache is warm for `closure.threshold.Rank` where it previously was not. That is the more realistic arrangement, but it makes cross-run comparison of that one row unsafe.

Propagation allocation on the selector is still 72 bytes against the ceiling's 48. That remains the boxing, and it remains the price of reusing the wrapper.

### The cliff, priced

`FastSelectorObserveRebuildingTheLambda` costs **22,582 ns per observation** — 11.8× the graph. That is a compile every time, and it is what a caller pays for building the same lambda fresh in a loop instead of holding it. The pattern was judged abnormal enough not to carry machinery for; this is the number that judgement is standing on, recorded so nobody has to re-derive it. `UseDirectSubscription` is the escape if anyone ever lands on it.

### What remains

Construction is 908 ns against a ceiling of 30. The largest identified component is expression normalization: `ReplaceParameters` rebuilds the whole tree per observation, and `Plan` then walks that tree — both per observation, both on work whose *shape* is identical for every argument. A fast path that determined eligibility and planned from the **lambda**, cached once, and resolved subscriptions relative to the argument would skip both. The graph cannot do this; its node caches are keyed by the normalized tree, so it must build one every time. That is the one place the fast path is structurally advantaged rather than merely leaner, and it is where the remaining order of magnitude lives.

Propagation is 38.0 ns against a ceiling of 11.0. That gap is the node-and-wrapper path — registry, scope, two intrusive lists, the `Evaluation` setter, `FastEqualityComparer`, the wrapper's compare — and closing it means not reusing the wrapper, which was a deliberate trade and should stay one until something forces it.

## Fourth cut: freezing the closure values

The rewrite that lifts closure field chains into a per-observation values array. Two predictions were stated before the run.

**Propagation: predicted a small rise on the comparison arm, unchanged on the selector. Both held, and the rise was below noise.**

| propagation, per observation, N=1000 | before | after |
|--- |---: |---: |
| selector (no closure field) | 38.0 ns | 37.2 ns |
| comparison (one closure field) | 37.5 ns | 37.3 ns |

An unbox and a bounds check are free against the surrounding forty nanoseconds. The array read costs nothing measurable.

**Allocation: predicted one array per observation. Held, to the byte.** Comparison construction rose 28 B per observation, against 32 B for a one-element `object[]`. Selector rose 33 B for an empty one. This is the kind of prediction that has held all day — a count multiplied by a known unit cost.

**Construction timing moved in ways this change does not explain, and that is the actual finding.**

Selector construction went from 908 ns to 749 ns per observation at N=1000 — 17% *faster* — while going 2% slower at N=100. Adding an empty array allocation cannot make anything 17% faster. Within-run standard deviation is under 2% in both, so the gap is not sampling noise inside a launch; it is variance *between* launches, which `launchCount: 1` does not measure at all.

That reframes several earlier figures. `GraphComparisonObserve` moved 17% between the second and third cuts and was attributed to node-cache warming from a benchmark change; it may simply have been the same between-run variance. **Construction numbers in this document should be read as ranges, not points.** Across the last three runs the fast selector spans 749–908 ns and the graph 1,849–1,919, so the honest statement is *roughly twofold*, not 2.11×.

Propagation is far more stable — the change arms sit within 2% across every run — because they measure a tight loop rather than an allocation-heavy construction path competing with the garbage collector.

**Before quoting a construction ratio for the record, re-run with `launchCount: 3`.** The single launch was chosen deliberately when the question was "fat or thin," and it answered that. It cannot answer "how much."

## Status

The mechanism is correct, has a differential fuzzer behind it, and is **roughly twice as fast to construct and 1.2 to 1.6 times faster to propagate**, with equal or less allocation. The remaining performance work — lifting analysis and normalization out of the per-observation path — is now purely a performance change, since its correctness half has landed.
