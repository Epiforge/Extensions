# What the queries did not own

*2026-09-28 — `QueryDisposalUnderChange`, `FlatteningUnderChange`, `ConcatenationUnderChange` and four benchmark classes, before and after 7.0.1 closed what `2026-09-28-the-copy-the-observer-keeps.md` left open*

## What was wrong

- **A query's last release could race a change on another thread.** Disposing of a filtered or projected view of a sequence or a dictionary, or of a collection built from a dictionary, stopped listening to its elements without taking its lock. A change on another thread could modify what the disposal was walking, which threw `InvalidOperationException` out of `Dispose`, or add an element the disposal had already passed, which left the disposed view listening to it.
- **A flattening read the sequences its selector returned as they stood.** A subscriber reading it inside one change to such a sequence saw a later change the flattening had not yet announced, and a filter built over it while one of them grew took the new element twice.
- **A concatenation read a second operand implemented outside this library as it stood**, because it had no lock of that operand to take. A subscriber reading the concatenation inside one change to that operand read 3 at the index where it had just announced 4.

## The change

- The five disposals tear down under the query's lock and mark the query released, and its change handlers return once it is.
- A flattening keeps a copy of each sequence its selector returns which announces its changes, changed only as those announcements arrive, and reads, weighs and indexes from the copies. It makes the dictionary holding them only when the first such sequence appears, and inserts an element added alone directly rather than enumerating the announcement, as the observation's copy does.
- A concatenation whose second operand is not a query of this library observes that operand through its `CollectionObserver`, so the operand is kept as a copy like any observed sequence. The concatenation holds the observation's lock while it is built and disposes of the observation when it is released.

## Tests

| run | Expressions, each of five frameworks | failing |
|---|---:|---|
| disposal befores | 943 of 948 | the five rows of `QueryDisposalUnderChange` |
| flattening befores | 948 of 950 | both rows of `FlatteningUnderChange` |
| concatenation befores | 950 of 951 | the row of `ConcatenationUnderChange` |
| after | **951 of 951** | |

Collections held at 647 of 647 and Components at 194 of 194 throughout. Each befores run tested the code as the round before it left it, with that round's rows added, and every new row failed on every framework. A disposal row failed either with `Collection was modified` out of `Dispose` or with the added member still watched, depending on which thread reached the list first; both are the defect. The second row of `FlatteningUnderChange` first held the build at the first enumeration of an inner list, which a fixed flattening never makes, so the fix failed it for never reaching the gate. It was revised to hold the build at whichever comes first, that enumeration or a member's score, and the revision is what passes here.

## What it costs

The after is `BenchmarkRun-20260928-131153`. The before for `OperatorConstructionBenchmarks` is `BenchmarkRun-20260928-104049`, which ran 7.0.1 before the disposal change. No run of the other three classes exists since the overnight 6.1.0 run, `BenchmarkRun-20260927-031028`, so their comparisons span 6.1.1, 7.0.0 and all of 7.0.1, not this change alone.

### Building

| arm | before | after | |
|---|---:|---:|---:|
| `Concat`, zero elements | 55.57 μs / 132 KB | 55.98 μs / **134 KB** | 1.01x |
| `Concat`, one element | 55.57 μs / 132 KB | 56.54 μs / **134 KB** | 1.02x |
| `SelectMany`, zero elements | 121.54 μs / 344 KB | 125.73 μs / **346 KB** | 1.03x |
| `SelectMany`, one element | 178.09 μs / 610 KB | 177.84 μs / **612 KB** | 1.00x |
| `Slice`, zero elements | 739.18 μs / 1,916 KB | 753.15 μs / **1,924 KB** | 1.02x |
| `Slice`, one element | 1,539.89 μs / 3,144 KB | 1,597.06 μs / **3,152 KB** | 1.04x |
| `Where`, one element | 126.18 μs / 436 KB | 125.22 μs / 436 KB | 0.99x |
| `Select`, zero elements | 54.90 μs / 170 KB | 58.53 μs / 170 KB | 1.07x |
| `Cast`, zero elements | 55.45 μs / 170 KB | 57.66 μs / 170 KB | 1.04x |

**A flattening and a concatenation each hold one reference more, and nothing else is allocated.** Each arm builds 256 of them, 2 KB. `Slice` builds two of each, so a slice allocates **7,696 B where it allocated 7,664**, and 7,200 in 6.1.0. `Where` over one element includes the locked disposal and allocates what it did.

**This run shows no time cost of its own.** The arms this change touched moved 0% to 4%, and `Select` and `Cast` over nothing, which it did not touch, moved 4% to 7% between the two runs.

### Flattening

`ObserveSelectManyBenchmarks` against 6.1.0:

| arm | 100 teams | 1,000 | 10,000 |
|---|---:|---:|---:|
| `InnerAddAndRemove` | 120.4 → **135.3 ns**, 1.12x | 179.9 → **192.1 ns**, 1.07x | 218.6 → **242.8 ns**, 1.11x |
| `EnumerateByIndexer` | 5,771 → **6,034 ns**, 1.05x | 120,905 → **129,448 ns**, 1.07x | 1,647,196 → **1,767,893 ns**, 1.07x |
| `EnumerateThenSweepByIndexer` | 5,829 → 5,845 ns | 62,914 → 59,013 ns | 656,742 → 659,845 ns |
| `Enumerate` | 339.8 → 339.4 ns | 3,220.0 → 3,217.8 ns | 33,465.8 → 33,447.6 ns |

`InnerAddAndRemove` allocates 288 B and each enumeration 40 B, as in 6.1.0. **Following a change to an inner sequence costs 7% to 12% more and reading the flattening by index 5% to 7% more.** Each read by index now looks up the copy of the sequence it lands in. How much of either belongs to this change and how much to what 6.1.1 through 7.0.1 changed before it, these runs cannot say.

### Concatenation

`ConcatIndexerPenaltyBenchmarks` against 6.1.0: a maximum over two concatenated lists 45.42 → 45.27 μs, over two concatenated projections 48.82 → 49.61 μs, and a filter over a concatenation of filters 1,917.83 → 2,000.70 μs with a standard deviation near 100. A concatenation of queries of this library reads as it did, and only a foreign second operand is observed.

### The figure the readmes quote

`QueryLambdaReuseBenchmarks` against 6.1.0: `ObserveWhere` over 256 one-element collections with a held predicate, 117.88 → **128.26 μs**, and with the predicate written inline, 14,228.80 → **14,644.83 μs**, whose standard deviation is 32% of it. Both readmes now quote these. Every arm sharing its lambda is 3% to 13% slower than in 6.1.0 and allocates what it did or up to 4% more.

## What remains

- **What 7.0.1 costs to build a query** is taken up in `2026-09-28-the-waits-under-the-locks.md`, which separates it over an empty collection, where no expression is observed. The flattening's inner change and its indexer, and building over one element, still compare only with 6.1.0.
