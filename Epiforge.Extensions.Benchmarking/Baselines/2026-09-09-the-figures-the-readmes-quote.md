# The figures the READMEs quote

*2026-09-09 — a recovery document, not a run*

## Why this exists

A read of the READMEs against `Baselines\` found four quoted figures that could not be traced to a recorded table. One was stale and has been corrected in the README. One was a rounding. **The other two are correct and had no baseline behind them**, because each came from a run whose report was overwritten before it was folded into a document.

**Nothing here was measured today.** Both tables are transcribed from the project record kept alongside this repository, which recorded them when they were read. They are set down here so that a reader of the READMEs can find them, and so that the next run of either instrument has a before. **A figure recovered from a note is weaker evidence than a figure read off a report, and any of these that matters should be re-measured rather than re-quoted.**

## Ordering, after the two safe cuts

`OrderedScaleComparisonBenchmarks`, re-run after the hoisted comparables and the finger-seeded binary searches landed. Per key change above the unobserved floor:

| elements | this library | DynamicData | |
|---:|---:|---:|---|
| 1,000 | 1,259.5 ns / 292 B | 984.4 ns / 414 B | theirs, **1.28x** |
| 4,000 | 1,987.0 ns | 3,411.5 ns | ours, **1.72x** |
| 10,000 | 3,070.9 ns | 9,160.4 ns | ours, **2.98x** |

Refitted: ours **1,058 ns fixed + 0.2013 per element**, theirs **76 + 0.9084**, **crossover 1,389 elements**. Read arms at every size confirmed no deferral.

**The READMEs say "about 1,400" where the fit says 1,389.** That is a deliberate rounding, recorded here so nobody hunts for 1,400 in a table and concludes it is missing. The three ratios and the two propagation figures are quoted exactly.

This run supersedes the figures in `2026-09-08-where-the-ordered-loss-lives.md`, which predate the two cuts and are conservative in this library's favor by 58 to 93 ns.

## What a live grouped view holds

Four `--footprint` readings, taken while the grouped removal index was being weighed and never written up because that work was reverted. Bytes per element retained above what the caller holds anyway:

| | 250 | 1,000 | 4,000 | 10,000 |
|---|---:|---:|---:|---:|
| this library | 1,601.9 | 1,177.4 | 981.8 | **1,023.9** |
| DynamicData | 2,081.2 | 2,001.7 | 1,923.5 | **1,953.5** |

The READMEs quote the last column — 1,024 against 1,954 — beside the grouping crossover, which is the size at which that crossover matters.

**This closes the gap `2026-09-08-what-a-live-query-holds.md` left open.** That document lists grouped retention under "what has not been measured," which was true when it was written and stopped being true the next day.

**The curve is not monotonic** — 1,601.9 down to 981.8 and back up to 1,023.9 — and nothing has explained the rise at ten thousand. The filtered view measured earlier showed the same shape, 953.0 / 934.2 / 859.7 / 911.3, so it is a property of the instrument or of the allocator rather than of grouping. Not chased.

## The other two

**The indexer ratio in the root README was stale and has been corrected.** It read "between sixty-seven and seventy-nine times slower" at ten thousand elements, which is a pre-finger-search figure computed from the 28 August tables; `2026-08-28-indexed-access.md:7` states in its own words that it invalidates every indexer figure in that document. Recomputed from its tables: a projected query is **30.6x** an enumeration at ten thousand, a filtered one at one-in-2 is **47.4x** and at one-in-16 is **63.0x**, and a hundred elements is **15.6x**. Out of order it is about **200x**. The Expressions README already carried the corrected sentence; the root README did not, and now matches it.

**The at-scale sentence was half a sentence.** The performance box cited `2026-09-08-at-scale.md` for allocation being invariant to a hundred thousand elements and omitted the same document's timing half, where the per-change lead falls from 27.9x to 23.1x to 7.4x. Both READMEs now state both.

## What has not been measured

Whether either recovered table still holds. `OrderedScaleComparisonBenchmarks` has not run since; `--footprint` has not run since. **Re-measure before quoting either for anything that matters, and fold the report into a baseline the moment it is read** — which is the rule both of these were lost to.
