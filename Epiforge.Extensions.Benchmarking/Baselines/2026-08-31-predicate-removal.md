# 31 August 2026 — removing by predicate

`ObservableRangeCollection<T>.RemoveAll(Func<T, bool>)` removes each matching element with `RemoveAt`. That is three notifications and one array shift per element removed. `ResetRemovingAll(Func<T, bool>)` walks the collection once, keeps the survivors, and raises a single `Reset`.

## What it costs today, and what it costs now

Both arms measured before either was written: the second is the proposed implementation in user space, so nothing had to be built to find out whether it was worth building.

| elements | removed | `RemoveAll` | one pass | ratio | `RemoveAll` alloc | one pass alloc |
|---:|---:|---:|---:|---:|---:|---:|
| 1,000 | 1% | 28.88 μs | 23.37 μs | 0.82 | 1.23 KB | 4.05 KB |
| 1,000 | 25% | 47.85 μs | 22.90 μs | 0.48 | 26.64 KB | 4.05 KB |
| 1,000 | 75% | 88.50 μs | 21.38 μs | 0.25 | 81.52 KB | 4.05 KB |
| 16,000 | 1% | 1,035.25 μs | 121.01 μs | 0.12 | 17.85 KB | 62.64 KB |
| 16,000 | 25% | 24,682.97 μs | 107.17 μs | 0.004 | 422.95 KB | 62.64 KB |
| 16,000 | 75% | 73,627.66 μs | 75.82 μs | 0.001 | 1,300.24 KB | 62.64 KB |

Three quarters of sixteen thousand elements: **73.6 ms against 76 μs, 971×.**

## What shipped, measured against what was proposed

The member walks `Items` directly and re-adds the survivors by index; the user-space arm goes through `Collection<T>`'s public indexer, re-reads `Count` every iteration, and hands `Reset` an `IEnumerable<T>` that has to be enumerated through a boxed enumerator. Predicted: the member wins slightly.

| elements | removed | `RemoveAll` | `ResetRemovingAll` | user-space | member vs. user-space |
|---:|---:|---:|---:|---:|---:|
| 1,000 | 1% | 28.47 μs | 28.47 μs | 24.30 μs | 1.17 |
| 1,000 | 25% | 50.01 μs | 28.72 μs | 24.01 μs | 1.20 |
| 1,000 | 75% | 86.06 μs | 25.53 μs | 20.41 μs | 1.25 |
| 16,000 | 1% | 1,037.66 μs | 74.01 μs | 117.94 μs | 0.63 |
| 16,000 | 25% | 24,805.15 μs | 68.48 μs | 104.14 μs | 0.66 |
| 16,000 | 75% | 73,657.37 μs | 55.75 μs | 75.83 μs | 0.74 |

At 16,000 elements the member wins by 1.36× to 1.59× — the predicted direction, a larger margin than "slightly" suggested. Allocation is 62.60 KB against 62.64 KB, so the win is work, not memory.

At 1,000 elements the member is consistently **slower** than the arm it was derived from, by about 4 μs. The difference is roughly 5σ on the means and so is not noise, and I have no mechanism for it. Every configuration at that size sits within 8 μs of the ~20 μs harness floor, where a 4 μs ordering is 20% of the measurement and the thing being measured is a fifth of it. Recorded as unexplained rather than explained badly; the member exists for large collections and wins decisively there.

## An optimisation this benchmark never exercises

The implementation scans for the first matching element before allocating the survivor list, so a predicate that matches nothing allocates nothing. In this benchmark the predicate is `value % 100 < RemovedPercent` over values starting at zero, so element zero matches in all six configurations and the scan breaks immediately. **The optimisation is never entered and its cost is unmeasured.**

Its cost is bounded by inspection: element reads become n + f where f is the index of the first match, against n without it, while predicate calls stay at exactly n either way. A collection whose only matching element is last therefore pays about twice the reads, in exchange for allocating nothing in the no-match case.

