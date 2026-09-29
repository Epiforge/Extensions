# The array of one

*2026-09-29 — an ordering by one key keeps that key in the element's entry rather than in an array of one*

## How this was measured

On the change, the footprint report was taken at 13:40 and `OrderedComparisonBenchmarks` ran with 7.0.1 as a second job. The befores are the footprint report of 10:59 and the `OrderedComparison` arms of `BenchmarkRun-20260929-124821`, both on the code committed just before this change. Tests on the change: Expressions 1,049 of 1,049, Collections 651 of 651, Comparisons 48 of 48, Components 194 of 194, on each of five frameworks.

## What changed

An ordering's entry for an element kept its keys in an array, one slot per key, so an ordering by one key paid for an array of one: a 24 B header beside the 24 B slot. The entry now holds its first key itself, and only an ordering by more than one key makes entries of a derived type carrying an array of the keys after the first. The comparer and the ordering reach a key by its index through the entry, and the ordering takes the number of keys from its selectors rather than from the entry, since an ordering given no key selector has no key at all.

An entry of an ordering by one key is 24 B larger and its array is gone, 48 B, so 24 B would be saved if the entry kept a reference to an array which might be null; keeping that reference only on the derived type saves its 8 B as well. An entry of an ordering by several keys is the same size as before.

## Predicted and measured

| a sorted view, per element | before | predicted | measured | ObservableComputations |
|---|---:|---:|---:|---:|
| retained, a thousand elements | 936.8 B | 904.8 B | 904.8 B | **897.6 B** |
| retained, four thousand | 874.2 B | 842.2 B | **842.2 B** | 893.7 B |
| retained, ten thousand | 925.7 B | 893.7 B | **893.7 B** | 902.7 B |
| building and disposing a view of a thousand | 1,030.54 KB | 999.4 KB | 999.41 KB | 1,093.63 KB |

Every prediction held, from the sandbox's own footprint of the same code, where allocation fell by exactly 32,000 B at a thousand elements and 320,000 B at ten thousand. Five build-and-drop cycles leave 160 B at four thousand and ten thousand and 256 B at 250 and a thousand, where the 10:59 report read 160 B at every size. The sandbox read 256 B at 250, a thousand and four thousand on the committed code before this change as well as after it, so the 96 B is not this change's. It is the ordering's static cache of stable key selectors, which shares structurally equal selectors among orderings and was restored this morning after the overnight run found building from a fresh selector ten times dearer without it: with the cache bypassed in the sandbox, the same rows read 160 B. The 10:59 report was taken before the cache returned. It holds one entry for each distinct stable selector for the life of the process, as 7.0.1's did, and nothing for each view built. The 250-element row read 972.7 B against 949.6 B before; it is the report's first sorted row and moved run to run in both the sandbox and earlier reports, so it is not quoted.

**A sorted view here now holds less than ObservableComputations' at four thousand and ten thousand elements**, and 7.2 B more at a thousand.

| `OrderedComparison`, a thousand elements | 7.0.1 | now | before this change |
|---|---:|---:|---:|
| an element moving in a sorted view | 1,291.2 ns / 292 B | 333.2 ns / 87 B, 0.26x | 336.5 ns / 87 B |
| building and disposing | 700.7 μs / 1,761.00 KB | 346.2 μs / 999.41 KB, 0.49x / 0.57x | 346.5 μs / 1,030.66 KB |

A key change neither allocates nor takes measurably differently.

## The readmes and the release note

- Both readmes' row for building a sorted view is from this run, all four libraries, and their row for what a live sorted view holds reads 905 B. The paragraph on what a view holds says ObservableComputations' sorted views hold the least at a thousand elements and this library's at four thousand and ten thousand, and memory is no longer given as a reason to choose ObservableComputations for sorting.
- The release note's ordering line carries this run's building figures, 0.49x the time and 0.57x the allocation, and its line on what each element holds reads 365 B less in a sorted view than 7.0.1, naming the key held in the entry.
