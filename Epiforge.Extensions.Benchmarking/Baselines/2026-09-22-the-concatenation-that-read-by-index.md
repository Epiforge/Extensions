# The concatenation that read by index

*2026-09-22 — `ConcatIndexerPenaltyBenchmarks`, two thousand elements concatenated from two sources of a thousand, the consumer constructed and disposed over a standing concatenation*

## The question

The comparison, element and filter queries, and the list operations on every collection query, choose between reading their source by index and enumerating it by the penalty the source reports. A concatenation, a prepending and an appending reported none, whatever their sources charged, so a maximum over a concatenation of two projections read it element by element through the concatenation's indexer and paid the projections' indexed cost on every read. GS5 builds exactly this shape. The request was to make the flag honest; the question here was what it was costing.

## The measurement

| arm | before | after | allocated before | allocated after |
|---|---:|---:|---:|---:|
| maximum over a concatenation of two lists | 26.428 μs | **3.810 μs** | 1.16 KB | 1.29 KB |
| maximum over a concatenation of two projections | 45.971 μs | **3.740 μs** | 1.16 KB | 1.34 KB |
| maximum over one projection of 1,000 — control | 1.452 μs | 1.495 μs | 1.44 KB | 1.44 KB |
| filter over a concatenation of two filters | 2,335.441 μs | 2,229.700 μs | 1,603.16 KB | 1,603.34 KB |
| filter over one filter of 1,000 — control | 300.321 μs | 314.339 μs | 796.85 KB | 796.85 KB |

The maximum over two projections is **12.29x faster**; over two lists, **6.94x**. Both now allocate 131 to 184 bytes more, which is the enumerators. The controls held: allocation identical, time within 3.0% and 4.7%, under the 9% that decides anything.

## The concatenation's own indexer was the expensive part

The before-run said something the request had not. A thousand elements enumerated through a projection cost 1.45 μs; two thousand read by index through a concatenation of two plain lists — sources with no penalty at all — cost 26.4 μs, about 13 ns a read. The concatenation's indexer takes a lock on every read to learn where its first source ends. So passing the sources' flags through, which is what was asked for and what a slice does, would have left the lists arm untouched at 26.4 μs.

The concatenation now reports a penalty unconditionally, and the lists arm became the head-to-head: the rule set before the run was to revert to passing the sources' flags through unless it fell by more than 9%. **It fell to 3.810 μs, 6.94x.** Enumerating a concatenation beats indexing it whatever its sources are. A prepending and an appending take no lock, so they pass their source's flag through, as a slice does.

## The filter over a concatenation did not move

2,335 μs before, 2,230 after, a 4.5% change that decides nothing, and allocation within 0.2 KB. The filter builds by reading its source the way the flag says, and the flag changed, so the reading was never what that arm spends its time on. Two thousand elements should cost about twice the single-filter control's 314 μs; they cost 2,230, reach Gen2 where the control does not, and allocate exactly twice the control. Linear allocation with time out of proportion and promotion appearing is the collector's signature rather than the code's: the class holds two filters and two projections of a thousand people each for the life of the run. **Not chased.** It is a finding about this instrument's resident population, not about concatenation.

## A shipped defect this found

The consumers test written alongside this change, `ConcatConsumers`, failed on its first assertion of `IndexOf` for all three shapes: expected 3 or 5, observed -1. `ObservableCollectionQuery.IndexOf` read:

```
if (!HasIndexerPenalty)
    for (int i = 0, ii = Count; i < ii; ++i)
        if (EqualityComparer<TElement>.Default.Equals(this[i], item))
            return i;
else
```

The `else` binds to the inner `if`, not the outer one. So on every query which reports a penalty — a filter, a projection, a flattening, both synchronization-context queries, and now a concatenation — the loop never ran and `IndexOf` returned -1 for everything. On every other query it returned the right answer by accident, running a full enumeration from the else at the first element that did not match, and for an element that was not there it ran that enumeration once per element. Nothing in the suite called `IndexOf` on a query before this. `Contains` beside it has braces and was right. A scan of all seventy files under `Observable\Query` for a loop whose body is an unbraced `if` sitting under an `if` with an `else` found this one site and no other.

## Predictions scored

- **Held.** The maximum over two projections falls to between 3 and 6 μs: 3.740. Allocation rises by 100 to 200 bytes: +184.
- **Held.** The maximum over two lists falls: 6.94x, which settled the decision rule.
- **Held.** Controls do not move: allocation identical, time inside 9%.
- **Held.** The filter over a concatenation's allocation stays within 20 KB: +0.18 KB. No prediction was made on its time, and it did not move.
- **Wrong, in the test rather than the benchmark.** Every test was predicted to pass. Three `ConcatConsumers` rows failed, on a defect in the library which shipped in 5.0.0; when it arrived is a question for `git log`, not for this document.

## What has not been measured

What `IndexOf` costs on a penalized query now that it works. Whether the element and single queries over a concatenation gain as the maximum did; they take the enumerator by the same flag.
