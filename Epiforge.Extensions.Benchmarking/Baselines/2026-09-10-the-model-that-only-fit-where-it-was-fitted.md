# The model that only fit where it was fitted

*2026-09-10 — `SourceCountBenchmarks` at three, six and twelve columns*

## What was predicted

Two instruments had each produced a per-raise figure for both mechanisms. From them a two-term model was fitted — a per-attachment cost for finding the attachments wanting a reported name, and a propagation of a fixed cost plus a marginal cost per node — and it reproduced all four measured six-column figures to three decimals. That was stated at the time to be worth nothing on its own, because the model had been fitted on those figures; three and twelve columns were named as the test.

It failed the test in every direction.

| | predicted | measured |
|---|---|---|
| unwanted-raise ratio, all widths | **1.87x, flat** | **1.622x, 1.824x, 2.215x — rising** |
| watched-raise ratio | 3.98x → 3.16x → 2.60x, falling | **2.433x, 3.124x, 2.754x — peaked** |
| construction ratio, all widths | 18.39x, flat | **16.19x, 17.79x, 7.04x** |
| fast path unwatched, absolute | 1.017 / 2.034 / 4.068 ms | 0.898 (−11.7%) / 2.010 (−1.2%) / 5.714 (+40.5%) |
| fast path watched, absolute | 1.601 / 2.618 / 4.652 ms | 1.757 (+9.7%) / 2.627 (+0.3%) / 6.259 (+34.5%) |

**Within 1.2% at the width it was fitted on, and out by a third to a half at twice that width.** That is the signature of a model with as many free parameters as it had data points: the residual at the fitted points was zero by construction and carried no information. Two instruments, two terms, an exact fit guaranteed before any arithmetic was done. Flagging the extrapolation as untested was correct and insufficient — the right response to a model that cannot fail to fit is not to label it, it is not to relay it.

## Why the per-attachment term is not a constant

The model's load-bearing assumption was that each mechanism charges a fixed amount per attachment on the source. Neither does:

| columns | fast path ns per attachment | graph ns per attachment | ratio |
|---:|---:|---:|---:|
| 3 | 2.995 | 4.857 | 1.62x |
| 6 | 3.350 | 6.109 | 1.82x |
| 12 | 4.761 | 10.545 | 2.22x |

Both rise, and **the graph's rises faster — 2.17x across the range against the fast path's 1.59x** — which is why the ratio climbs instead of holding. Nothing algorithmic changed; what changed is the working set. Twelve columns over a thousand sources is twelve thousand attachment objects and twelve thousand observations, and walking a linked list of twelve scattered objects has worse locality than walking three. The model had no term for memory, so it could not express the one effect that decided the answer.

The propagation fared no better. Its supposedly fixed cost measures 28.161 ns at three columns and 45.421 at six, and the fast path's delegate invocation *falls* across the range — 8.582, 6.172, 5.451 ns — where the model held it constant.

## The direction of the error is the one piece of good news

Every miss runs the fast path's way. **The unwanted-raise advantage grows with row width rather than holding**, from 1.62x at three columns to 2.22x at twelve, because the graph's locality degrades faster. A wider grid is better for the fast path than a narrow one, not neutral as claimed and not worse.

And the result the widths were added to test survives intact: **the fast path wins all nine comparisons at all three widths.** It allocates nothing on every raise arm at every width. The graph allocates 2,400,000 B on every watched arm at every width — identical across widths, because one column is raised and therefore one node pair propagates however many columns the row has, which is the one prediction in this family that held for the reason given.

## An instrument defect, introduced with the widths

The construction ratio collapsing to 7.04x at twelve columns is not a property of the library. Per observation, the fast path's construction **allocation falls** across the widths — 618.7, 589.3, 574.7 bytes — while its **time rises tenfold**: 163.0, 180.7, 893.8 ns. Gen2 collections go from none at six columns to 109.375 at twelve. Linear allocation and exploding time with promotion appearing is garbage collection, not work.

The cause is this instrument. Its setup retains two sets of observations for the raise arms — two thousand sources' worth, `2 × 1,000 × Columns`, which is twenty-four thousand live observations at twelve columns — and the construct arms then allocate twelve thousand more on top of that live set. At three and six columns it inflates them; at twelve it pushes them into Gen2 and the arm stops measuring construction at all.

So **no construction figure from this instrument is clean, including the 1.117 ms already recorded and quoted to a third party.** The three- and six-column numbers are inflated by an amount nobody has measured and the twelve-column number is a garbage-collection measurement wearing a construction label. The fix is to split the construct arms into a class of their own, so they run against their own live set and nothing else.