`RangeCollectionFirstMatchBenchmarks` measures that directly. Both the First and Last cases remove exactly one element, so the rebuild is identical and only the prefix re-read differs.

| elements | first match | mean | allocated |
|---:|---|---:|---:|
| 1,000 | first element | 27.96 μs | 4,104 B |
| 1,000 | last element | 25.08 μs | 4,104 B |
| 1,000 | no match | 18.36 μs | **0 B** |
| 16,000 | first element | 68.85 μs | 64,104 B |
| 16,000 | last element | 75.87 μs | 64,104 B |
| 16,000 | no match | 23.32 μs | **0 B** |

The worst case costs 10% at 16,000 elements, inside a standard deviation of 12 μs, and is faster than the best case at 1,000. Doubling the reads is not measurable against everything else the method does — I said the reads were the cheap half of the work and understated it. The no-match case allocates **nothing at all**, against 64,104 bytes, and runs in a third of the time.

The trade is worth making, and now demonstrated rather than asserted.

## The prediction, which was wrong

I predicted the two arms would cross over in time, and that the single pass would "lose badly at 1%" because it rebuilds a thousand elements to remove ten. It does not lose anywhere in the measured range. At the corner where I expected the worst result for it — 16,000 elements, 1% removed — it wins by 8.6×.

The error was comparing "work proportional to n" against "work proportional to k" without weighing the constants. A single sequential pass over n elements is about the cheapest thing a collection can do; the constant on it is a few nanoseconds per element. The constant on the per-element removal path is the array shift, and it is not small.

## Why `RemoveAll` is shaped the way it is

`List<T>.RemoveAt(i)` copies everything after `i` down one slot. Removing k elements scattered through n therefore moves on the order of n·k/2 elements — quadratic in the removal fraction, and the measurements say so with no ambiguity.

Marginal cost per element removed, net of the ~20 μs harness floor that both arms pay:

- 1,000 elements: 0.085 μs and 0.091 μs at the two densities that can resolve it
- 16,000 elements: 5.84, 6.15 and 6.13 μs across all three densities

Flat in the removal fraction at each size, as O(n·k) predicts, and 68× more expensive at 16× the elements. The extra 4.3× is bandwidth: at 1,000 elements the average shift is about 1.25 KB and the run sustains roughly 15 GB/s; at 16,000 it is about 20 KB and sustains roughly 3.3 GB/s. That last pair is inferred by dividing measured bytes by measured time, not measured independently.

## Allocation, where the old member still wins

`RemoveAll` allocates **108 to 114 bytes per element removed** and nothing per element kept — six independent rows agree on that constant to within 6%, and it is flat in the size of the collection. Roughly 96 bytes of it is a `NotifyCollectionChangedEventArgs` and its single-item list, per removal; the rest is the list of removed items that `RemoveAll` builds and then throws away (see below).

The single pass allocates **4 bytes per element in the collection**, plus one event, and nothing per element removed — 62.64 KB at 16,000 elements is 64,000 bytes of `List<int>` backing store and change.

So the allocation crossover is 108k against 4.1n, or **k/n ≈ 3.8%**. Below that, `RemoveAll` allocates less. That is the one honest caveat in the new member's documentation, and it is the reason the caveat is there.

## Decisions taken

**It returns `int` and there is no `GetAndResetRemovingAll`.** Materialising the removed items is precisely the cost this member exists to avoid; a caller who wants them should call `GetAndRemoveAll`. The asymmetry with `RemoveAll`/`GetAndRemoveAll` is deliberate.

**It ignores `RaiseCollectionChangedEventsForIndividualElements`.** The member's name says what it does; a flag that turned `ResetRemovingAll` into per-element events would make the name a lie. A test pins that both settings produce identical event sequences.

**A predicate that matches nothing raises nothing and allocates nothing.** The implementation scans for the first match before allocating the survivor list, so the no-match case costs one pass and no memory. `Reset(IEnumerable<T>)` always raises; a member whose name begins with "Reset" arguably should too, but announcing a change that did not happen is worse than the inconsistency.

