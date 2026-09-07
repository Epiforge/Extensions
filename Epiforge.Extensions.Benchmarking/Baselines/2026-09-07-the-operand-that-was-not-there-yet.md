# The operand that was not there yet

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was refused

A short-circuiting operator whose deferred operand reached a source of its own, and a conditional expression whatever its branches reached. Both were refused for the same stated reason: the graph does not attach the nodes of a deferred operand until an evaluation reaches it, and the fast path decided its whole subscription plan once, when the observation was constructed. Attaching eagerly would have made the two mechanisms disagree about what is subscribed.

## What the graph actually does, measured before anything was written

Nine tests were written and run against the unchanged library, because the eligibility argument had to rest on the graph's behavior rather than on a reading of it. All nine passed. Three of their results shaped the design, and two of those were not in any note:

- **The latch lives in `Evaluate`, not in construction.** `ObservableBinaryExpression.OnInitialization` builds the right operand's entire node tree at construction with `deferEvaluation: true`; what is deferred is the *attachment*, because `ObservableMemberExpression` subscribes to its target's value inside `Evaluate`. The graph pays for the nodes of an untaken branch either way.
- **`ObservableConstantExpression.OnInitialization` attaches unconditionally, ignoring `deferEvaluation`.** So the contents subscription of a constant, or of the argument, is eager even where only an untaken branch reads it, while the contents subscription of a closure field is deferred. `TheGraphAttachesToAConstantsContentsEvenWhereOnlyADeferredBranchReadsIt` and `TheGraphAttachesToAClosureFieldsContentsOnlyWhenTheBranchReadingItIsTaken` pin the two against each other.
- **A member read inside an untaken branch still attaches eagerly if anything outside that branch also reads it**, because the observer caches nodes by expression and any eager parent reads `Evaluation`, which un-defers. `TheGraphAttachesASharedMemberAtItsEarliestUseWhicheverBranchNamesItFirst` pins it.

The third is the one that changed the design. A subscription's attach time is the *earliest* use of the node planning it, not the first branch the analyzer happens to walk into, so a subscription belongs to the nearest operand enclosing every use of that node — the nearest common ancestor of the groups the node is reached in — rather than to the operand it was planned in.

## What changed

Each deferred operand — the right operand of `&&`, `||` and `??`, and each branch of `?:` — becomes a group. `FixedSubexpressionRewriter` wraps the group's root in a block which first stores `true` into a `bool[]` passed alongside `values` and then yields the operand's value, so reaching the operand is recorded by the operand's own evaluation, on its own path, with nothing at all on the eager path and no call out of the delegate. The observation reads those flags after `evaluate` has returned, attaches the groups newly reached, and evaluates again, so the result it keeps was read with the subscription already in place — the order `ObservableMemberExpression.Evaluate` itself uses. A group whose subscriptions all resolve to an enclosing group is empty and is pruned, so an expression whose branches reach nothing new compiles exactly as it did.

`DeferringDirectObservableExpression` is a separate class holding the group bitmask, so the loop and the bitmask exist only where they are used. An expression with no deferred operand was intended to pay nothing; it pays 8 bytes, which is measured below.

## The readings

Before is the 7 September run recorded in `2026-09-07-the-scope-in-the-wrong-place.md`, which is the last column of its table. A `Direct` arm equal to its `Graph` twin is the instrument saying the shape was refused.

| arm | before | after |
|---|---:|---:|
| `ConditionalDirect` | 5.21 MB | **1.42 MB / 482.1 μs** |
| `ConditionalGraph` | 5.21 MB | 5.21 MB / 6,163.8 μs |
| `TwoObjectShortCircuitDirect` | 4.11 MB | **1.44 MB / 498.6 μs** |
| `TwoObjectShortCircuitGraph` | 4.11 MB | 4.11 MB / 4,162.1 μs |
| `TwoObjectConditionalDirect` | new arm | **1.45 MB / 478.2 μs** |
| `TwoObjectConditionalGraph` | new arm | 5.19 MB / 6,719.2 μs |
| `NotifyingChainDirect` | 3.71 MB | 3.71 MB / 3,723.4 μs |
| `NotifyingChainGraph` | 3.71 MB | 3.71 MB / 3,695.8 μs |
| `RankComparisonDirect` (control) | 1.18 MB / 356.0 μs | 1.19 MB / 355.8 μs |
| `RankComparisonGraph` (control) | 2.78 MB / 2,476.6 μs | 2.78 MB / 2,462.1 μs |
| `IndexerReadDirect` (control) | 1.35 MB | 1.35 MB / 517.6 μs |
| `IndexerReadGraph` (control) | 4.79 MB / 4,861.6 μs | 4.79 MB / 4,867.4 μs |

Every graph arm is byte-identical to its before. The three times on record moved by 0.6%, 0.1% and 0.06%, so this pair of runs is tight enough that the time movements below are readable.

