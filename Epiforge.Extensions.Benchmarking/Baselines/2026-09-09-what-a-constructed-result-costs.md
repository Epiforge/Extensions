# What a constructed result costs

*2026-09-09 — `ObservationShapeBenchmarks`, four arms added, 1,000 elements, one change each*

## Why these arms exist

A member initialization observation used to write a changed binding into the object it had already produced and never assign its evaluation, so it announced nothing and a consumer holding the result had it mutated underneath them. The correction constructs a new object per evaluation. **The before for that correction is not recoverable by checkout, but it is recoverable by construction**: the old path allocated exactly what a member read allocates, since it evaluated the binding, boxed the result and wrote it through reflection without assigning anything. `ChangeEveryValueMemberRead` is therefore the before, and the excess over it is what the correction costs.

Two arms per shape, one per mechanism, so the fast path serves as the control that never had the defect.

## The measurement

Per change, above nothing, at 1,000 elements:

| arm | allocated | time | bytes per change |
|---|---:|---:|---:|
| nothing observing | 46.88 KB | 8.077 μs | **48.0** |
| a member read — the before | 70.31 KB | 39.871 μs | **72.0** |
| a constructor, graph | 117.19 KB | 62.360 μs | **120.0** |
| a member initializer, graph | 179.69 KB | 81.602 μs | **184.0** |
| a constructor, fast path | 70.31 KB | 36.629 μs | **72.0** |
| a member initializer, fast path | 70.31 KB | 36.719 μs | **72.0** |

**The fast path pays nothing for either shape.** Both its arms land on the member read's figure to the byte, because the compiled delegate returns a reference which needs no box and writes the member inline without reflection. Constructing an object per change costs it 24 bytes for the object and not one byte more.

**The graph pays 48 for a constructor and 112 for a member initializer**, over the same before.

## The constructor arm closes exactly

48 floor, 24 for the boxed argument, 24 for the instance, and **24 for the term already on record as unexplained on every observed method call and constructor** — the one measured on 8 September as charged once per observation rather than once per call. That is 120 against a measured 120.

**The prediction recorded before the run was 96, and it was wrong because it left that term out.** It was available and on record. A prediction which adds up a table of unit costs is only as good as the table it consults.

## The member initializer arm does not close, and the gap is 64 bytes

The same model gives 120: floor, one boxed binding value, one instance, and the same unexplained constructor term. Measured 184. **64 bytes per change are unaccounted for, and they are a cost the correction introduced rather than one it inherited.**

The leading candidate, from reading rather than measurement: `ObservableMemberInitExpression` holds its bindings in a field typed `IReadOnlyDictionary<,>`, and a binding change now runs the full `Evaluate()`, which walks that dictionary **twice through the interface** — once in `FirstMemberAssignmentFault` over `.Keys` and once in the evaluation loop. Each walk boxes a struct enumerator. The old in-place handler did neither: it reached its binding with `TryGetValue` and returned. So routing binding changes through `Evaluate()` is what exposed the cost, and the cost is enumeration rather than construction.

**Nothing looks that dictionary up by key any more.** The only `TryGetValue` was in the handler the correction deleted; every remaining use enumerates. A materialized array of the same triples would remove both boxed enumerators, the dictionary itself, and its comparer.

**A second thing the reading turned up, unverified:** the dictionary is keyed by observable expression with `ObservableExpressionEqualityComparer.Default`, and the observer caches nodes by expression, so two bindings whose expressions are equal should resolve to one node and `Add` should throw. `new Box { First = source.Rank, Second = source.Rank }` is the shape. That is a guess about behavior, which is a test not yet written, and it is written first.

## Predictions scored

Four predictions, two exact and two wrong. **Both fast path arms were predicted at 72 and measured 72.** The graph constructor was predicted at 96 and measured 120, missing a term that was already on record. The graph member initializer was predicted at 96 and measured 184, missing that same term and 64 bytes besides.

The pattern holds that a prediction summing measured unit costs lands and a prediction reasoning about what code does not need does not: the fast path was predicted from what it cannot allocate, and the graph was predicted by forgetting to consult the table.

## What has not been measured

Whether the 64 bytes are the two boxed enumerators, which the change about to be made will decide. What the same shapes cost at other element counts. Whether the unexplained 24 bytes on every observed call and constructor is one term or two.

## The after — the 64 bytes were the enumerators

The bindings moved from a field typed `IReadOnlyDictionary<,>` to an array of triples walked by index, which removed both interface-typed walks, the dictionary and its comparer. Same instrument, same day.

| arm | before | after |
|---|---:|---:|
| a member initializer, graph | 179.69 KB / 81.602 μs | **117.19 KB / 64.704 μs** |
| a constructor, graph | 117.19 KB / 62.360 μs | 117.19 KB / 64.993 μs |

**184.0 B per change to 120.0, and it lands on the constructor arm to the hundredth of a kilobyte.** Both shapes now decompose identically: 48 floor, 24 for the boxed value, 24 for the instance, 24 for the term still unexplained on every observed call and constructor. The prediction recorded before the run was 120 and that it would land on the constructor arm; it did both.

**Fourteen of fourteen control arms are identical to the hundredth of a kilobyte.** Nothing else moved, which is what a change confined to one node's own storage should look like.

**The time saving is real and the run is slower.** Every unchanged arm rose 2% to 5.5% between the two runs — the floor 8.077 to 8.283, the member read 39.871 to 41.410, the constructor 62.360 to 64.993 — so this pair carries about 4% of drift on byte-identical code. Against that band the member initializer's 81.602 to 64.704 is a 20.7% fall, and adjusted for the drift closer to 24%. It is far outside the band; the individual arm movements inside it decide nothing.

## What the correction actually costs, finally

Against the member read, which is what a binding change allocated before any of this:

- **48 B per change**, which is the object constructed plus the term charged on every observed construction.
- Not the 112 the first measurement showed. **64 of that 112 was enumeration the correction introduced by routing binding changes through `Evaluate()`, and it is gone.**
- **0 on the fast path**, which never had the defect, never boxed an enumerator, and constructs an object per change for 24 bytes and nothing else.

The release note's claim — one construction per evaluation — is now the whole truth rather than an understatement.

## And a throw nobody had hit

Written before the change, because whether it failed decided whether the array was a correction or only a saving: `new Box { First = source.Rank, Second = source.Rank }` threw `ArgumentException` from `Dictionary.Add` inside the node's own initialization, since the observer resolves two equal binding expressions to one cached node. **Shipped in 4.3.0 and 4.4.0, and dependent on mechanism** — the fast path never builds that node and served the same lambda without complaint. An array has no keys, so the shape now works on both.

**Recorded and not fixed:** two bindings sharing a node subscribe to it twice from the same dependent, so a change evaluates the observation twice. The second evaluation produces an equal value and announces nothing, so it is correct and wasteful. Deduplicating means tracking which occurrence owns the subscription and keeping the disposal count right, which is not something to ride along with a correctness fix.