## The six-column controls moved, and the cause is named

| arm | recorded | now | |
|---|---:|---:|---:|
| construct, fast path | 1,117.0 μs | 1,084.0 | −3.0% |
| construct, graph | 20,540.0 μs | 19,281.9 | −6.1% |
| watched raise, fast path | 2,618.0 μs | 2,627.1 | +0.3% |
| watched raise, graph | 8,281.0 μs | 8,207.4 | −0.9% |
| unwatched raise, fast path | 2,034.0 μs | 2,009.9 | −1.2% |
| unwatched raise, graph | 3,812.0 μs | 3,665.3 | −3.8% |

The claim recorded when the widths were added was that the six-column set would *reproduce* the earlier figures, because the first six expressions were kept in their original order. That is not what happened, and the reason is the same change: `BenchmarkSlice` went from seven backing fields to thirteen, so every slice is forty-eight bytes larger and every arm's locality changed with it. **Keeping the expressions identical does not hold the subject identical.** The raise arms came back within 3.8% and the construct arms within 6.1%, which is reproduction in the loose sense and not the sense that was claimed.

## What has to be retracted

The width-independence of the 1.87x was relayed to the Tachyon working group as a strengthened claim, with the note that it was theirs rather than ours. It is wrong. The ratio is not independent of width; it improves with width. The retraction is owed promptly and without softening, because it was handed over as publishable.

## What has not been measured

Construction at any width, cleanly — every figure on record is contaminated by the retained live set. Whether the per-attachment rise is locality or something else, which a width sweep holding total object count constant would separate. Any width above twelve or below three.

## The after — the defect was a third of it

The construct arms now run in `SourceWidthConstructionBenchmarks`, which retains no observations. Same day, same machine.

| width | fast path ns/obs, contaminated → clean | graph ns/obs | time ratio | allocation ratio |
|---:|---|---:|---:|---:|
| 3 | 163.0 → **156.5** (−4.0%) | 2,850.1 | **18.21x** | 3.70x |
| 6 | 180.7 → **172.5** (−4.5%) | 2,829.4 | **16.40x** | 3.79x |
| 12 | 893.8 → **591.4** (−33.8%) | 5,399.5 | 9.13x | 3.81x |

The prediction recorded above was an either/or: the twelve-column figure either falls into the 163–181 band, in which case the resident set was the cause, or it stays near 893.8, in which case the cliff is intrinsic and the defect claim should be withdrawn. **Neither happened.** It fell by a third and stopped at 591.4, with Gen2 collections rising from 109.375 to 164.063. So the resident set accounted for about a third of the inflation and two thirds of it is intrinsic to constructing and disposing twelve thousand coexisting observations in one operation. Posing it as an either/or was the error — two causes were available and the question was their proportion, not which one.

What that leaves is a clean reading at three and six columns and none at twelve. The graph's per-observation construction is flat at about 2,840 ns across the two clean widths while the fast path's is 156.5 and 172.5, which is **16.40x at six columns and 18.21x at three**. The 18.39x withdrawn above came from the contaminated six-column pair; **the honest figure for a six-column grid is 16.4x**, and it was withdrawn correctly and restored lower.

**The allocation ratio is the robust one: 3.70x, 3.79x, 3.81x — stable across every width including the one where timing is meaningless**, because allocation is counted rather than timed and the collector cannot distort it.

## The raise arms moved in a way worth recording

Re-running them after the construct arms left the class was meant to be a formality. Two widths were:

| width | watched ratio | unwatched ratio |
|---:|---:|---:|
| 3 | 2.453 (was 2.433) | 1.646 (was 1.622) |
| 6 | 3.038 (was 3.124) | 1.843 (was 1.824) |
| 12 | **2.161 (was 2.754)** | 2.263 (was 2.215) |

**The rising unwanted-raise ratio replicated** — 1.646, 1.843, 2.263 against 1.622, 1.824, 2.215, every width within 2.2%. That is the claim the retraction to the working group rests on and it now has two runs behind it. The watched ratio is still non-monotone, peaking at six.

But the twelve-column watched arm moved 25.5% on the fast path while the graph's moved 1.5%, against deviations of 1.1% and 0.8%. The only change was that the class stopped allocating two thousand slices it never used. **Removing two thousand unused objects from a setup moved a measured arm by twenty-five percent**, which is larger than several differences elsewhere in this file that have been attributed to mechanism. Absolute figures at twelve columns are not stable enough to quote, and the stable quantities in this family are the allocation ratios and the three- and six-column timings.
