# The subscription planned twice

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

The plan could name the same event of the same source more than once, and the observation attached every entry. Three ways in:

- **A member read in more than one place.** `subject.NameGets + subject.NameGets` is two member nodes over one parameter node, and each planned a property subscription for it. A standing test, `RepeatedMemberPlansTheSameSiteTwice`, named that as the expected behavior.
- **A branch reading what the test reads.** `(person.Rank > 0 ? person.Rank : person.Rank) > 0` planned three, one eager and one in each branch of the conditional, so the observation attached a second time whenever a branch was first evaluated.
- **An indexer over a constant or a parameter.** `AnalyzeConstant` and `AnalyzeParameter` plan a contents subscription for the target, and then `AnalyzeIndex` plans the same one again. Every such expression has been planning it twice since the indexer was admitted, and two standing tests were pinning it.

None of this could produce a wrong value: the fast path re-reads every input from live state on each evaluation. What each duplicate cost was an attachment object and a whole redundant evaluation on every notification from that object.

## The rule, and why it is agreement rather than approximation

**A subscription is discarded when another names the same event of the same source and belongs to a group attached no later and never released** — the same group, or a group which is an ancestor of its own. Sibling groups keep theirs, since neither is attached when the other is.

This is the graph's own grain. The observer caches a node by expression, so an expression named in several places is one node, attached once, at its earliest use — which the test `TheGraphAttachesASharedMemberAtItsEarliestUseWhicheverBranchNamesItFirst` pinned before any of this week's eligibility work. The discard makes the plan say what the graph does. And it is conservative in the direction that matters: it matches by source node *identity*, kind and property name, so two entries which differ in any of those are both kept.

The discard runs before empty groups are pruned, which is where the value is. Once a conditional's branch entries go, both its groups are empty, both are pruned, and the observation stops being a deferring one at all — no flags array, no group bitmask, no repeated evaluation on first reach.

## The readings

| arm | before | predicted | after |
|---|---:|---:|---:|
| `ConditionalDirect` | 1.28 MB / 464.8 μs | 1.14 | **1.14 MB / 360.7 μs** |
| `RankComparisonDirect` (control) | 1.14 MB / 353.0 μs | 1.14 | 1.14 MB / 352.5 μs |
| `IndexerReadDirect` (control) | 1.29 MB / 518.4 μs | 1.29 | 1.29 MB / 509.6 μs |
| `TwoObjectShortCircuitDirect` (control) | 1.31 MB / 469.4 μs | 1.31 | 1.31 MB / 474.0 μs |
| `TwoObjectConditionalDirect` (control) | 1.31 MB / 471.4 μs | 1.31 | 1.31 MB / 474.1 μs |
| `NotifyingChainDirect` (refused) | 3.71 MB | unchanged | 3.71 MB |
| every `Graph` arm | as measured | unchanged | byte-identical |

**Six predictions, six exact.** One arm was predicted to move and five to hold, and that is what happened.

## What they say

**`ConditionalDirect` and `RankComparisonDirect` are now the same arm in everything but the body of a delegate.** They read 1.14 MB against 1.14, Gen0 63.4766 against 63.4766, Gen1 46.3867 against 46.3867 — identical to every printed digit. That was the falsifiable form of the prediction: after the discard the two expressions differ only in a delegate which is compiled once per lambda and shared, so nothing per element should separate them, and nothing does.

**The account closes in both directions, within 4 bytes.** The 140 bytes per element this removed were named in advance as a `bool[]` at 32, the deferring subclass's two extra fields at 16, one attachment at 56, and the array `AttachDeferred` grows at 40 — 144. The same four terms had been used the other way to explain why the arm cost 1.28 rather than the floor. An account which predicts a figure and then predicts its removal is worth more than one which only fits.

**The time movement is real and the rest are not.** The graph arms are byte-identical and moved between −2.7% and +1.7%, so this pair's drift band is about 3%. `ConditionalDirect` fell 22.4%, far outside it, and landed within 2.3% of `RankComparisonDirect` — inside the band, so those two are now the same time as well as the same size. Every other direct arm moved inside the band.

## What this does not cover

**Two distinct source nodes which resolve to the same object are not deduplicated.** Two occurrences of a captured `other` in a lambda are two `MemberExpression` nodes, and matching them would need expression equality rather than identity. The graph uses equality, so that is where the remaining difference lives. It was left out deliberately: `ExpressionEqualityComparer`'s treatment of constants has not been checked, and two equal-looking closure reads over different display-class instances must not be merged. Settling that is a test, not an argument.

**And the fast path still keys a subscription by property name where the graph keys by event kind**, so an expression reading two *different* properties of one object still places two handlers on it where the graph places one. That is untouched here and is a different question.

## Tests

Four standing plan assertions counted the duplicates and were changed, all of them analyzer tests and all entailed by the change: `RepeatedMemberPlansTheSameSiteTwice` is now `RepeatedMemberPlansOneSite`, and the two indexer planning tests and the repeated-subexpression test drop from three subscriptions to two. Two new tests pin the rule in both directions — a member read by both the test and a branch is planned once and eagerly; a member read by two sibling branches is planned in each.

`DifferentialFuzz` is the judge that matters here, because the discard halves how often a repeated member is evaluated per change, and the number of notifications a consumer receives must not move with it. It passes.

## A mistake worth recording

The first run of this change failed one test with an `ArgumentOutOfRangeException` rather than an assertion. The cause was not the library: a scripted edit renumbered `plan.Subscriptions[2]` to `[1]` on the line it matched and left the two lines after it — which the anchor had not covered and which had never been read — still reading `[2]`. The match count was asserted; the surrounding method was not printed and read. That is the standing bulk-edit rule, and asserting the count is only half of it. The tell was in the failure itself: an out-of-range index rather than a failed assertion means the count assertion above it had already passed, which is to say the library was right.

## Left open

- Deduplicating by expression equality rather than node identity, which needs the constant question settled by a test first.
- The property-name keying difference described above.
- At most 15 bytes per element on the two arms which still carry a deferred operand and which the allocation model does not name.
