# Selecting and ranking

*2026-09-22 — `QuerySelectionBenchmarks`, a thousand people, construction arms building and disposing only what they name over a standing source and ordering; propagation arms toggling one person's rank there and back, two changes, each over a collection of its own*

## The question

GS5 asked for four things its formulas need: a maximum and minimum under a comparer of its choosing, the element with the extreme key, the element at a position computed from the count, and a live rank. The before-run priced what it does today for each; the after-run priced the operators that replace those shapes.

## The measurement

| arm | before | after, first run | after, caches made lazy |
|---|---:|---:|---:|
| floor: a change nobody observes | 17.01 ns / 96 B | 16.70 ns / 96 B | 17.05 ns / 96 B |
| maximum of names, default comparer — control | 321.75 μs / 887,312 B | 329.05 μs / 887,472 B | 322.75 μs / 887,344 B |
| first of a descending ordering by name — GS5's text maximum | 939.77 μs / 1,619,440 B | 956.47 μs / 1,619,888 B | 966.53 μs / 1,619,472 B |
| **maximum of names by an ordinal comparer** | — | 314.82 μs / 887,472 B | **314.67 μs / 887,344 B** |
| **element with the greatest name, ordinally** | — | 459.69 μs / 945,804 B | **448.63 μs / 945,676 B** |
| element at a fixed position of an ordering | 257.78 ns / 904 B | 255.34 ns / 904 B | 254.77 ns / 904 B |
| **element at a position computed from the count** | — | 202.69 ns / 896 B | **205.47 ns / 896 B** |
| count ahead of one element, constructed — ranking today | 485.69 μs / 931,025 B | 499.79 μs / 931,153 B | 488.67 μs / 931,028 B |
| **rank, constructed over a standing ordering** | — | 767.88 ns / 2,256 B | **748.47 ns / 2,256 B** |
| count ahead of one element, propagated | 191.04 ns / 336 B | 180.39 ns / 336 B | 185.90 ns / 336 B |
| ordering alone, propagated | — | 1,404.40 ns / 720 B | 1,490.49 ns / 720 B |
| **rank over that ordering, propagated** | — | 2,374.75 ns / 816 B | **2,392.70 ns / 816 B** |

The maximum by comparer is **3.07x faster and 1.83x lighter** than the ordering GS5 uses for it today, and within the time of the default comparer. The element with the greatest key costs 1.39x a plain maximum, the price of pairing each element with its key. A rank costs **653x less time and 413x less memory** to construct than counting the elements ahead of one.

## A rank is cheap to build and not cheap to keep

The rank's own share of a toggle is 2,392.70 less 1,490.49, **902 ns and 96 bytes for two key changes**, about 451 ns a change, against 186 ns for the whole toggle through a count. Every key change in the ordering re-reads every rank over it, because a key which comes to equal a neighbor's changes the tie count without moving anything, and the ordering cannot say which ranks a change reached. The 96 bytes are the two notifications each change raises, since both counts move. For a grid of N rows ranked over one ordering, a key change costs N such re-reads. That is the trade made for construction three orders of magnitude cheaper, and it is recorded rather than hidden.

## A regression the controls caught

The first after-run moved allocation on arms the new operators cannot reach: the count arm +128 bytes, the default maximum +160, the ordering by name +448. Every collection query had been given a cache dictionary and a lock for the element-at-by-count queries, and every ordering a cache dictionary and a lock for its ranks, all allocated whether or not anything used them. Both caches are now created on first use, under locks the queries already had.

The prediction that tested that account was that the count arm returns to exactly 931,025 bytes, the ordering to exactly 1,619,440, and the default maximum to within 32 bytes of 887,312. **The magnitude held and the exactness did not**: 931,028, 1,619,472 and 887,344. The maximum's 32 bytes were predicted: its cache is now keyed on direction and comparer, and each of its three entries is eight bytes wider. The ordering's 32 and the count's 3 remain. Each collection query still carries one reference field for the lazy cache and each ordering a second, which accounts for some of it in principle; how much, and whether the count's three bytes are that at all, is not settled by this run.

## Found while building it

- **`ObserveMax` and `ObserveMin` compared by magnitude.** The comparison query tested `Compare(...) == SoughtComparison`, so it replaced the value it held only when a comparison returned exactly 1 or -1. A comparer, or a type's own `CompareTo`, may return any positive or negative number, and over such a type the query reported whichever element it met first. It now reads the sign. `ComparerOverloads` pins it for a comparer and for a type whose comparison returns five times the difference.
- **A comparer consulting what the key selector did not read is never told.** The first form of the tie-breaking test used the element as the key and read its hours inside the comparer; the key never changed, so nothing re-read it, and the test failed. That is the contract rather than a defect, and the documentation of every comparer overload now states it.

## Predictions scored

- **Held.** The maximum by comparer within 10% of the default maximum: 2.5% faster, identical allocation.
- **Held.** The rank far below counting at construction: 653x.
- **Wrong in direction.** The element at a counted position within 10% of the fixed one: it is 19.3% faster.
- **Held in magnitude, missed in exactness.** The lazy caches returning the controls to their before figures, as above.
- **Not predicted, and now measured.** The rank's cost to keep current.
- **The floor held**, 96 bytes on all three runs.

## What has not been measured

A rank's propagation at row counts other than one rank over a thousand elements. Whether the element at a counted position is faster than the fixed one for a reason, or by the difference in how the two caches are keyed.
