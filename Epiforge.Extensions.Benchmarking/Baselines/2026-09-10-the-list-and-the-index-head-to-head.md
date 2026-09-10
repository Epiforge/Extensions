# The list and the index, head to head

*2026-09-10 — `AttachmentDispatchBenchmarks`, the attachment structure in isolation, 4 to 1,024 attachments over one or six names*

## Why this instrument exists

`2026-09-09-the-index-that-cost-more-than-it-saved.md` records an index justified by measuring only what it removed, built, wired into two query classes, measured, and reverted the same day. The lesson it names is the instrument that would have prevented it: **two candidate implementations measured head to head, in isolation, before either is wired in.**

A name index over a source's attachment list was about to be built on the same half-measurement. `2026-09-09-what-a-raise-costs-many-observations.md` priced the walk it removes at 3,500.8 μs; nothing had priced the lookup, the threshold test, or the maintenance on every attach and detach. This is that measurement. Neither candidate is in the library; both are structures carrying the same attachment objects and the same wanting test, with an attachment counting a notification rather than evaluating anything, so no observation, expression or propagation is in the figures.

## The measurement

Per raise, and the attach-and-detach of the whole set:

| attachments | names | unwanted raise, list → index | wanted raise, list → index | attach and detach | raises to repay |
|---:|---:|---|---|---:|---:|
| 4 | 1 | 2.04 → 1.86 ns (**1.1x**) | 2.48 → 4.84 ns (0.51x) | 2.94x | 260 |
| 4 | 6 | 2.17 → 1.88 ns (**1.2x**) | 2.50 → 3.41 ns (0.73x) | 5.52x | 357 |
| 16 | 1 | 10.26 → 1.88 ns (**5.5x**) | 8.61 → 13.07 ns (0.66x) | 2.67x | 20 |
| 16 | 6 | 10.22 → 1.89 ns (**5.4x**) | 9.93 → 4.27 ns (**2.33x**) | 3.44x | 30 |
| 64 | 1 | 67.81 → 1.88 ns (**36.1x**) | 61.75 → 65.54 ns (0.94x) | 2.28x | 8 |
| 64 | 6 | 67.77 → 1.87 ns (**36.2x**) | 47.89 → 9.32 ns (**5.14x**) | 2.81x | 11 |
| 256 | 1 | 255.86 → 1.86 ns (**137.3x**) | 210.26 → 216.07 ns (0.97x) | 2.30x | 8 |
| 256 | 6 | 256.57 → 1.87 ns (**137.5x**) | 218.28 → 34.22 ns (**6.38x**) | 2.54x | 10 |
| 1,024 | 1 | 1,101.89 → 1.88 ns (**585.8x**) | 964.08 → 909.17 ns (1.06x) | 2.19x | 7 |
| 1,024 | 6 | 1,122.71 → 1.89 ns (**595.5x**) | 971.11 → 154.31 ns (**6.29x**) | 2.36x | 8 |

## The index is O(1) on an unwanted raise and the list is linear, as predicted

**The indexed unwanted raise is 1.86 to 1.89 ns at every count and both spreads** — ten rows spanning a factor of 256 in attachment count, flat to three hundredths of a nanosecond. The list costs about **1.07 ns per attachment**, linear across the same range. The crossover is at roughly **two attachments**, which is below anything worth gating on; the index already wins at four.

## The maintenance is real and it is repaid in eight raises

The index loses the attach-and-detach arm at every count, by **2.19x to 2.81x above sixteen attachments** and more below it, and allocates a dictionary plus one bucket per distinct name — about 248 bytes for the first name and 69 for each after it, so 656 bytes at six names, constant in the attachment count and 0.9% of the set's allocation at 1,024.

Against that, one unwanted raise at 1,024 attachments saves 1,100 ns. **The whole set's extra attach cost is repaid by seven to eleven unwanted raises at sixty-four attachments and above**, by twenty to thirty at sixteen, and by two hundred and sixty at four. Any source that lives long enough to be raised at a few dozen times has paid for its index.

This is the figure the reverted grouping index never had, and it is the reason this one is not the same mistake. It is also the figure that retires the tripwire recorded before the run — *"if the attach penalty comes in above about 3x, the index is dead, because a grid attaches thirty thousand times."* The penalty does exceed 3x, at four and sixteen attachments with six names. But a grid's sources carry six attachments, which is below any threshold worth setting, so the index is never built there and the attach path never changes. **The tripwire was aimed at a cost the threshold already eliminates, and it was badly specified rather than met or missed.**

## The rows that changed the design