## What they say

**A conditional expression over one object went from 5.21 MB to 1.42, and from its graph twin's 6,163.8 μs to 482.1 — 3.7x the memory and 12.8x the time.** The before time is not on record, but the shape was refused, so the arm was running the graph; its twin in this same run is the fair comparison.

**A short-circuiting operator reaching a second object went from 4.11 MB to 1.44, and from 4,162.1 μs to 498.6 — 2.9x and 8.3x.**

**A conditional reaching a second object, measured for the first time, costs 1.45 MB against the graph's 5.19, and 478.2 μs against 6,719.2 — 3.6x and 14.1x.** This is the arm which actually exercises late attachment on a conditional: 999 of its thousand elements take the branch reaching `other` and attach there after construction.

**Nothing else moved.** `NotifyingChain` is still refused and its two arms are still identical to the byte, which is the instrument confirming that the property chain through a notifying intermediate was not touched.

**A deferred group costs about 130 nanoseconds per observation.** The three admitted arms land between 478.2 and 498.6 μs against `RankComparisonDirect`'s 355.8, which is the latch, the attachment and the one repeated evaluation the group's first reach requires.

## The prediction, which was wrong in both directions

Predicted 1.33, 1.30 and 1.35 MB; measured 1.42, 1.44 and 1.45. The direction held and the magnitude did not, by 90 to 150 bytes per element.

**And the claim that a group-free expression would pay nothing was wrong by 8 bytes.** `RankComparisonDirect` went 1.18 to 1.19 MB. The `bool[]` is held in a field on the shared generic class rather than on the deferring subclass, so every direct observation carries one extra reference: 8 bytes, 0.008 MB per thousand. `IndexerReadDirect` did not move, which is what an 8-byte cost looks like at a resolution of 0.01 MB — one arm crosses the rounding boundary and the other does not. The field is removable: the base class can pass an empty array to the delegate instead of holding one, since `[]` is `Array.Empty<bool>()` and allocates nothing. It was removed in the pass recorded below, along with the two allocations the decomposition named, and the figures in this table are that pass's before.

## The account does not close, by about 140 bytes per element

`TwoObjectShortCircuitDirect` exceeds `RankComparisonDirect` by 0.25 MB, which is 250 bytes per element. Named: a `bool[1]` at 32, the deferring subclass's bitmask at 8, one attachment to `other` at 56, and a one-entry `values` array at 32 — 128. The contents subscription planned for the closure field resolves to `None`, `other` being no kind of collection, and costs nothing.

**The leading candidate for the remaining 122 is `AttachDeferred` itself**, which builds a `List<DirectSubscriptionAttachment>`, lets it allocate a backing array, and then allocates a fresh `attachments` array to copy into — 32 plus 56 plus 40 by the unit costs on record, which is 128 and closes the gap almost exactly. That is a decomposition from reading, which this project holds fit to rank candidates and unfit to price them, so it is a hypothesis and not a measurement. Counting the group's attaching sites before allocating anything, and growing `attachments` once, would test it in one pass.

## Then the three allocations the decomposition named

Three removals in one pass, with the arms only partly separating them: the flags field and the scratch array move every direct observation, the list moves only an observation with a deferred operand. What distinguishes the first two is that the scratch saving scales with the number of sites the analyzer named while the field is flat, so the two controls predict different movements rather than one number repeated.

- The `bool[]` field moved off the shared generic class onto the deferring subclass, and the base hands the delegate one static empty array.
- `Attach` allocated an array sized to every site, filled the prefix which resolved to something, and then copied to a right-sized one. It now counts what will attach in a pass which resolves and type-tests and allocates nothing, then allocates once at that size.
- `AttachDeferred` built a `List<DirectSubscriptionAttachment>` and let it allocate a backing array before copying into the grown `attachments`. The attachments now go straight into the grown array, and the compare-and-swap retry merges rather than re-attaching.

| arm | before | predicted | after |
|---|---:|---:|---:|
| `RankComparisonDirect` | 1.19 MB | 1.14 | **1.14 MB / 353.0 μs** |
| `IndexerReadDirect` | 1.35 MB | 1.29 | **1.29 MB / 518.4 μs** |
| `ConditionalDirect` | 1.42 MB | 1.27 | **1.28 MB / 464.8 μs** |
| `TwoObjectShortCircuitDirect` | 1.44 MB | 1.30 | **1.31 MB / 469.4 μs** |
| `TwoObjectConditionalDirect` | 1.45 MB | 1.30 | **1.31 MB / 471.4 μs** |
| `NotifyingChainDirect` | 3.71 MB | unchanged | 3.71 MB |
| every `Graph` arm | as measured | unchanged | byte-identical |

