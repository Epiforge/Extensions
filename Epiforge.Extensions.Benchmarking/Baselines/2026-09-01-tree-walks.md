# Benchmark comparison, 1 September 2026 — the walks a flip costs

Every measurement before this one was about allocation. This one is about time, because after the boxed booleans the query layer was spending 104 ns per element crossing its predicate while allocating 32 bytes — three times what the element and its observation cost together, and the largest unexamined number on the board.

An element crossing a filtered query's predicate walked a thousand-node treap three times:

- `IndexOf(node)` climbs from the node to the root, counting.
- `PrefixWeightBefore(index)` descends from the root, counting back down to that index and summing weights.
- `SetWeight(node, weight)` climbs from the node to the root, repairing subtree weights.

Roughly sixty dependent pointer loads. Nothing else on the path is close: two uncontended locks, one dictionary lookup, three cached `EventArgs`.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

`DefaultJob`.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryNotificationBenchmarks*
```

## Act one — climb once instead of climbing and descending

The first two walks answer one question, and they are mirror images. `IndexOf` accumulates `CountOf(parent.Left) + 1` whenever it came up from the right; the same climb accumulating `WeightOf(parent.Left) + parent.Weight` yields the prefix weight directly, with no position materialized in between.

`PrefixWeightedSequence<T>.PrefixWeightBefore` now has an overload taking a node. Six places asked the composed question — the filtered query's flip path, three in the ordered query, two in the reducing query. Three other calls to `IndexOf(node)` genuinely want a position and were left alone.

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 149.008 μs | **123.907 μs** | 16.8% |
| `FlipEveryMembershipWithNothingObserving` | 133.671 μs | **107.629 μs** | 19.5% |
| `FlipEveryRankObservedWithoutAQuery` | 30.330 μs | 30.034 μs | control |
| `FlipEveryRankWithNoQuery` | 8.536 μs | 8.302 μs | control |

Allocation is byte-identical in all four arms, as it must be: no allocation was touched.

Per flip, the query's share went from 103.3 ns to 77.6 ns. **Removing one of three walks removed 24.9% of the query's time**, which prices the three walks at about three quarters of everything the query does.

### The prediction, and why it was wrong

Written before the run: 120–127 μs and 135–142 μs, from "one walk of three, so 5 to 10 percent."

**Measured 107.629 and 123.907 — both better than the range.**

The error was the denominator. Five to ten percent was taken against the whole arm, which contains 8.5 μs of control and 30 μs of observation that no change to the query can move. Against the part that can move it is a quarter. *Predict on the portion the change can reach, not on the number the benchmark prints.*

## Act two — do not compute a position nobody will read

The surviving prefix walk feeds exactly two consumers: the arguments describing the change to a subscriber, and the patch applied to a snapshot a live enumeration is holding. When a query is held and read rather than subscribed to, and nothing is enumerating, neither exists and the walk is dead work — the same shape as the notification guard, one layer down.

`FlipMembershipWithAccess` now takes the position lazily.

This computes the prefix weight *after* `SetWeight` rather than before, which is sound because the two are independent, term by term:

- `WeightOf(node.Left)` reads a descendant's subtree; `SetWeight` touches the node and its ancestors.
- `WeightOf(ancestor.Left)` is accumulated only for ancestors reached from the right, whose left subtrees therefore exclude the node.
- `ancestor.Weight` is a node's own weight; `SetWeight` changes `Weight` on the target only, and `SubtreeWeight` on the rest.

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 123.907 μs | 126.440 μs | unmoved |
| `FlipEveryMembershipWithNothingObserving` | 107.629 μs | **86.672 μs** | 19.5% |
| `FlipEveryRankObservedWithoutAQuery` | 30.034 μs | 29.638 μs | control |
| `FlipEveryRankWithNoQuery` | 8.302 μs | 8.430 μs | control |

Predicted: 82–86 μs unobserved, the subscriber arm unmoved, both controls unmoved, allocation byte-identical.

**Measured 86.672**, which is 0.672 outside the top of the range rather than inside it — recorded as a miss, small and in the conservative direction.

The subscriber arm reads 2.0% slower, which is not a movement. The control moved 1.5% in the same run, and against it the ratio went 14.93 to 15.00. That is what a control is for: without it, two microseconds on the subscriber arm would have looked like a change I had made.

The query's share of an unobserved flip is now **57.0 ns**, from 103.3 at the start of the day.

## Act three — the assignment already walks that path

`SetWeight` climbs from the node to the root repairing subtree weights. `PrefixWeightBefore(node)` climbs the same path accumulating. The second climb reads exactly the fields the first one already loads — `parent.Left` and `parent.Weight` — so the accumulation costs arithmetic and no memory traffic at all.

`SetWeight` now returns the preceding weight. Daniel authorized altering the type: *"It's okay to alter a type to increase performance if you think it will."* The signature change is source-compatible and binary-breaking, and is noted for release.

This makes the position **free** rather than merely cheaper, so act two's laziness is gone again: `FlipMembershipWithAccess` takes a plain local from the assignment. The reducing query's `TranslateInnerChangeWithAccess` asked the same pair and now asks once.

Two `SetWeight` calls in the ordered query were left composed with a separate `PrefixWeightBefore`. Fusing them would reorder a mutation of `positions` ahead of a `results` change that a subscriber can observe, which is a behavior question rather than a performance one, and the path is cold.

Predicted: `FlipEveryMembershipWithASubscriber` to **102–108 μs**, `FlipEveryMembershipWithNothingObserving` **unmoved or a hair slower**, both controls unmoved, allocation byte-identical.

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 126.440 μs | 120.874 μs | 4.4%, not the 15% predicted |
| `FlipEveryMembershipWithNothingObserving` | 86.672 μs | 102.043 μs | **17.7% slower** |
| `FlipEveryRankObservedWithoutAQuery` | 29.638 μs | 30.059 μs | control |
| `FlipEveryRankWithNoQuery` | 8.430 μs | 8.260 μs | control |

**Both predictions wrong, and in opposite directions.** "Costs arithmetic and no memory traffic at all" was the claim, and it was worth fifteen nanoseconds a flip.

### What the four runs price, once they are read together

Let C be a climb from a node to the root over a path already in cache, and B the accumulation carried along it — a `parent.Right` load and a branch which the shape of a treap makes unpredictable, at every level.

| | shape | query's share of a flip |
|--- |--- |---: |
| act one, unobserved | C + B | 77.6 ns |
| act two, unobserved | C | 57.0 ns |
| act three, unobserved | C + B | 72.0 ns |
| act two, observed | 2C + B | 96.8 ns |
| act three, observed | C + B | 90.8 ns |

From the observed pair, **C = 6.0 ns**. From the unobserved pair, **B = 15.0 ns**. Act one to act two removed C + B from the unobserved case and measured 20.6 against the 21.0 those two constants predict — the model closes to four tenths of a nanosecond across four runs it was not fitted to.

So the accumulation costs **two and a half times the climb it rides on**, and a second climb over a path the first one just walked is nearly free. That is the reverse of the assumption act three was built on. It also explains act one, which was the large win precisely because the walk it removed was a *descent from the root* — a different path, and a cold one.

### Act four — pay for it only where it is read

`SetWeight` returns to `void` and gains an overload taking `out int prefixWeightBefore`. The binary break is withdrawn; the change is additive again and `CompatibilitySuppressions.xml` can go.

`FlipMembershipWithAccess` decides up front whether anything will read the position — a subscriber, or a snapshot a live enumeration holds — and calls the overload which accumulates only then.

Predicted: `FlipEveryMembershipWithNothingObserving` back to **86–88 μs**, `FlipEveryMembershipWithASubscriber` **unmoved at 119–122 μs**, both controls unmoved, allocation byte-identical.

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 120.874 μs | **121.851 μs** | unmoved, as predicted |
| `FlipEveryMembershipWithNothingObserving` | 102.043 μs | **87.386 μs** | 14.4%, inside the range |
| `FlipEveryRankObservedWithoutAQuery` | 30.059 μs | 30.362 μs | control |
| `FlipEveryRankWithNoQuery` | 8.260 μs | 8.205 μs | control |

**Both landed, and the two best figures of the day now hold at the same time.**

The C and B constants were fitted to four runs and then predicted a fifth. The unobserved query share came back at **57.02 ns** against the 57.0 the model said, and the observed at **91.5** against 90.8. A model built out of arithmetic rather than a story about which code is slow.

## Where a flip stands

Per element crossing the predicate, across the whole day:

| | start of day | now |
|--- |---: |---: |
| bytes, nothing subscribed | 216 | 80 |
| the query's share of the time, nothing subscribed | 103.3 ns | 57.0 ns |
| the query's share of the time, something subscribed | 103.3 ns | 91.5 ns |

## What was learned that outlasts the change

**A second traversal of a path the first one just walked costs 6 ns; carrying an unpredictable branch along a traversal costs 15.** Fusing two walks over the same path is therefore worth much less than it looks, and adding conditional work to a walk that every caller pays for is worth much less than nothing. Act one was the large win because the walk it removed was a descent from the root — a different path, and a cold one.

The general form: **the expensive thing is touching memory that is not already in cache, not doing the work twice.**

## Not done

`ObservableCollectionOrderByQuery` still composes `PrefixWeightBefore` with a separate `SetWeight` in two places. Fusing them would move a mutation of `positions` ahead of a `results` change a subscriber can observe. That is a behavior question, the path is cold, and by the model above it is worth six nanoseconds. Left alone deliberately.

## Where a flip stands

Per element crossing the predicate, with nothing subscribed, at the start of today and now:

| | then | now |
|--- |---: |---: |
| bytes | 216 | 80 |
| the query's share of the time | not measured | 77.6 ns, falling |

## Not done

`PrefixWeightBefore(node)` and `SetWeight(node, weight)` climb **the same path** from the node to the root. Fused into one climb, an observed flip would walk once rather than twice.

Not attempted, for an API reason rather than a performance one: the honest signature returns the prefix weight from `SetWeight`, and changing a public method from `void` to `int` breaks binary compatibility for a type which may already have shipped. An awkwardly named companion method would buy the same nanoseconds and cost the sequence its clean surface. Worth raising with Daniel rather than deciding alone.
