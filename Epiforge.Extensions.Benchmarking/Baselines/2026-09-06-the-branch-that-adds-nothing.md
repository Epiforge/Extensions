# Benchmark comparison, 6 September 2026 — the branch that adds nothing

`&&`, `||`, `??` and `?:` have been refused as `DeferredBranch` since the fast path was written, and `2026-08-31-deferred-branches.md` recommended against ever admitting them. The obstacle recorded there is exact: the fast path plans and attaches once, so it would attach to sources in branches the graph has not yet touched, and `SubscriptionAgreement.AssertAgreement` compares plan against graph with `CollectionAssert.AreEqual`.

That obstacle is real and is still the reason `?:` stays refused. It is also narrower than it reads, and three tests measured how much narrower.

This is unreleased work.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## The three facts, each measured before anything was written

**The graph is lazy at the leaf, not at the operator.** `ObservableBinaryExpression.OnInitialization` and `ObservableConditionalExpression.OnInitialization` both construct *and* `SubscribeDependent` every operand immediately; only the leaf's evaluation is deferred, and `ObservableMemberExpression` attaches inside that. `TheGraphDoesNotSubscribeToTheRightOperandOfAndAlsoUntilItIsTaken`.

**It never lets go.** `person.Rank > 0 && other.Rank > 0` holds one attachment before the left is true, two after, and stays at two when the left goes false again. A latch, not a re-decision. `TheGraphKeepsItsSubscriptionToTheRightOperandOfAndAlsoAfterItStopsBeingTaken`.

**Where both operands read one object, the graph's set of sources does not change at all across that transition.** `s.Rank > 0 && s.Score < 100` goes from one attachment to two, but both are `PropertyChanged` on the same object, and `SubscriptionLog.Describe` — like `AssertAgreement` and `Planned` — keys on object and event, not property name. `TheGraphsSourcesForAndAlsoOverOneObjectDoNotChangeWhenTheBranchIsTaken`.

The third is what makes admission possible with no change to the executor: an eager plan agrees with the graph at every moment, before the branch is taken and after.

## What changed

```csharp
BinaryExpression binaryExpression when IsShortCircuiting(binaryExpression) => AnalyzeShortCircuiting(binaryExpression, planner),
```

`AnalyzeShortCircuiting` analyzes the left operand, snapshots `planner.Subscriptions.Count`, analyzes the right, and admits the expression only if every subscription in the tail matches one in the head by `Source` reference and `Kind`. A conversion-bearing coalesce is refused explicitly, since it used to be caught by the blanket refusal downstream of this arm. `Analyze` now runs a planner so it cannot disagree with `Plan`; the observer only ever calls `Plan`, so nothing on a hot path pays for it.

`?:` is untouched and still refused. Nothing in the corpus measures it, and a shape that cannot be priced should not be admitted.

## The measurement

| | before, both arms | after, direct | after, graph |
|--- |---: |---: |---: |
| `ShortCircuitOverOneProperty` | 4.49 MB | **1.24 MB** | 4.49 MB |
| `ShortCircuitOverTwoMembers` | 5.05 MB | **1.56 MB** | 5.05 MB |
| `DeferredBranchOverAPropertyChain` | 6.45 MB | **1.56 MB** | 6.45 MB |
| `EligibilityMatrix.ShortCircuit` | 4,595.68 KB | **1,269.8 KB** | 4,595.69 KB |

In time, `DeferredBranchOverAPropertyChain` went from 7,831.8 μs to **515.5 μs**, and `ShortCircuitOverOneProperty` from 4,532.7 to **437.7**.

The largest of these was the most expensive shape a caller could write in this corpus. It now allocates 24% of what it did and builds in 6.6% of the time.

**Every shape in the measured corpus is now eligible.** The census of 6 September found eleven of thirteen; the chain change took it to twelve; this takes it to thirteen.

## An account which closes, and a new constant

The direct arms are not flat this time, and the steps are the finding.

| shape | direct | distinct sites |
|--- |---: |---: |
| `RankComparison` | 1.17 MB | 1 |
| `ShortCircuitOverOneProperty` | 1.24 MB | 1 |
| `ShortCircuitOverTwoMembers` | 1.56 MB | 2 |
| `DeferredBranchOverAPropertyChain` | 1.56 MB | 2 |

`person.Rank > 0 && person.Rank < 100` reads one member twice and costs 0.07 MB more than reading it once — the larger compiled delegate, and nothing else, because the two reads plan one site between them. `person.Rank > 0 && person.Name != null` reads two members and costs 0.32 MB more than that.

**A second distinct subscription site costs the fast path 0.32 MB per thousand elements — 320 bytes per element.** It closes across two independent shapes: `ShortCircuitOverTwoMembers` and `DeferredBranchOverAPropertyChain` differ by a modulo node and a member hop, both worth 0.6 to 0.7 MB on the graph, and land on the same 1.56 MB on the fast path. The fast path charges for sources, not for nodes; the graph charges for nodes. That is the whole difference between the two mechanisms, and it is now measured from both sides.

That one site of `ShortCircuitOverOneProperty` is a model rather than an observation: it rests on the two `person.Rank` reads reaching `Planner.Reached` as one expression, which the arithmetic fits and which nothing here checks directly.

## The controls

`RankComparison` reproduced at 1.17 MB direct and 2.62 MB graph in all three classes. `PropertyChain` held at 1.17 and 3.25. `ArgumentIndependent` held at 801.05 KB and 813.17 KB, `StaticFieldTarget` at 1,301.28 and 3,023.88, all exact.

One arm moved and is not explained: `StaticPropertyValueGraph` read 2,903.04 KB against 2,896.77 recorded this morning, 0.2% higher on an expression containing no short-circuit and no chain, whose direct arm reproduced to the byte. Nothing in either change reaches it. Flagged, not explained, and not worth a run of its own.

## Between-run drift, measured properly for once

Three classes ran, of which one was needed; the other two re-measured shapes already covered. The one thing they bought that the single class could not is a calibration the orientation has been quoting without evidence.

`RankComparisonGraph` is byte-identical at 2.62 MB in all three, and its mean read 2,122.6 μs, 2,304.6 μs and 2,403.4 μs. The first two are both `launchCount: 3` runs of an identical arm — **8.6% apart on byte-identical allocation**, which is a cleaner figure than the "up to about 7%" the orientation has carried from a single suite. A time movement under 9% on any arm of this project, unaccompanied by an allocation change, decides nothing.

## A note on method

The request that produced these three reports cost half an hour of a machine its owner could not use, to answer a question worth three minutes. The two extra classes measure the same expressions as the class that was needed — `EligibilityMatrix.ShortCircuit` is character-for-character `ShortCircuitOverOneProperty`, and `OperatorEligibility.DeferredBranch` is `DeferredBranchOverAPropertyChain` — and both carry `launchCount: 3`. Ten arms were asked for as twenty-six.

The orientation already carried this rule, in Daniel's words, from a benchmark he vetoed as *"about an order of magnitude too high"*: 25 minutes for a question worth 90 seconds. It failed to catch this because it was written about designing a benchmark's scale, and this was a request for three existing ones. **Count the arms, multiply by the launch count, and state the minutes in the request.** A rule that only catches the shape it was born from is not a rule.