**Two predictions landed exactly and three landed one printing step high.** The controls carry only the flat field and the site-scaled scratch array; they were predicted at 1.14 and 1.29 and read 1.14 and 1.29. That is a count of objects at unit costs already on record, which is the form of prediction this project has found reliable, and it is the form which failed in the pass before this one — where the same method was applied to terms nobody had priced. The three arms with a deferred operand were predicted 1.27, 1.30 and 1.30 and read 1.28, 1.31 and 1.31, so the model is short by at most 15 bytes per element there and possibly by nothing at all, the true figures lying anywhere inside the rounding step. Not chased.

**A count read out of the analyzer was wrong again, and was caught before the run rather than after.** The short-circuit arm's second object is a static field of the benchmark class and not a closure field, which makes that expression three sites and not four; noticing it moved that prediction from 1.29 to 1.30, which is the figure that then landed within a step. The standing rule is that a count read this way is the least reliable term in any prediction resting on it, and it is worth re-reading rather than trusting.

**The fast path now has a floor of 1.14 MB per thousand elements**, against 1.18 where this day's work began, and an indexer read costs 1.29 against 1.35. Neither of those is the deferred-operand feature; both are the scratch array, which every direct observation had been paying for since the fast path was written.

**Times decide nothing in this pair of runs.** The graph arms are byte-identical and their means moved between −4.5% and +5.2%, so the drift band here is about 5%, against 0.6% in the previous pair. Every direct arm moved inside that band. Read the controls in the run you have; there is no drift figure that carries between pairs.

**The excursion recorded in the previous pass settled itself.** `TwoObjectConditionalGraph` reported a standard deviation of 4.5% of its mean and now reports 2.3%, on byte-identical allocation. It was the instrument, and waiting for a run that was going to happen anyway cost nothing.

## Also recorded

**The fast path keys a subscription by source, event *and* property name; the graph keys by source and event.** So an expression reading two properties of one object places two handlers on it through the fast path and one through the graph. This is not new — it has been true of every admitted `&&` over two properties since 6 September — but nothing had stated it, because `SubscriptionAgreement` compares distinct entries and hides it. It cannot produce a wrong value, since the fast path re-reads every input from live state on each evaluation, which is why it never had the intermediate-value defect the graph had; it costs an attachment and a whole redundant evaluation per notification of that object. `ConditionalDirect`'s expression reads one member three times through three distinct nodes and so attaches three times where the graph attaches once, which is part of why that arm overshot its prediction.

## What guards it

- `DeferredAttachment` — nine tests pinning the graph's attachment timing, nine running both mechanisms over identical fixtures and comparing what each has attached before and after the operand is reached, and two on the plan. Each of the nine cross-mechanism tests asserts the fast observer's expression cache is empty first, so the comparison fails rather than passing vacuously if the analyzer ever declines the shape again.
- `DifferentialFuzz` needed no change and became a far stronger judge for free: it already generates `AndAlso`, `Condition` and `Coalesce` over four distinct objects, and every one of those shapes was refused, so the fast world fell back to the graph and those programs were comparing the graph against itself. How large that share was has never been measured.
- `SubscriptionAgreement`, unchanged in what it proves.

## Tests which had to change, and why

- `AndAlsoReachingASecondObjectIsIneligible`, `CoalesceReachingASecondObjectIsIneligible`, `OrElseReachingASecondObjectIsIneligible` and `ConditionalOfMembersIsIneligible` asserted the refusal this change removes. They now assert eligibility.
- `IneligibleExpressionStillBuildsTheGraph` asserted that `s.Rank > 0 ? s.Rank : s.Score` falls back to the graph, and had chosen a conditional precisely because conditionals were refused. Its proposition is worth keeping, so it now uses `s.Next!.Rank`, a property read through an intermediate which can notify — a refusal with no plan behind it to remove.
- `TheGraphDoesNotSubscribeToAnUntakenBranchUntilItIsTaken` constructed its observer with direct subscription left on, so it had only ever measured the graph because the shape was refused. It now pins `UseDirectSubscription = false`, as its two siblings in that file already did, which restores the proposition its name states. Changing its expected count instead would have turned a graph test into a fast-path test without saying so.

## Left open

- **Deduplicating plan entries by resolved source, event and property name, keeping the shallowest group.** The graph caches one node per equal expression and attaches once where the fast path attaches once per node, so `ConditionalDirect`'s expression, which reads one member through three distinct nodes, attaches three times where the graph attaches once. This is now the largest difference between the mechanisms on this path, and every byte of it is waste: an attachment object, and a whole redundant evaluation on every notification from that object. The befores are the figures in the second table above.
- At most 15 bytes per element on the three arms with a deferred operand which the model does not name, and which may be nothing at all at this resolution. Worth naming only if a later run at a smaller element count puts the units in kilobytes.
- The `ShortCircuit` arms of `EligibilityMatrixBenchmarks` and `RefusalCauseBenchmarks` have moved, their deferred operand's subscription now being attached late rather than eagerly. Their recorded figures remain a valid before for whenever re-measuring them is decided on its own merits.
