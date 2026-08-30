# Direct subscription: the execution path, measured in six cuts

> **Read the sixth cut for the current state.** The earlier sections record a regression, its diagnosis, and three corrections, kept because the reasoning that found the costs is worth more than the numbers it corrected. Every figure before the fifth cut comes from a single launch and should be read as provisional.

`DirectSubscriptionCeilingBenchmarks`, .NET 10.0.11, Intel Core Ultra 9 275HX. Cuts one through four at one launch, the fifth at three. Three arms: **graph** (`UseDirectSubscription = false`), **fast** (the mechanism as built), **ceiling** (the hand-rolled stand-in from `2026-08-30-direct-subscription-ceiling.md`).

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

## Fifth cut: three launches, and where the variance actually lives

No code change. `[SimpleJob(launchCount: 3)]`, and `FastSelectorObserveRebuildingTheLambda` removed — at roughly 20 ms per operation it dominated the run and its cost is already priced above as a one-time measurement.

### The figures of record

Per observation, N = 1000.

| | graph | fast | ceiling | fast vs graph |
|--- |---: |---: |---: |---: |
| selector construction | 1,922 ns / 1,784 B | **912 ns / 1,733 B** | 30 ns / 184 B | **2.11× faster, 0.97× memory** |
| comparison construction | 3,372 ns / 3,607 B | **1,224 ns / 2,258 B** | 3,141 ns / 16,684 B | **2.75× faster, 0.63× memory** |
| selector propagation | 46.8 ns / 72 B | **36.0 ns / 72 B** | 10.8 ns / 48 B | **1.30× faster** |
| comparison propagation | 59.0 ns / 96 B | **36.5 ns / 72 B** | 11.2 ns / 48 B | **1.62× faster, 0.75× memory** |

At N=100 the same ratios are 2.06×, 2.89×, 1.41× and 1.63×. So the defensible claims are **2.1× construction on the selector, 1.3 to 1.6× propagation**, with the comparison-construction figure carrying a caveat given below.

### What the third launch found

**The variance is not spread across the benchmark. It is concentrated in one arm.** `GraphComparisonObserve` has a standard deviation of 20% of its mean at N=100 and 11% at N=1000. Every other arm in the table is at or under 2.1%.

The shape is launch-level bimodality, not sampling noise. At N=100 the mean sits 14% *above* the median; at N=1000 it sits 5% *below* it. Individual launches are landing in different places, and which place dominates changes with the parameter.

**The single launch had landed on the fast mode.** It reported `GraphComparisonObserve` at 252,861 ns for N=100. The three-launch median is 252,984 ns — the same number. Had I quoted a comparison-construction ratio from that run I would have said 2.53×, stated it as a measurement, and been wrong by 14% in the direction that flattered the change. That is precisely the failure mode `launchCount: 1` cannot detect, and it is the reason this re-run happened.

The honest comparison-construction claim is therefore a range: **2.5× to 2.9×**, resting on a graph arm whose distribution is wide. The selector rows, at under 2.1% deviation everywhere, are the ones to quote.

### Three launches are still not enough

`FastSelectorObserve` at N=1000 moved from 749,492 ns in the fourth cut to 912,332 ns here — up 21.7% — with identical code and identical allocation (1,694 KB then, 1,693 KB now). Its within-run deviation is 1.9%. So the run-to-run gap is eleven times the variance the run itself reports.

Its Gen2 collections fell from 3.9063 to 0.9766 per thousand operations across the same two runs. Allocation did not change; when the collector chose to run did.

I do not have an explanation I can defend. The obvious suspect — that removing a 6.9 MB-per-operation arm changed the GC environment — I believe is wrong, because BenchmarkDotNet runs each case in its own process, so the removed arm never shared a heap with this one. **Marking this as unexplained rather than inventing a mechanism.** What it establishes is a floor on honesty: construction figures carry run-to-run uncertainty of roughly ±20% that neither one launch nor three fully captures, and they should be quoted as ranges indefinitely, not just until the next re-run.

Propagation remains stable to within 2% across all five cuts, for the reason given in the fourth cut: it measures a tight loop, not an allocation path racing the collector.

### A prediction that came true sideways

The first cut, watching the naive ceiling go quadratic on a shared closure source, said of the ceiling and the fast path: *"they cross not far beyond it."* They have crossed. At N=1000 the fast path constructs a comparison observation in 1,224 ns against the hand-written ceiling's 3,141 — **2.6× faster than the thing built to represent the best case**, on a sixth of the memory.

