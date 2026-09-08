# The source named twice

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

An observation discarded a planned subscription only when another named the *same node*. That covers a member of the argument, because the compiler gives one `ParameterExpression` to every occurrence of a lambda's parameter, which is why a conditional whose branches read what its test reads fell from 1.28 MB to 1.14. It does not cover a value reached through a captured variable or a static field: two occurrences of `other` are two `MemberExpression` nodes naming one object, so each planned its own subscription, each attached its own handler, and each drove its own evaluation whenever that object announced a change. The graph merges them, because it caches nodes by expression equality.

The discard now compares sources with `ExpressionEqualityComparer` rather than `ReferenceEquals`, which is strictly wider — the comparer's first act is a reference test.

## Settling the premise first

Whether that comparer can be trusted here is a question about the comparer, not about this codebase, and it was settled with two tests before anything was written. A `ConstantExpression` contributes its *value* to the diagram, and diagram elements are compared with `object.Equals`, which for a compiler-generated closure type is reference equality. So two reads of one captured variable compare equal despite being distinct nodes, and two reads of separate variables of the same name and type — separate display-class instances — compare unequal. Both tests pass.

## What was measured

Two arms were added to price a source named twice, and a third to name what it should become. `SharedSourceRepeatedDirect` is `person => other.Rank + other.Rank > person.Rank`; `TwoSourceSumDirect` is `person => other.Rank + person.Rank > 0`, the same shape over two distinct objects. Neither defers an operand, so nothing but the duplicate is in play.

| arm | before | after |
|---|---|---|
| `SharedSourceRepeatedDirect` | 511.3 μs / 1.30 MB | **446.8 μs / 1.24 MB** |
| `SharedSourceRepeatedGraph` | 3,867.7 μs / 3.57 MB | 3,909.5 μs / 3.57 MB |
| `TwoSourceSumDirect` | 443.9 μs / 1.23 MB | 448.8 μs / 1.23 MB |

Fourteen of the fifteen arms hold their allocation to the printed digit across the pair, and the drift band on time runs from −1.7% to +2.2%. Against a band that tight, the subject arm's **−12.6%** is real, and it lands within 2 μs of the arm it was predicted to become.

## The prediction, and why it could not name one digit

The prediction was 1.23 or 1.24 MB and nothing else moving. It came in at 1.24. The two-figure spread was not hedging: after the merge the repeated expression attaches the same two handlers as `TwoSourceSumDirect`, but it still resolves its source into *two* slots of the fixed-value array, and 8 bytes per element is 0.008 MB against a printing step of 0.0105. The instrument cannot separate those two answers at a thousand elements, and saying so in advance is what made 1.24 informative rather than lucky.

## The model, which now closes on three arms

Taking `RankComparisonDirect` at 1.14 MB as one subscription and no fixed values:

| arm | subscriptions | fixed-value array | predicted | measured |
|---|---|---|---|---|
| `RankComparisonDirect` | 1 | — | 1.14 MB | 1.14 MB |
| `TwoSourceSumDirect` | 2 | `object?[1]`, 32 B | 1.23 MB | 1.23 MB |
| `SharedSourceRepeatedDirect`, before | 3 | `object?[2]`, 40 B | 1.30 MB | 1.30 MB |
| `SharedSourceRepeatedDirect`, after | 2 | `object?[2]`, 40 B | 1.24 MB | 1.24 MB |

**A subscription on the fast path costs 62 bytes per element**, solved from the pair and then confirmed against the other two rows without further fitting.

## What it does not explain

The same model puts `TwoObjectShortCircuitDirect` at 1.28 — one subscription beyond the floor, a 32-byte fixed-value array, a 32-byte flag array and the deferring observation's two extra fields — against a measured 1.31. **About 30 bytes per element on the arms with a deferred operand are still unnamed**, which is more than the 15 recorded earlier and sharpens that open question rather than closing it. Arithmetic that does not close is a finding.
