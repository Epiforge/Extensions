# The search that began at the top

*2026-09-29 — an ordering finds where an element moving far belongs, and where a new element belongs, by descending the order once*

## How this was measured

`BenchmarkRun-20260929-180934` ran `OrderByMembershipShare`, `OrderByMoveDistance` and `OrderedRandomMoveComparison` on the change, the last two with 7.0.1 as a second job. The befores are the same classes' arms in the overnight `BenchmarkRun-20260929-035504`, which ran after the race fix and before the entry held its first key. That change leaves a move's time and allocation where they were, so the befores stand for moves; adding an element allocated 32 B more before it, and the add-and-remove row says so. Each time is per change, above what the same changes cost with nothing observing them. Tests on the change: Expressions 1,051 of 1,051, Collections 653 of 653, Comparisons 48 of 48, Components 194 of 194, on each of five frameworks.

## What the profile found

After round A, a move searched outward from the element's old place by one, two, four and eight places and, when that did not bracket the destination, searched by halves between the last two places it tried. Each step of the search by halves found the element it compared by its position, walking from the element compared before it when that was nearer and from the root otherwise, so a search walked the tree once for each step. In the sandbox, moving elements to random places among ten thousand spent about 40% of its samples there: 28% finding elements by position and the rest comparing them. Moves of a couple of places were already bracketed by the first steps and never reached it. An insertion has no old place to search from and searched by halves across the whole order every time.

## What changed

- **`PrefixWeightedSequence<T>.CountWhile<TState>`** counts the items at the start of a sequence for which a test holds, which is the position at which it first does not. It descends from the root once, testing the item at each level and going right, past the left subtree and the item, when the test holds, and left when it does not. The test takes a state of the caller's, so a caller passing a static delegate allocates nothing. It requires that the test hold for every item before any for which it does not; otherwise the count is unspecified. Collections is 4.3.0, validated against 4.2.0.
- **An ordering uses it wherever the search by halves ran.** When the outward steps do not bracket the destination, and for every insertion, it counts the elements that the element should follow. The test is one static delegate over the comparer, the entry and the direction of the move, so a search allocates nothing.
- **Ties land where they did.** A new element, or one moving earlier, follows the elements whose keys equal its own; one moving later precedes them. The descent can meet the moving element itself, which is still in its old place. It counts the element as preceding only when the move is later, and the count is then reduced by one, since the element leaves the place it was counted in.
- **The comparer takes its directions as an array** rather than an `IReadOnlyList<bool>`, so each comparison reads their length and each direction without an interface call.

Expressions is 7.0.3, validated against 7.0.2.

## How it was checked

- `CountingWhileATestHoldsAgreesWithAListOfTheSameContent` builds 200 sequences of sorted items, with weights of zero to two, and checks both `<` and `<=` against every threshold from below the least item to above the greatest, against a list of the same content. `CountingWhileValidatesItsArguments` checks the null test.
- `OrderingFarMoves` orders 400 elements by a rank of 40 values, alone and then by a name of five values descending, and makes 4,000 changes: insertions (duplicates among them), removals, replacements, and changes of rank and name. Every 50 changes it checks the view's count, its content, its indexer against its enumeration, and its order. Reversing the tie rule fails both of its rows and none of the twelve ordering tests before it.
- In the sandbox, a differential run applied the same random moves, insertions and removals to views on the code before and after, and every final order, ties included, was the same.

## Predicted and measured

| per change | before | predicted | measured | | 7.0.1 | |
|---|---:|---:|---:|---:|---:|---:|
| to a random place among a thousand | 1,055.9 ns | ~720 ns | 684.1 ns | 0.65x | 1,919.8 ns | 0.36x |
| to a random place among ten thousand | 2,411.9 ns | ~1,600 ns | 1,491.7 ns | 0.62x | 4,561.2 ns | 0.33x |
| two places, a thousand | 337.4 ns | unchanged | 329.1 ns | 0.98x | 1,287.9 ns | 0.26x |
| 32 places | 636.7 ns | ~460 ns | 489.1 ns | 0.77x | 1,366.3 ns | 0.36x |
| 512 places | 700.3 ns | ~500 ns | 473.8 ns | 0.68x | 1,309.5 ns | 0.36x |
| adding and removing an element, `OrderByMembershipShare` | 896.21 ns / 1,304 B | ~800 ns | 843.12 ns / 1,272 B | 0.94x | | |

A random move allocates 96 B, and moves of 2, 32 and 512 places 87, 95 and 95 B, as before. The random moves beat their predictions and 512 places nearly met it. 32 places and adding and removing fell short: the sandbox had them at 0.72x and 0.89x. The 32 B less that adding allocates is the entry holding its first key, not this change; in the sandbox, on the same code with and without the descent, adding and removing allocated the same.

**A move's cost no longer climbs with its distance past eight places.** Before, 512 places cost more than 32; now it costs less, since past the outward steps a move costs one descent wherever it lands.

## The other libraries

| to a random place, per change | among a thousand | among ten thousand |
|---|---:|---:|
| this library | 684.1 ns / 96 B | 1,491.7 ns / 96 B |
| NMF Expressions | 583.4 ns / 603 B | 1,176.9 ns / 603 B |
| ObservableComputations | 655.5 ns / 72 B | 10,227.4 ns / 72 B |
| DynamicData | 1,098.5 ns / 432 B | 10,811.4 ns / 432 B |

NMF Expressions is fastest at both sizes, at 0.85x and 0.79x this library's time, allocating 6.3 times as much. ObservableComputations takes 0.96x this library's time among a thousand and 6.86x among ten thousand, and DynamicData 1.61x and 7.25x. The overnight run had this library at 1.84x NMF Expressions' time and 1.64x ObservableComputations' among a thousand.

## The readmes and the release notes

- Both readmes' comparison table gains two rows, an element moving to a random place in a sorted view among a thousand and among ten thousand, from this run for all four libraries, and the paragraph on sorting gains a sentence on them.
- Collections' release note describes `CountWhile`. Expressions' release note carries the figures above: 0.65x and 0.62x for random moves, 0.77x for 32 places, 0.68x for 512, 0.94x for adding and removing.
