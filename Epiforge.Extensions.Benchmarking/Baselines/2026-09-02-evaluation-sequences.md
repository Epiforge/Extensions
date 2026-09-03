# Benchmark comparison, 2 September 2026 — lazy sequences on the graph's evaluation path

Six graph node types read their operands with LINQ inside `Evaluate`, the method which runs on every change. Reading a list the node already holds, through a lazy sequence, allocated **152 bytes per evaluation** — fifty-nine percent of everything a method-call node spent.

## How this was found

By sweeping for the shape `2026-09-02-the-thirty-two-bytes.md` had just cost a day: a lambda capturing a parameter, which forces a closure at method entry. The sweep found one more of those and something larger it was not looking for.

A crude script over the Observable folder produced mostly noise — it could not tell a switch arm from a lambda, and most of its hits were `X => Y` in switch expressions, which allocate nothing. What survived a second pass by hand was a grep for LINQ operators on files whose `Evaluate` runs per change:

| node | per evaluation |
|--- |--- |
| `ObservableMethodCallExpression` | `Select(…).FirstOrDefault(…)` for the fault check, `Select(…).ToArray()` for the arguments |
| `ObservableIndexExpression` | the same two |
| `ObservableNewExpression` | the same two, the `ToArray()` written out twice in a ternary |
| `ObservableInvocationExpression` | `Select(…).FirstOrDefault(…)`, and `All(…)` |
| `ObservableMemberInitExpression` | `Keys.Select(…).FirstOrDefault(…)` |
| `ObservableNewArrayInitExpression` | `Select(…).FirstOrDefault(…)` |

None of these lambdas capture anything, so the delegates are cached statics and the closure trap does not apply. What is allocated is the sequence machinery itself — an iterator per `Select`, and the enumerator it boxes out of the source — on every evaluation, over a list the node holds in a field.

## The instrument

`GraphEvaluationBenchmarks`, three arms, `DefaultJob`, a thousand elements, all on `UseDirectSubscription = false` so the graph is what runs. Every arm flips the same property on the same objects; the two observed arms differ only in the kind of node the predicate builds, and **neither predicate's result ever changes**, so the arms measure evaluation without any notification riding along.

Two of the three arms were predicted exactly before the before-run, which is what made the third readable.

| arm | predicted | before | account |
|--- |---: |---: |--- |
| `FlipEveryNameWithNoObservation` | 46.88 KB | 46.88 KB | 48 B, the element's two `EventArgs` |
| `FlipEveryNameObservedAsAMemberComparison` | 70.31 KB | 70.31 KB | and 24 for the boxed `int` from `.Length` |
| `FlipEveryNameObservedAsAMethodCall` | not predicted | **250 KB** | 256 B, of which 104 could be named |

The 104: 48 for the floor, 32 for the one-element argument array, 24 for the `bool` which `FastInvoke` boxes on the way out. **The other 152 was the sequences.** Its decomposition across the four objects involved was not attempted — the count of objects removed is knowable, their individual sizes are not, and guessing at them is the failure this series keeps recording.

## The change

Two generic helpers on `ObservableExpression`, `FirstFault` and `EvaluationResults`, both plain indexed loops. Eleven call sites across six node types now use them.

They are **generic over the list type** rather than taking `IReadOnlyList<ObservableExpression>`, and that is not gratuitous: `ObservableIndexExpression` holds an `EquatableList<T>`, which is a readonly struct. Passing it as an interface would box it, and the change would have replaced one allocation with another.

`FirstFault` stops at the first faulting expression and reads no further. That is what makes it equivalent rather than merely similar: the `Evaluation` getter triggers deferred evaluation, so reading more operands than the lazy sequence did would be a behavior change, not just a slower one.

`ObservableMemberInitExpression` iterates the keys of a dictionary rather than a list, so it keeps a small private loop of its own, with a note saying why it is not using the shared helper. `ObservableNewExpression`'s duplicated `.ToArray()` — the same expression written into both arms of a ternary — collapsed to one local.

## Result

| arm | before | after | |
|--- |---: |---: |---: |
| `FlipEveryNameWithNoObservation` | 8.822 μs / 46.88 KB | 8.742 μs / **46.88 KB** | control |
| `FlipEveryNameObservedAsAMemberComparison` | 60.296 μs / 70.31 KB | 58.057 μs / **70.31 KB** | control |
| `FlipEveryNameObservedAsAMethodCall` | 64.783 μs / 250 KB | **45.857 μs / 101.56 KB** | **2.46×** |

**Predicted 101.56 KB. Measured 101.56 KB.** Both controls byte-identical.

Per evaluation a method-call node fell from 208 bytes to 56 — the argument array and the boxed result, and nothing else.

On time, the arm is **29.2% faster** while the two controls moved 0.9% and 3.7% in the same run. That gap is wide enough to call the direction real; no per-object figure is quoted from it, for the reason given in `2026-09-02-graph-boolean-boxes.md`.

## What is left on this path

The 56 bytes a method-call node still spends are both justified: `FastInvoke` needs an `object?[]`, and it boxes what it returns. But **the boxed result is a `bool` here, and the shared boolean boxes do not reach it** — those cover the binary, unary, type-binary, coalesce and short-circuit nodes, whose delegates the observer compiles itself, and not the reflection path a method call or a member read goes through. A method call returning `bool` and a property returning `bool` each box one per evaluation. That is a separate change against `FastInvoke`'s callers and wants its own before-and-after.

Two LINQ uses were deliberately left:

- `ObservableNewExpression.OnInitialization` builds its parameter types with `Select`. Construction, not evaluation, and cold.
- `ObservableIndexExpression`'s dictionary-changed handler runs `Any` and `FirstOrDefault` over lambdas capturing a local `key` — a display class plus **two delegates which cannot be cached**, per notification. This is the closure shape again, and the reason it is still here is that nothing benchmarks a dictionary's propagation path at all. Building that instrument comes before fixing this.
