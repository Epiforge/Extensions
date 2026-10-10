# Shifting a range once

*2026-10-10 — `RangeCollectionSpanBenchmarks` and `OrderingEmptyingBenchmarks`, each against Expressions 7.1.2 and the Collections 4.3.2 it was released with*

## The question

An ordering whose results emptied announced a reset, and so did one an element entered while empty. Grindstone's view alerts ignore resets, so an element arriving into an empty ordered view raised no alert. The ordering now announces an addition and a removal instead, and removing everything became `RemoveRange(0, count)` on its results.

That call removed one element at a time from the front of the backing list, shifting everything behind it each time. So did `InsertRange`, `ReplaceRange`, a `MoveRange` of more than one element and each run `RemoveAll` takes out, in an `ObservableRangeCollection<T>` announcing once per operation. An operation on n elements near the start of a collection of length m cost n shifts of up to m elements. Each now copies the range out once and shifts the rest once.

The question is what that is worth, and what announcing the elements an ordering loses costs against announcing a reset.

## Range operations on the front half

| arm | elements | 7.1.2 | current | | 7.1.2 | current |
|---|---:|---:|---:|---:|---:|---:|
| insert half again at the start | 1,000 | 30.03 μs | 2.39 μs | 12.6x | 12.14 KB | 9.91 KB |
| move the front half to the end | 1,000 | 32.43 μs | 1.37 μs | 23.6x | 4.27 KB | 2.05 KB |
| remove all of the front half by predicate | 1,000 | 33.44 μs | 22.57 μs | 1.48x | 2.11 KB | 2.11 KB |
| remove the front half | 1,000 | 21.16 μs | 1.19 μs | 17.8x | 2.05 KB | 2.05 KB |
| replace the front half with a quarter | 1,000 | 36.30 μs | 5.56 μs | 6.52x | 9.38 KB | 8.2 KB |
| insert half again at the start | 16,000 | 6,057.5 μs | **12.5 μs** | **486x** | 189.42 KB | 156.4 KB |
| move the front half to the end | 16,000 | 74,752.8 μs | **11.1 μs** | **6,721x** | 64.37 KB | 31.34 KB |
| remove all of the front half by predicate | 16,000 | 74,782.6 μs | **38.1 μs** | **1,962x** | 31.41 KB | 31.41 KB |
| remove the front half | 16,000 | 74,983.1 μs | **9.45 μs** | **7,936x** | 31.34 KB | 31.34 KB |
| replace the front half with a quarter | 16,000 | 75,603.8 μs | **29.8 μs** | **2,537x** | 142.02 KB | 125.39 KB |

**No arm allocates more than it did.** A move allocates half, since it copies into an array of exactly the range rather than a list grown to hold it.

Each iteration fills a fresh collection, so each figure is one operation timed alone. The current arms at 1,000 elements are around a microsecond, where the timer's resolution shows in the medians (0.9, 1.0, 1.6 μs); read those ratios as order of magnitude. Removing by predicate at 1,000 is mostly the thousand calls to the predicate, which nothing here changes.

## An ordering going from full to empty and from empty to full

| arm | elements | 7.1.2 | current | | 7.1.2 | current |
|---|---:|---:|---:|---:|---:|---:|
| empty a full ordering | 1,000 | 107.66 μs | 71.76 μs | 1.50x | 12.02 KB | 16.02 KB |
| fill an empty ordering | 1,000 | 777.61 μs | 734.15 μs | 1.06x | 739.53 KB | 730.65 KB |
| empty a full ordering | 16,000 | 99,737.2 μs | **967.5 μs** | **103x** | 187.8 KB | 250.4 KB |
| fill an empty ordering | 16,000 | 9,697.2 μs | 9,778.1 μs | 0.99x | 11,127.77 KB | 10,992.98 KB |

Emptying removes every element from the source in one range, so the released job also pays the released source's removal, which is most of its 99.7 ms. **Emptying now allocates a third more**, 4 bytes an element at both sizes: the array of removed elements the ordering announces where it announced a reset. Filling costs what it did. Its StdDev at 16,000 is 65% of the mean in the released job and 57% in the current one, with medians of 5,186 and 5,278 μs, so the means carry collections the iterations happen to land on.

## Predictions scored

- **Tests held:** 0 failures, Collections 659 on every framework. The before had the 5 predicted, all `InvalidRangesChangeNothing`.
- **Range operations at 16,000, predicted at least 1,000x:** four of five held. Insertion missed at 486x, because the released insertion took 6.06 ms where the released removals took 75 ms for comparable shifting; why insertion was 12x cheaper is not established.
- **Range operations at 1,000, predicted at least 30x:** all five missed, at 1.48x to 23.6x. The prediction counted shifts and ignored the fixed cost of an operation, which is most of a microsecond-scale arm.
- **Emptying at 16,000, predicted at least 1,000x:** missed at 103x. The current arm's 967 μs is not the removal. What it is has not been measured; releasing 16,000 key observations is the likeliest candidate and is not established.
- **Filling, predicted within 1.2x in time and allocation:** held, the current arm taking 0.94x and 1.01x the released time and 0.99x its allocation.

## What has not been measured

What the 967 μs of emptying a 16,000-element ordering is spent on. Range operations in the mode announcing each element, which still shift once an element because each announcement has to describe the collection as it stands.
