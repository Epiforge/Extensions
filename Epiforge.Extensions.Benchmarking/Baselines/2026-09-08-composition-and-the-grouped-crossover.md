# Composition, and the grouped crossover

*2026-09-08, second sitting — `ChainedComparisonBenchmarks` and `GroupedScaleComparisonBenchmarks`, DynamicData 9.4.33*

## Why these exist

Every comparison before them measured a single operator, which is not how either library is used. The READMEs were written and then held rather than committed, because advising on composition from single-operator figures is over-claiming.

## Grouping has a crossover, and it is the opposite shape to sorting

`GroupedScaleComparisonBenchmarks`, a thousand migrations at each size, per change above the floor:

| elements | this library | DynamicData | |
|---|---|---|---|
| 1,000 | **236.9 ns** | 604.8 ns | ours, 2.55x |
| 4,000 | **392.5 ns** | 628.0 ns | ours, 1.60x |
| 10,000 | 734.0 ns | **624.4 ns** | theirs, 1.18x |

**DynamicData's cost per migration is flat and this library's grows**, which is exactly the reverse of the ordered comparison, where theirs grew and this library's barely moved. Fitting each: this library ≈ **182 ns fixed + 0.0552 per element**; DynamicData ≈ **603 + 0.0022**, which is flat within noise. **They cross at about 7,900 elements.**

So the two operators have opposite shapes, and the general claim "this library scales better" is false. It scales better at sorting and worse at grouping.

**Allocation does not cross.** 578, 603, 578 B per migration here against 1,891, 1,938, 1,891 there — about 0.31x at every size.

**Nothing is deferred.** Reading every group after the thousand changes adds 0.1%, 0.9% and −0.8% to the totals. Both libraries do the work when the change arrives.

**The chained run predicted this before the instrument was built**: grouping a filtered view cost 243.9 ns over a thousand admitted elements and 517.0 over five thousand while DynamicData's stayed at 677.7 and 686.1. A two-point fit put the crossing at ~7,400; the three-point measurement puts it at ~7,900.

## Composition is close to additive, for both

`ChainedComparisonBenchmarks`, ten thousand elements, a filter admitting a tenth or half of them, per change above the floor:

| | 1,000 admitted | 5,000 admitted |
|---|---|---|
| filter only, ours | **8.5 ns / 0 B** | **8.7 ns / 0 B** |
| filter only, theirs | 197.1 ns / 594 B | 202.2 ns / 594 B |
| filter then group, ours | **247.8 ns** / 578 B | **519.4 ns** / 578 B |
| filter then group, theirs | 668.5 ns / 2,133 B | 696.1 ns / 2,133 B |
| filter then order, ours | 1,280.5 ns / **285 B** | **2,323.0 ns** / **305 B** |
| filter then order, theirs | **1,057.6 ns** / 646 B | 4,701.8 ns / 664 B |

Subtracting the filter-only arms gives what the second operator costs on top of a filter. For this library that is 1,272.0 ns to order a thousand admitted elements against 1,259.5 measured standalone at a thousand, and 239.3 ns to group them against 236.9 standalone — **additive to within 1%**. DynamicData's second stage comes in slightly *cheaper* in a chain than standalone (860.5 against 984.4 for ordering, 471.4 against 604.8 for grouping).

**Neither library suffers a compounding penalty for composition.** What decides a chained comparison is not the chain; it is the size of the view arriving at each stage. Filtering ten thousand elements to a thousand and then sorting puts this library on the losing side of its own sorting crossover, and the measurement bears that out: 1,280.5 against 1,057.6, a loss of 1.21x where sorting ten thousand unfiltered elements is a win of 2.98x.

**This library's filter allocates 0 B in a chain**, at both selectivities — 46.88 KB, the identical figure the unobserved arm reports. The zero survives composition.

## The instrument was wrong first, again

The first run of `ChainedComparisonBenchmarks` failed both of this library's ordered arms. The log was unambiguous: the count check passed, the ordering check passed, and only the last assertion failed. **The probe set an element's rank to `AdmittedCount - 1`, which ties with the element already holding it.** This library's search places a newly equal element before the incumbent and DynamicData's linear insert places it after, so the probe was asserting a tie-break neither library specifies — and DynamicData passed by luck of that difference. The unfiltered version used `int.MaxValue`, which is unique; adapting it to respect the filter introduced the collision unnoticed.

**It was replaced with a stronger check rather than a weaker one**: the two end elements exchange ranks, so every key stays unique, and the view must reflect the exchange and then the restoration. A no-op now fails it twice.

## The predictions, scored

Recorded before the chained run: *ours ~1,267 ns and theirs ~1,177 at a thousand admitted, theirs by ~1.08x; ours ~2,072 and theirs ~4,810 at five thousand, ours by ~2.3x; grouping ours at both, 2-3x.*

- **Ordering at a thousand admitted: this library 1,280.5 against 1,267 predicted — near exact. DynamicData 1,057.6 against 1,177 — it came in cheaper.** Ratio 1.21x rather than 1.08x.
- **Ordering at five thousand: 2,323.0 against 2,072 predicted, 4,701.8 against 4,810. Ratio 2.02x against 2.3x predicted.** Both within reach.
- **Grouping: predicted this library at both settings, and it was — 2.70x and 1.34x.** But the prediction said "grouping has no crossover", and that was wrong: it has one, just beyond the sizes this instrument used.
- **The arithmetic-on-measured-figures footing worked far better than reasoning about source had.** Every figure landed within about 12% except DynamicData's chained sort.

## What has not been measured

Whether the grouped crossover moves with the number of groups; sixteen was used throughout and a grouping into two or two hundred may behave differently. Why this library's grouped migration grows with the collection at all — the plausible suspicion is that removal from the old grouping is a linear scan, since each group holds more elements as the collection grows, but that is unverified. Chains three operators deep. Ordering or grouping something other than a filter.
