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

Predicted: `FlipEveryMembershipWithASubscriber` to **102–108 μs**, `FlipEveryMembershipWithNothingObserving` **unmoved or a hair slower** since it now computes a position it may not read, both controls unmoved, allocation byte-identical.

*(Results to be recorded on the next run.)*

## Where a flip stands

Per element crossing the predicate, with nothing subscribed, at the start of today and now:

| | then | now |
|--- |---: |---: |
| bytes | 216 | 80 |
| the query's share of the time | not measured | 77.6 ns, falling |

## Not done

`PrefixWeightBefore(node)` and `SetWeight(node, weight)` climb **the same path** from the node to the root. Fused into one climb, an observed flip would walk once rather than twice.

Not attempted, for an API reason rather than a performance one: the honest signature returns the prefix weight from `SetWeight`, and changing a public method from `void` to `int` breaks binary compatibility for a type which may already have shipped. An awkwardly named companion method would buy the same nanoseconds and cost the sequence its clean surface. Worth raising with Daniel rather than deciding alone.