**It is on the interface, not just the class.** That makes it visible to anyone holding an `IObservableRangeCollection<T>`, and it makes the release a major one, since adding an abstract member to a public interface is a break for any third-party implementer.

Package validation says so, as `CP0006` on each of the five target frameworks. Note that bumping `Version` does not clear it: ApiCompat compares against `PackageValidationBaselineVersion`, which knows nothing of semantic-versioning intent, so a break stays a break until the baseline moves past the version that lacked the member.

## An obligation this release incurs

Clearing `CP0006` writes `Epiforge.Extensions.Collections\CompatibilitySuppressions.xml`, generated by rebuilding once with `/p:ApiCompatGenerateSuppressionFile=true`. It is the first such file in this repository.

**It must be deleted when `PackageValidationBaselineVersion` moves past 3.1.0.** Until then it records one intentional break; after then it silently absorbs unintentional ones, which is the opposite of what package validation is for. It is not covered by `.gitignore` and so travels with the commit, which is the point — the record should be visible — but the same visibility is what makes a stale one dangerous.

The alternatives were a default interface method, rejected because a default is not callable on the concrete type without a cast and so would have forced the same logic to exist twice, and putting the member on the class alone, rejected because a performance member is least useful to exactly the consumer who programs against the abstraction.

## Found and not fixed

**`RemoveAll` builds a list it only measures.** It is `GetAndRemoveAll(predicate).Count` — a `List<T>` grown to k and then copied into an array, to take a length. That is roughly 12 of the 108 bytes per element removed. A private counting loop would remove it without changing any behaviour.

**`RemoveAll`, `GetAndRemoveAll` and `RemoveRange(IEnumerable<T>)` ignore the constructor flag entirely.** They raise one event per element removed whatever the collection was told. Fixed the same day; `2026-08-31-honouring-the-flag.md` carries the correction and the measurements.

What that means depends on what the flag is for, and I had it backwards. I read `RaiseCollectionChangedEventsForIndividualElements` as a performance switch — false meaning "batch for speed", true meaning "stay compatible". It is not. It exists so that a collection can be bound to a consumer that cannot process a `NotifyCollectionChangedEventArgs` carrying more than one item at all; WPF's `ListCollectionView` is the canonical one, and it throws rather than degrading. So `true` is a **prohibition** on multi-item events, and `false` is a **permission** to emit them.

Under that reading the audit comes out differently, and better:

| method | flag true | flag false |
|---|---|---|
| `InsertRange`, `MoveRange`, `RemoveRange(int, int)`, `ReplaceAll`, `ReplaceRange` | per element | one multi-item event |
| `RemoveAll`, `GetAndRemoveAll`, `RemoveRange(IEnumerable<T>)` | per element | **per element** |
| `Reset(IEnumerable<T>)` | one `Reset` | one `Reset` |

The three offending methods are already **correct** under the prohibition — they never emit a multi-item event, so no consumer of a flag-`true` collection can be harmed by them or would see any change if they were fixed. They are wrong only in never taking the permission they are granted. That makes the fix cheaper than first assessed: the behaviour change reaches only consumers who explicitly asked for multi-item events.

It also changes the shape of the fix. If `false` is permission to be descriptive rather than an instruction to be terse, the right event is not a single `Reset` — it is one `Remove` per contiguous run of removed elements. Runs never emit more events than today, never emit an event a flag-`false` consumer did not permit, preserve the indices such a consumer wants, and need no threshold or magic number. A single `Reset` throws away information that the permission was granted precisely to carry, and would make `ResetRemovingAll` largely redundant; runs leave it a distinct purpose, for the caller who wants exactly one event whatever the clustering.

The `<remarks>` this session added to both `RemoveRange` overloads asserts that scattered positions "cannot be described by a single event". That is true of a single `Remove` and false of everything else, and it documents a defect as though it were a property of the problem. It was mine, and it has to be rewritten or removed when the methods are.
