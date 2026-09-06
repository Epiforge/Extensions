# Benchmark comparison, 6 September 2026 — the chain that cannot notify

Reading a property through another property has been refused by the analyzer since the fast path was written, as `ChangeableMemberTarget`. `2026-09-06-what-the-refusals-cost.md` priced that refusal for the first time at 3.25 MB against 1.17 for a shape the fast path accepts, and observed that the refusal covers two populations which had never been separated: an intermediate value which can raise a change notification, and one which cannot.

A `string` cannot. Neither can any sealed type implementing none of the notification interfaces. For those, there is nothing to subscribe to on the intermediate at all, so the whole chain is watched by whatever its target contributes — which is exactly what the graph already does.

This is unreleased work.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

`RefusalCauseBenchmarks`.

## What changed

`DirectSubscriptionAnalyzer.AnalyzeMember` refused every member whose target was not fixed. It now refuses only those whose target could notify:

```csharp
if (!IsFixed(target))
    return CannotNotify(target.Type) ? AnalyzeNode(target, planner) : new(memberExpression, DirectSubscriptionIneligibility.ChangeableMemberTarget);
```

`CannotNotify` mirrors the existing `CannotBeDisposed` exactly — sealed, implementing none of `INotifyPropertyChanged`, `INotifyCollectionChanged`, `INotifyDictionaryChanged`. When it holds, the member contributes **no subscription of its own** and the analysis of its target stands.

Planning nothing is not a convenience, it is required. `DirectObservableExpression.Resolve` resolves constants, parameters, field chains and quotes, and throws on a property target — the crash `2026-08-31-deferred-branches.md` records. A subscription planned for such a node could not be resolved. Planning none means none needs resolving.

## Why this is agreement and not approximation

The graph decides at evaluation time, by testing the value it obtained for the notification interfaces. `CannotNotify` decides at plan time on the static type, and is strictly the more conservative test: where it holds, no runtime value of that type can notify, so the graph subscribes nothing there either. The two cannot disagree, by construction.

The fact underneath that argument was not read, it was measured. `TheGraphAttachesNothingForAMemberOfANonNotifyingValue` compares the graph's attachments for `subject.Tag` against those for `subject.Tag!.Length` and asserts they are the same list. Its partner `TheGraphAttachesToEachLinkOfAChainOfNotifyingValues` asserts that `s.Next!.Rank` attaches to both links, so the two populations are pinned apart in the test suite and not only in prose.

## The measurement

| | before | after |
|--- |---: |---: |
| `PropertyChainDirect` | 3,262.6 μs, 3.25 MB | **352.2 μs, 1.17 MB** |
| `PropertyChainGraph` | 3,245.7 μs, 3.25 MB | 3,206.3 μs, 3.25 MB |

**64% of the allocation and 89% of the time are gone.** A predicate reading the length of a name now costs, over a thousand elements, 1.17 MB where it cost 3.25.

The figure worth noticing is not the ratio. It is that 1.17 MB and 352.2 μs are what `RankComparisonDirect` costs in the same run — 1.17 MB and 355.1 μs. **A chained predicate now costs exactly what the simplest possible predicate costs**, to the resolution the instrument reports, which is what a mechanism that compiles one delegate and attaches one subscription should do regardless of how many nodes the expression would have had.

## The controls, all four of which held

| | before | after |
|--- |---: |---: |
| `RankComparisonDirect` | 1.17 MB | 1.17 MB |
| `RankComparisonGraph` | 2.62 MB | 2.62 MB |
| `ShortCircuitOverOneProperty`, both arms | 4.49 MB | 4.49 MB |
| `ShortCircuitOverTwoMembers`, both arms | 5.05 MB | 5.05 MB |
| `DeferredBranchOverAPropertyChain`, both arms | 6.45 MB | 6.45 MB |

Three refused shapes stayed refused and byte-identical on both sides, so nothing leaked past the new condition. `DeferredBranchOverAPropertyChain` is the sharpest of them: its property chain is now acceptable and it did not move at all, because `&&` refuses the whole expression before the chain is ever reached. That is the separation this pair of documents was built to make, working in the direction it was built for.

## The predictions

Allocation was predicted at "about 1.2 MB" and came in at 1.17. That prediction was a lookup rather than a story — every eligible shape measured this week lands between 1.17 and 1.30 — and it is the form which keeps working.

**No time prediction was made, and one was available.** The same lookup gives 350 to 420 μs for every eligible shape in the corpus; 352.2 was there to be predicted and was not. That is the hedging failure recorded in the orientation, committed again two days after it was written down: a number worth reporting is worth predicting, and the model for this one was sitting in the same table as the model for the other.

## Two tests failed, and one of them was already broken

`IneligibleExpressionPlansNothing` asserted `person.Name!.Length` is ineligible with reason `ChangeableMemberTarget` and plans nothing. Its subject is the general claim; its fixture stopped being ineligible. All four assertions survive verbatim on a fixture that is still refused — `recorded.Next!.Rank`.

`DisposingAnObservationTwiceDoesNotReleaseAnother` asserted `CachedObservableExpressions` is non-zero while a second observation is alive, and observed zero. The cause is that **a direct observation is not in the observer's expression cache at all**: `DirectObservableExpression.DisposeCore` latches on its own `released` field and never calls `observer.ExpressionDisposed`, unlike every graph node.

That is the finding worth keeping. Once the fixture shape moved to the fast path, three of `ObservationLifetime`'s five tests began passing **vacuously** — `IdenticalObservationsShareOneCachedExpression` was comparing zero against zero — and only the fourth noticed. Every assertion in that class is about the graph's expression cache and handle identity, so the class now constructs its observer with `UseDirectSubscription = false` and tests what it was written to test. No assertion was changed; only which mechanism the fixture reaches.

This is the same species as the `"Item[]"` defect in `2026-09-05-the-indexer-nobody-told.md`: a test that keeps passing after its input stops reaching the code it names. There the input was constructed wrongly from the start; here it was constructed correctly and the library moved underneath it. **Widening eligibility silently retires graph coverage, and nothing in the suite reports that it has happened.** Any future eligibility change should ask which tests it just made vacuous.

## The judge

`DifferentialFuzz` had no property-read-through-a-property in its grammar. It generated `s.Tag.Trim()` — a method call on a property value — and `s.Next.Rank`, a chain through a notifying intermediate, but nothing reaching a property *of* a property's value. `s.Tag.Length` is now an integer leaf, taking `rng.Next(11)` to `rng.Next(12)`, so all four hundred and fifty programs are freshly shaped. `Tag` goes null during mutation, so the leaf exercises the fault path in both mechanisms as well as the value path. All four hundred and fifty passed, as did the new plan-versus-graph case in `SubscriptionAgreement`.

This is the fourth time in this series that a shape was absent from the generator's grammar and the absence was found by hand. The rule stated in August still holds: a generator covers what its grammar knows, and its grammar is a list of things somebody already thought of.

## What is left of this refusal

The other population is untouched and is the harder one. `person.Team.Rank`, where `Team` notifies, still costs the graph in full, and admitting it would mean re-subscribing as the intermediate is replaced — deciding subscriptions after evaluation, which is what the graph exists to do. Nothing here weakens that argument; it only removes the cases the argument never applied to.

Neither population's share of what people actually write is observed by anything in this repository, and the census limit still stands.
