# Benchmark comparison, 2 September 2026 — the thirty-two bytes, found

Two independent benchmarks had reported the same unexplained figure for two days: an element crossing a filtered query's predicate, with nothing subscribed to the query, cost **32 bytes** that nothing in the propagation path could account for. It was the last unnamed number in that path.

It is a compiler-generated closure, allocated on entry to a method whose body never runs.

## What it is

`FaultList.ExchangeElementFault` is called once per element whose evaluation changed. It returns early when no fault changed, which is every call in every benchmark here, so its body never executes. Inside that body is this:

```csharp
evaluationFaults.RemoveAll(elementFault => elementFault.Element is TElement faultElement && elementComparer.Equals(faultElement, element));
```

The lambda captures `element` and `elementComparer`, both of them **parameters**. A captured parameter belongs to the method's outermost scope, so the compiler allocates the display class and copies the captured values into it **at method entry**, before the first statement. The early return happens after that allocation, not instead of it.

A display class holding two reference fields is sixteen bytes of object header and two eight-byte fields. **Thirty-two, exactly**, and one per call.

That accounts for the figure and for its shape: `PropagationFloorBenchmarks.ChangeAffectingNoElement` changes no element's evaluation, so the handler never runs and the arm costs 48 bytes; `ChangeAffectingOneElement` changes one, so it cost 80.

`ExchangeKeyFault` had the identical shape and the identical cost on every dictionary query.

## Why it was eliminated, wrongly, twice

The orientation listed this method as ruled out, with the reason *"returns before allocating when no fault changes."* That reading is correct about the body and silent about the prologue. It was written by reading for `new`, and there is no `new` on the path — the allocation is in the IL the compiler emits before the method's first line.

The rule this yields is sharper than the one already recorded about greps: **an early return proves the body did not run and says nothing about what the compiler emitted before the first statement.** Reading a method for allocations means reading it for lambdas over parameters as well as for constructors.

## How it was found

Not by reading. Reading eliminated the entire crossing path — the `ref struct` deferral, the three deferral slots, the `nodes.Count == 1` branch, the null enumeration snapshot, `SetWeight`'s pure pointer climb, `IsChangeObserved`'s field read, `SetCount`'s cached event arguments and short-circuited logging, `PropagationScope`'s reused thread-static list, the scope wrapper's pass-through re-raise — and arrived at zero against an instrument saying 32. **An account which does not close is a finding, and the finding was that the account was wrong.**

`CrossingCostBenchmarks` was written to bisect the one thing reading could not see past: whether the cost belonged to the observation having a subscriber at all, or to what the query does when it hears from one. Three of its four arms replicate figures already on record, deliberately — an instrument which cannot reproduce two existing benchmarks has nothing to say about a third arm.

| arm | before | after | |
|--- |---: |---: |--- |
| `FlipEveryRankWithNoObservation` | 46.88 KB | 46.88 KB | control |
| `FlipEveryRankObservedWithNoSubscriber` | 46.88 KB | 46.88 KB | replicates a known figure |
| `FlipEveryRankObservedWithASubscriber` | **46.88 KB** | 46.88 KB | the question, and its answer |
| `FlipEveryMembershipInAWhereQuery` | 78.13 KB | **46.88 KB** | replicates a known figure, then moves |

The subscribed arm came back at the floor. A subscriber on an observation costs nothing, so `PropagationScope` stayed eliminated and the cost had to be inside the query's handler — a method already read twice. Reading it a third time with "what does the compiler allocate before the first statement" as the question found it immediately.

## The fix

The lambda-bearing body moves into `NewOperationFaultForElement`, so the closure is constructed only when a fault actually changes. `ExchangeKeyFault` gets `NewOperationFaultForKey`. Pure extraction: the two conditions which guarded those statements become the two boolean parameters, and nothing else changes.

Both extracted methods carry a brief summary saying why they exist, because the obvious tidying is to inline them again.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *CrossingCostBenchmarks* *PropagationFloorBenchmarks* *QueryNotificationBenchmarks*
```

## The prediction, which held eight times

The mechanism could not be verified directly — there is no .NET toolchain available to the session, so the claim about where the compiler places the allocation was an inference. What an inference of that kind can do is predict a number in every benchmark that touches the path, and it predicted eight.

| benchmark | arm | before | predicted | measured |
|--- |--- |---: |---: |---: |
| `CrossingCost` | `FlipEveryMembershipInAWhereQuery` | 78.13 KB | 46.88 | **46.88** |
| `CrossingCost` | `FlipEveryRankObservedWithASubscriber` | 46.88 KB | 46.88 | 46.88 |
| `CrossingCost` | `FlipEveryRankObservedWithNoSubscriber` | 46.88 KB | 46.88 | 46.88 |
| `CrossingCost` | `FlipEveryRankWithNoObservation` | 46.88 KB | 46.88 | 46.88 |
| `PropagationFloor` | `ChangeAffectingOneElement` | 80 B | 48 | **48** |
| `PropagationFloor` | `ChangeAffectingNoElement` | 48 B | 48 | 48 |
| `QueryNotification` | `FlipEveryMembershipWithNothingObserving` | 78.13 KB | 46.88 | **46.88** |
| `QueryNotification` | `FlipEveryMembershipWithASubscriber` | 148.44 KB | 117.19 | **117.19** |

Eight for eight, to the byte. Count times known unit cost, once more.

Two of those are worth stating on their own:

**`PropagationFloorBenchmarks`' two arms are now identical at 48 bytes.** One changes no element's membership and one changes an element's membership, and they allocate the same — which is to say **an element crossing a filtered query's predicate, with nothing subscribed, now costs nothing at all** beyond the element announcing its own property change.

**Two independent benchmarks now agree to two hundred nanoseconds.** `FlipEveryMembershipWithNothingObserving` at 76.883 μs and `FlipEveryMembershipInAWhereQuery` at 77.092 μs measure the same thing by different scaffolding and were written a day apart.

No time claim is made. The arms moved between −2.5% and +4%, within the between-run band this suite has established, and the standing rule is that a time saving cannot be attributed to an individual allocation site by differencing arms.

## What a crossing costs now, and what is left

| | bytes per element |
|--- |---: |
| the element's own `PropertyChanging` and `PropertyChanged` arguments | 48 |
| the query, with nothing subscribed | **0** |
| the query, with a subscriber | 72 |

The 72 is a `NotifyCollectionChangedEventArgs` for a single item with an index, which is what a subscriber asked for. There is nothing left to remove on this path that a listener does not need.

## The full arc, closed

`2026-09-01-notification-guard.md` decomposed the original 216 bytes per flip into 88 for the deferral list, 72 for the collection event arguments, and 56 for "expression re-evaluation and the rest". That last 56 is now itself decomposed and gone: **24 was the boolean box**, removed by `2026-09-01-boxed-booleans.md`, and **32 was this closure**.

| part | bytes | fate |
|--- |---: |--- |
| the deferral list | 88 | three fields precede the list |
| the collection event arguments | 72 | built only when something is listening |
| the boolean box | 24 | two shared boxes |
| the fault-exchange closure | 32 | allocated only when a fault changes |
| **total** | **216** | **0 with nothing subscribed, 72 with a subscriber** |

Four measurements taken over three days sum to the figure the instrument gave at the start, and every one of them is now zero or justified. That is what predicting a number before each run buys.