The single-name wanted raise is where the index does worst, and it does badly enough to matter: **0.66x at sixteen attachments — a third slower on the hot path of a plausible shape**, sixteen observations of one property of one object which raises only that property. A threshold on attachment count alone would impose that regression.

The cause is visible in the same rows. When every attachment wants the name, the bucket holds all of them and the lookup buys nothing; the index's only remaining advantage is that a bucket walk skips the per-attachment wanting test the list must perform, which is worth 0.13 ns each and does not cover the lookup until about a thousand attachments. At six names the bucket holds a sixth of them and the index wins from sixteen upward, converging on **6.29x — the name count**, which is what dividing the walk by the number of names predicts.

So the gate the data implies has two parts rather than one: **build the index when a source carries at least sixteen attachments *and* more than one distinct name.** That is free to evaluate — a source already counts its attachments, and one flag set when an attached name differs from the first suffices for the second test. Checked against every shape measured:

- a grid row, six attachments over six names: below the count, stays a list, nothing changes
- the fan-out watched shape, a thousand attachments over one name: one name, stays a list, and the 1.06x it forfeits is noise
- the regression band, sixteen to two hundred and fifty-six attachments over one name: excluded by the name test, which is the whole reason that test exists
- sixteen or more over several names: indexed, and better on both raise arms

## An ordering hazard the instrument exposed by not having it

The mimic gives every attachment exactly one bucket. The real `DirectSubscriptionAttachment` does not fit that: an `IndexerPropertyChanged` attachment wants both its property name and the conventional `Name[]` form, so it belongs to two buckets, and one `NextInBucket` pointer cannot express that. An attachment with a null property name wants only a report naming every property, so it belongs to none.

Putting those in a side list walked alongside the bucket solves membership and breaks order: today a raise visits matching attachments in list order, and an indexer attachment and an exact-name attachment can both want the same report. Whether anything depends on that order is not something to assert — so the order should be preserved rather than argued about. An ordinal assigned to each attachment under the registry's lock makes the bucket and the side list both ordered sublists of the main list, and a merge by ordinal reproduces today's sequence exactly, skipped entirely when the side list is empty, which is the common case.

The reads must also stay lock-free, as they are today. Bucket contents mutate under the registry's existing lock with the same volatile discipline the main list uses; adding a *new name* copies the dictionary and publishes it with a volatile write, which is cheap because the key count is small and changes rarely, and leaves in-flight readers walking a consistent snapshot.

## An observation not acted on

The list's search loop costs more per attachment than its notify loop — **1.076 ns against 0.941 at 1,024 attachments, 1.14x**, and the same ratio at 16, 64 and 256. Walking past an attachment that does not want the name is more expensive than notifying one that does, because the search loop evaluates a three-part condition per step where the notify loop evaluates one. That is a mild pessimization on the path an unwanted raise takes, and the index removes it at scale rather than fixing it. Whether restructuring the condition is worth anything on its own is unmeasured.

## Predictions scored

Five were recorded before the run. **Two landed, two were right in direction and wrong in magnitude, and one was wrong.**

- **Wrong.** *"`IndexedWantedRaise` at one name loses at every count, by a constant."* It loses at four, sixteen, sixty-four and two hundred and fifty-six, and **wins at 1,024** (1.06x). The gap is not constant either — 0.51x, 0.66x, 0.94x, 0.97x, 1.06x, improving monotonically. The error was treating the lookup as the index's only difference on that arm and missing that a bucket walk also skips the wanting test, which is a per-attachment saving that eventually overtakes a fixed cost. **This was labelled the falsifiable one, and it was falsified.**
- **Direction right, magnitude wrong.** The attach penalty was predicted at 1.5x to 2.5x and measured 2.19x to 5.52x. The band was set from the structure's extra work without accounting for the dictionary's own allocation at small counts, where it is not amortized over anything.
- **Direction right, band slightly off.** The unwanted-raise crossover was predicted between four and sixteen attachments and is at about two.
- **Exact.** The six-name wanted raise wins for large counts with its crossover between four and sixteen — it is between four and sixteen.
- **Exact.** Both raise arms allocate nothing on both candidates; only the attach arm allocates, and the indexed one allocates a dictionary plus one bucket per name.

The one that was wrong is the one that changed the design, and it changed it for the better: without the single-name rows coming in the way they did, the gate would have been a count alone and a sixteen-attachment single-name source would have been made a third slower.

## What has not been measured

Whether any real application has a source carrying sixteen or more attachments over more than one name, which after this gate is the only shape the index pays for — the question is out and unanswered. What the ordinal merge costs when the side list is not empty. The real implementation against the real registry, which carries three wanting kinds where the mimic carries one.