But the crossing did not arrive the predicted way. It was expected to come from growing N until the ceiling's `Delegate.Combine` curve overtook a stationary fast path. Instead N stayed put and the fast path fell from 6,601 ns to 1,224. The direction was right and the mechanism was wrong, which puts it in the same category as every other prediction here that told a story about which code was hot.

### Where the remaining room is

| selector, N=1000 | fast | ceiling | fast is | share of the graph→ceiling gap closed |
|--- |---: |---: |---: |---: |
| construction | 912 ns | 30 ns | 30.7× the ceiling | **53%** |
| propagation | 36.0 ns | 10.8 ns | 3.3× the ceiling | **30%** |

Both readings are true and they point opposite ways. Against the graph, construction is now the better result — more than half the available gap closed. Against the ceiling it is far the worse, because 53% of a 64× gap still leaves thirty-fold. Construction is where the remaining order of magnitude is, and the route to it is unchanged: analyze and plan from the lambda once, resolve sources relative to the argument, and skip `ReplaceParameters` and `Plan` per observation entirely.

## Sixth cut: analysis, planning and normalization all leave the per-observation path

Two changes, benched together. Eligibility and the subscription plan are computed once per lambda and cached beside the compiled delegate, with sources compiled into sites that resolve against the argument and the frozen-values array. And with eligibility known from the lambda alone, `ReplaceParameters` became unnecessary on the fast path — the normalized tree is now built lazily, only if something asks to print or log it.

Three launches. Per observation, N = 1000.

| | graph | fast | ceiling | fast vs graph |
|--- |---: |---: |---: |---: |
| selector construction | 1,797 ns / 1,784 B | **263 ns / 912 B** | 27 ns / 184 B | **6.8× faster, 0.51× memory** |
| comparison construction | 2,918 ns / 3,608 B | **338 ns / 1,000 B** | 2,982 ns / 16,684 B | **8.6× faster, 0.28× memory** |
| selector propagation | 45.5 ns / 72 B | **35.0 ns / 72 B** | 10.6 ns / 48 B | **1.30× faster** |
| comparison propagation | 57.4 ns / 96 B | **35.7 ns / 72 B** | 11.2 ns / 48 B | **1.61× faster, 0.75× memory** |

At N=100 construction is 6.4× and 8.6–10.1×; propagation 1.38× and 1.63×.

Construction fell from the fifth cut's 912 ns to 263 ns — a saving of **649 ns per observation** — and allocation nearly halved. Propagation is unchanged to within 3%, which is what a change that touches only construction should do, and is the control that says the two figures are not moving together for some shared reason.

### The ceiling has been passed on comparison construction

At N=1000 the fast path constructs a comparison observation in 338 ns against the hand-rolled ceiling's 2,982 — **8.8× faster than the thing built to represent the best case**, on a sixteenth of the memory. At N=100 they are level: 273 ns against 270.

That is the whole argument for the registry, drawn as a curve. The ceiling subscribes per observation to a shared closure source and pays `Delegate.Combine`'s quadratic; the fast path shares one subscription and pays a dictionary insert. The crossover the first cut predicted "not far beyond" N=1000 now sits at **N=100**, and past it the gap only widens.

### What remains, and it is no longer construction

| selector, N=1000 | fast | ceiling | fast is | share of the graph→ceiling gap closed |
|--- |---: |---: |---: |---: |
| construction | 263 ns | 27 ns | 9.6× the ceiling | **87%** |
| propagation | 35.0 ns | 10.6 ns | 3.3× the ceiling | **30%** |

The two readings have swapped places since the fifth cut, where construction was 30.7× the ceiling and the larger prize. Propagation is now the worse of the two against the ceiling and the place the remaining work is. Its 3.3× is the node-and-wrapper path — registry, scope, two intrusive lists, the `Evaluation` setter, `FastEqualityComparer`, the wrapper's compare — and closing it means not reusing the wrapper, which remains a deliberate trade.

### A prediction I made unscoreable, which is a method error worth recording

Before the run I predicted the *first* of the two changes would be worth ~185 ns of the 912, by subtracting the dedup set's measured 912 ns saving from `Plan`'s isolated 1,097 ns. I then benched both changes together, which makes the prediction unscoreable from this table.

