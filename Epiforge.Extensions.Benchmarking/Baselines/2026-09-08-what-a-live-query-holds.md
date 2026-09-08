# What a live query holds

*2026-09-08 — `QueryFootprintReport`, run as `--footprint`, DynamicData 9.4.33, 64-bit, server GC off*

## The question

Everything measured on 8 September was allocation. Allocation is what a query costs to build and to feed; it is not what a query weighs while it sits there. A caller holding a live view over a collection for the lifetime of a window is paying the second, not the first, and the two can point in different directions.

## The instrument was wrong first, and the way it was wrong is the lesson

`QueryFootprintReport` already measured retention and had a column named `After release` which its own prose called the control, saying it should be near zero. **It had never been near zero, and no session had ever looked at it.** For `ThreeNodes` at 10,000 elements it read 8,472,144 B against 9,112,448 B retained: disposing the query and dropping the observer appeared to release 7%.

Three rounds of instrumentation chased that figure as a property of this library — first as a subscription outliving disposal, then as a bounded retention of the most recently built graph. **Both were wrong, and the control which killed them took six lines and involves no library at all:**

| control | Allocated | Retained | After dispose |
|---|---|---|---|
| `The collection alone, then released` at 10,000 | 1,064,152 B | 960,120 B | **960,120 B** |
| `The collection alone, five cycles` at 10,000 | 5,320,760 B | 0 B | **0 B** |

A bare `ObservableRangeCollection<BenchmarkPerson>`, created and dropped with nothing else in the picture, appears to retain **96.0 B per element forever** — unless the creating and dropping happens in a method which has *returned* before the measurement, in which case it retains **exactly zero**.

**A method which builds an object graph, drops it, and then measures from that same frame reads its own dead locals as live.** Setting the source-level variable to null does not clear the copy the JIT spilled elsewhere in the frame, and that copy is in the GC's root table until the frame pops. Every `After dispose` and `After release` figure this report has ever produced from an inline arm was measuring the measuring method.

**The control that settles a question about a library should not contain the library.** It was written fourth.

## What release actually costs

With each cycle body moved behind a method boundary, and both libraries given the same treatment:

| arm, at 10,000 elements | allocated over five cycles | left afterwards |
|---|---|---|
| `Expressions, five build-and-drop cycles` | 59,837,712 B | **160 B** |
| `Expressions, five cycles then one over ten elements` | 59,875,784 B | 192 B |
| `DynamicData list, five build-and-drop cycles` | 201,166,624 B | 245,064 B |
| `The collection alone, five cycles` | 5,320,760 B | 0 B |

**This library releases everything.** Five complete build-and-drop cycles over five collections of ten thousand elements allocate 59.8 MB and leave 160 bytes — not 160 per cycle, 160 in total, and identically 160 at 250, 1,000 and 4,000 elements as well. There is no leak of any size, and nothing here bears on the release.

**DynamicData's figure is 100–300 KB and does not scale with element count** — 681.4 B/element at 250 falls to 24.5 B/element at 10,000, which is a flat absolute cost wearing a per-element disguise. Whether that is a bounded warm-up of its own caches or a small fixed cost per query was **not determined**, because it would take another arm and it does not bear on any advice given to a reader. It is not a leak in the sense that matters and should not be reported as one.

## What a live view weighs

`Retained` is measured while the objects are alive, so the frame artifact never touched it. Each figure is above whatever the caller holds anyway: the collection for this library and for DynamicData's list, the collection *and* the cache for DynamicData's cache, with the cache's own weight on its own row.

| | 250 | 1,000 | 4,000 | 10,000 |
|---|---|---|---|---|
| the collection alone | 96.5 B | 96.1 B | 96.0 B | 96.0 B |
| **this library's view** | **953.0 B** | **934.2 B** | **859.7 B** | **911.3 B** |
| DynamicData's list view | 1,890.0 B | 1,865.0 B | 1,858.8 B | 1,864.0 B |
| DynamicData's cache view | 1,775.9 B | 1,974.8 B | 1,889.8 B | 1,942.6 B |
| DynamicData's cache itself | 52.2 B | 54.8 B | 28.5 B | 49.1 B |

**A live filtered view costs this library about 911 B per element and DynamicData about 1,864 B, so this library holds roughly 0.49x** what the alternative does — 0.46x against the cache path once the cache's own weight is added. Both are flat across a fortyfold range of sizes, so this is a property of the designs.

**Neither figure is small.** A view over ten thousand elements weighs 9 MB here and 19 MB there, against 960 KB for the collection alone: **a live query weighs 9.5x the data it observes in this library and 19x in DynamicData.** That is the sentence a reader deciding between them deserves, and it matters more than the ratio between the two.

## The prediction, wrong in the direction to distrust

Recorded before the run: *we retain more per element than DynamicData's view, because we hold one observation object per element; somewhere near 300–600 B/element. If our figure comes back under 200 B/element, suspect the instrument.*

**We retain less, not more — 0.49x rather than the 1.5x or so implied.** The guard was set on the wrong side: it watched for a figure too flattering to be true and the error was in the other direction entirely. A prediction which only guards one tail is half a prediction.

## What has not been measured

Retention of ordered, grouped and lookup queries, all of which hold more per element than a filter does. Retention under the graph rather than the fast path. Whether DynamicData's five-cycle residue is warm-up or per-query. Retention at a hundred thousand elements, where `ScaleComparisonBenchmarks` found this library's per-change *time* degrading 8x and where a 9 MB-per-ten-thousand view would be 90 MB.
