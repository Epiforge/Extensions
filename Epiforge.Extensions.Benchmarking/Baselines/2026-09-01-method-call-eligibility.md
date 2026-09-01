# Benchmark comparison, 1 September 2026 — method calls

`DirectSubscriptionAnalyzer` refused every `MethodCallExpression`, so an ordinary predicate like `person => string.IsNullOrEmpty(person.Name)` fell back to building a graph. It is now admitted when its return type is sealed and implements neither disposal interface.

Four arms, `DefaultJob`, a thousand elements. Two of them are controls which must not move.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *MethodCallEligibilityBenchmarks*
```

## Method

The benchmark was written and run **before** the change, on Daniel's instruction: *"When you want to be sure of the outcome of a change, write tests and benches in advance of the change and ask me to run befores and afters for you."*

That before run is what makes the after run readable. `MethodCallDirect` and `MethodCallGraph` allocated **identically**, 3.14 MB each, and sat 2.4% apart on time — the two observers differ in nothing but the variable under test. Without that, four numbers afterwards would be four numbers.

## Result

| arm | before | after | |
|--- |---: |---: |---: |
| `MethodCallDirect` | 2,857.6 μs / 3.14 MB | **362.9 μs / 1.19 MB** | **7.87× / 2.64×** |
| `MethodCallGraph` | 2,791.9 μs / 3.14 MB | 2,800.3 μs / 3.14 MB | control |
| `RankComparisonDirect` | 370.2 μs / 1.22 MB | 362.6 μs / 1.22 MB | control |
| `RankComparisonGraph` | 2,354.1 μs / 2.69 MB | 2,366.0 μs / 2.69 MB | control |

All three controls held: identical allocation across both runs, and time within 2.1%, 0.3% and 0.5%.

## The claim

`person => string.IsNullOrEmpty(person.Name)` and `person => person.Rank > 0` now cost **362.9 μs against 362.6 μs** — 0.08% apart, against standard deviations of 5.33 and 3.78.

A predicate calling a static method is no longer merely faster than it was. It costs what an integer comparison costs, because it takes the same path and its plan is the same shape: one property subscription on the argument. This is the same form of claim the operator work made for string equality, and it lands the same way.

## The predictions, all three of which were wrong

Recorded before the run: 380–430 μs, about 1.22–1.30 MB, and — stated explicitly — *not* landing on `RankComparisonDirect`, because `MethodCallGraph` was already 19% above `RankComparisonGraph` and the call is a real extra node.

It landed at 362.9 μs and 1.19 MB, below both ranges, **and exactly on `RankComparisonDirect`**.

The reasoning was wrong in a specific way worth keeping. The graph's extra cost for a method call is an extra *node* — an `ObservableMethodCallExpression` per element, with its subscriptions and its bookkeeping. The prediction assumed some fraction of that cost survives into the fast path. None of it does. The fast path compiles the predicate to a delegate, where the call is an instruction rather than a node, and plans subscriptions from the analysis rather than from the tree. There is no node left to be 19% more expensive.

The precedent for this was in the operator document, already read, which recorded string comparison arriving at exactly the integer comparison's cost for exactly this reason. The prediction should have been "it lands on the rank arm," and the evidence for that was on hand.

`MethodCallDirect` allocates 1.19 MB against `RankComparisonDirect`'s 1.22 MB — about 31 bytes per element **less**. The plausible account is that `person.Rank > 0` has a constant operand to freeze and `string.IsNullOrEmpty(person.Name)` has none. Not chased, and recorded as plausible rather than established.

## The correctness break this change caused, and what found it

The first version of the rule broke three tests, two of them with wrong values rather than wrong reason codes.

`ExpressionObserver.GetObservableExpression` rewrites a call to a property get-method into the member or index access it stands for, in an arm which sits **before** its general method-call arm. **C# compiles every indexer read to a `get_Item` call**, so every indexer in the codebase reaches the analyzer as a `MethodCallExpression`. The new arm caught them first, declared `p1[5]` on an `ObservableDictionary<int, int>` eligible because `int` cannot be disposed, and planned none of the indexer's subscriptions. The observation stopped re-evaluating when the dictionary changed.

The analyzer now refuses a call to a property or indexer get-method outright, in an arm placed immediately before the general one, mirroring the observer's dispatch order.

**Mirroring the rewrite instead of refusing would likely make an indexer on the argument newly eligible**, since the index analysis accepts a fixed target. That is a separate proposal and should get its own before-tests and its own benchmark rather than riding along with the fix for a break it resembles.

The hazard had been identified before the change was written, from the *disposal* mirror of that normalization, and then dismissed on the grounds that C# compiles a property read to a `MemberExpression` — true for parameterless properties, false for indexers. Half the claim was checked; the whole claim was believed. The dispatch which actually mattered was never read, because only the disposal path had been searched.

The rule that follows is more general than indexers: **the observer normalizes some node kinds before it dispatches, so a new arm in the analyzer has to be checked against that entire switch, not against the node type it names.**

## What the before run bought

Two tests written ahead of the change asserted that the graph attaches nothing for a call beyond what its object and arguments attach — the claim the whole rule rests on. They passed before it, which turned an inference about somebody else's code into a measurement of it.

They did not catch the indexer break, because neither used an indexer. A test which would have — asserting that a compiled `people[0]` is refused, and asserting first that its body really is a `MethodCallExpression` so it cannot pass vacuously — now exists as `PropertyGetMethodCallIsIneligible`. It was not written earlier because the case had already been reasoned away.