I considered a run between the two and decided against it to save fifteen minutes. That was wrong, and specifically wrong against this document's own standing lesson: predictions about *which code is hot* have failed every time here, and the one occasion built to test one, I skipped. The saving of 649 ns is real and measured; its split between the two changes is not recoverable from these instruments.

It is partly recoverable cheaply. `DirectSubscriptionOverheadBenchmarks` measures `BuildThenPlanSelector` and `BuildSelectorOnly`; their difference is `Plan`'s cost on a normalized selector, which is exactly what the first change stopped paying per observation. That benchmark was raised to three launches and re-run.

### The attribution, recovered — and the prediction scored wrong

| isolated, three launches | mean | allocated |
|--- |---: |---: |
| `BuildSelectorOnly` | 18.1 ns | 56 B |
| `BuildThenAnalyzeSelector` | 32.8 ns | 56 B |
| `BuildThenPlanSelector` | 87.9 ns | 464 B |

By subtraction, `Analyze` costs **14.7 ns** and `Plan` costs **69.7 ns / 408 B**. The analyzer figure replicates the 12.6 ns measured two cuts ago, which is the check that the instrument is measuring what it did before.

**Predicted 185 ns for the first change. Actual 70 ns. Wrong by 2.6×, overestimating.**

The prediction's error is more instructive than its size. I justified my confidence by filing it under "count multiplied by a measured unit cost" — the class that has held every time in this document. It was not that. It was the *difference of two figures taken from two different instruments*: `Plan`'s 1,097 ns from the isolation benchmark minus the 912 ns drop measured in the observation benchmark. Those two were never a clean minuend and subtrahend, and the arithmetic silently assumed the entire observation-benchmark drop was the dedup set inside `Plan`. The lesson is not "predict less" but **the class label was doing the work of justifying the confidence, and I applied the label wrongly**. A prediction's pedigree has to be checked as carefully as its arithmetic.

So the split of the 649 ns:

| | time | allocation |
|--- |---: |---: |
| planning, moved to per-lambda | ~70 ns | ~408 B |
| normalization, made lazy | ~579 ns | ~464 B |

**Normalization was 89% of the time saved and roughly half the memory.** The fourth cut's prose said "that is where the remaining order of magnitude is," and it was right — but that too was a story about which code was hot, and it is recorded as having been lucky rather than sound, on the same evidence that just convicted mine.

### An unrelated number this run turned up, worth chasing

`BuildThenHashSelectorLambda` — one structural hash of a two-node lambda through `ExpressionEqualityComparer` — costs **681 ns and 1,441 B**, with a 12.7% standard deviation. That is the diagram-per-lookup cost recorded as parked long ago, and it is nearly three times what a whole fast-path observation now costs.

The graph performs several such lookups per observation against its node caches, and `GraphSelectorObserve` is 1,797 ns. **The hypothesis is that structural hashing dominates the graph's construction cost.** That is precisely the sort of claim this document has been wrong about repeatedly, so it is written as a hypothesis with a measurement attached: count the comparer's invocations for one graph observation, multiply by 681 ns, and see whether the product resembles 1,797. If it does, the parked item is the largest remaining win in the library and it benefits the graph, the queries, and every consumer — not just the fast path.

### The fifth cut's variance findings replicated

`GraphComparisonObserve` is again the only wide arm — 14.5% standard deviation at N=1000, 18.2% at N=100, against 2.4% or less everywhere else. Two independent runs now agree that the variance is a property of that one arm rather than of the benchmark.

The arms that did not change moved 3–8% run to run: `GraphSelectorObserve` −6.5%, `CeilingSelectorObserve` −8.0%, `GraphSelectorChange` −2.9%. That sits inside the ±20% band the fifth cut claimed and did not contradict it. The 6.8× construction ratio is large enough that this drift cannot account for it.

## Status

The mechanism is correct, has a differential fuzzer behind it, and at three launches is **6.8× faster to construct on the selector and 8.6× on the comparison, and 1.3 to 1.6× faster to propagate**, on roughly half the construction memory and equal or less propagation memory. Construction figures carry roughly ±20% run-to-run uncertainty; the ratios here are far larger than that band.

The performance work this document has pointed at since its first cut — lifting analysis, planning and normalization out of the per-observation path — is **done**. Construction now closes 87% of the graph-to-ceiling gap and beats the hand-written ceiling outright wherever a subscription source is shared.

What is left is propagation, at 3.3× the ceiling, whose cost is the reused wrapper. That trade should stay until something forces it.
