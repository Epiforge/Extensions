# Direct subscription

An observable expression is normally observed by building a graph of nodes, one per subexpression, each subscribing to its children and recomputing when they change. The graph exists because the *set of things worth subscribing to* can change as values change: in `a.B.C`, when `a.B` takes a new value, whatever was watching `.C` must let go of the old object and take hold of the new one.

Many expressions never need that. In `person => person.Rank > threshold.Rank` nothing that is subscribed to is ever replaced. Two objects are watched for one property each, for the lifetime of the observation, and every change means the same thing: run the lambda again. For those, a compiled delegate and a fixed set of subscriptions does the whole job, and the graph is overhead.

This document defines when that substitution is permitted. It governs the work, and nothing in the implementation may weaken it.

## The contract

> The fast path must subscribe to exactly the same set of change sources as the graph would, no more and no fewer.

It also inherits the sentence established by `wrapper-deferral`:

> No consumer of an observable expression ever receives an evaluation that was not simultaneously true of every input that expression depends on.

A fast-path observation satisfies the second trivially — it has no interior structure to be transiently inconsistent — but it must still enter a `PropagationScope` at each of its roots, so that an application mixing both kinds of observation gets one settled notification per propagation rather than one per mechanism.

Subscribing to *more* than the graph would is not a safe direction to err in. It costs extra notifications, and the whole point of the boundary work is that extra notifications are the expensive thing. Subscribing to *fewer* is the failure that cannot be tolerated at all: silent staleness, where the value is wrong and nothing says so.

## What the graph actually subscribes to

Verified by search across the node layer, with the tail of the results checked: **eight subscriptions, six handlers, three node types.** Every other method that looks like an entry point is `OnDependencyEvaluationChanged`, which is downstream by construction.

| node | subscribes to | on the value of | gated by |
|--- |--- |--- |--- |
| `ObservableMemberExpression` | `PropertyChanged`, filtered to the member's name | its target expression | `IsIgnoredPropertyChangeNotification` |
| `ObservableMemberExpression` | `DictionaryChanged` | its own value, only when the member is a field of a compiler-generated type | `MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged` |
| `ObservableMemberExpression` | `CollectionChanged` | as above | `MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged` |
| `ObservableIndexExpression` | `DictionaryChanged` | the object it indexes | — |
| `ObservableIndexExpression` | `CollectionChanged` | the object it indexes | — |
| `ObservableIndexExpression` | `PropertyChanged`, filtered to the indexer's name | the object it indexes | — |
| `ObservableConstantExpression` | `DictionaryChanged` | the constant's value | `ConstantExpressionsListenForDictionaryChanged` |
| `ObservableConstantExpression` | `CollectionChanged` | the constant's value | `ConstantExpressionsListenForCollectionChanged` |

Two things follow immediately.

**Every subscription is to the value of some subexpression.** So the question of whether a fixed set of subscriptions can reproduce the graph's set is exactly the question of whether those particular values can change.

**Eligibility is a property of the observer, not of the expression.** Five options and one ignored-property registry decide whether each of those subscriptions happens at all. The analyser takes the observer's configuration as input, and the same lambda may be eligible under one observer and not under another.

## The eligibility rule, as derived

An expression is eligible when every member access and every index operation has a target whose value cannot change while the observation lives.

Values that cannot change:

- a `ConstantExpression`'s value, by definition
- a parameter's value, which is replaced by a constant before observation begins
- a field of a compiler-generated closure type, reached from either of the above

The third deserves its reasoning written down, because it is what admits the archetype. `person => person.Rank > threshold.Rank` does not compile to that shape; it compiles to `person => person.Rank > closure.threshold.Rank`, so the comparison's right operand is a member access two deep. The saving fact is that a compiler-generated closure does not implement `INotifyPropertyChanged`, so **the graph never subscribes to it either.** A reassignment of the captured local goes unnoticed by both mechanisms. The fast path is not permitted to be blind to anything the graph can see; it is permitted to be blind to what the graph is already blind to.

What that excludes: `person.Manager.Rank`, where `person.Manager` is a member on a type that does notify. Its value can be replaced, the object to watch for `Rank` changes with it, and no fixed set of subscriptions can follow. That needs the graph, and the analyser must say so.

**This rule is a derivation, not a measurement.** It is the hypothesis the analyser's tests and the differential fuzzer exist to falsify, and it should be treated as provisional until they have run.

## What the analyser returns

A structured verdict, not a log message and not a boolean. Eligible, or ineligible with the offending subexpression and the reason it offends. Two consumers need it in that form: the execution path, which needs only the verdict and the subscription set; and diagnostics, which need to tell a caller *which part of their lambda* cost them the fast path, because a cost model nobody can see is the library's standing weakness.

Logging the declined path through `observer.Logger` under its own `EventId` follows from the structured value. It does not replace it.

## Turning it off

`ExpressionObserverOptions` gains `UseDirectSubscription`, mirrored on `IExpressionObserver` as the other options are, so that a caller can decline the mechanism for a whole observer.

It is public rather than internal for a reason that is not about consumers. The differential fuzzer's entire purpose is to run one expression down both paths and compare, so a switch has to exist; and tests in this repository do not get visibility the library does not otherwise expose. Making the switch public means the fuzzer exercises the same mechanism a caller does, rather than something adjacent to it that happens to be reachable from a test assembly.

The consumer case is independently sufficient. This mechanism's failure mode is silent staleness, which is the hardest kind of defect to attribute from outside a library. A caller who suspects it should be able to falsify that suspicion with one flag in one minute. Refusing them that would be asserting a confidence this document has already declined to assert.

**It defaults to on.** An earlier draft of this section proposed defaulting it off for a first release while evidence accumulated, which was incoherent. That is a rollout model for software with telemetry — ship dark, watch, flip — and this is a library that reports nothing back. Nobody would enable an off-by-default option, so no evidence would accumulate; the only effect would be to withhold the benefit. The evidence has to exist before shipping and we have to produce all of it ourselves, which the next section specifies.

The mechanism is not labelled experimental either. A label gathers no data without telemetry; it transfers blame rather than reducing risk. The one thing it would legitimately buy is licence to remove the feature without a major version, and the option already buys something better — a consumer in trouble fixes it themselves in one line without waiting for a release.

Adding the member to `IExpressionObserver` is a breaking change for anyone implementing that interface. Expressions is already at an unreleased 4.0.0, so it is free if this lands before that ships and expensive afterward.

## The evidence required before shipping

The number of fuzzer runs is not the bar. What the generator is *capable of emitting* is.

**Subscription-set equality is the primary instrument**, because it tests the contract's own sentence rather than a downstream consequence of it. A recording notifier logs every `+=` and `-=` performed against it; the same expression is observed once each way; the sets of subscribed pairs of object and event must be equal. This is deterministic. Comparing notification sequences only exposes a wrong subscription set when some generated mutation happens to touch the difference, whereas comparing the sets exposes it always.

The rest:

- analyser verdicts asserted over all thirteen expression kinds `GetObservableExpression` dispatches, under varied observer options, including the ignored-property registry
- a generator whose grammar can emit every node kind, and which deliberately straddles the eligibility boundary rather than sampling its comfortable interior — the rule is wrong, if it is wrong, at the edge
- differential runs comparing complete notification sequences, with zero divergences, left running in continuous integration so the evidence keeps accruing after anyone stops watching
- every hazard in the next section either resolved or converted into an ineligibility rule

A divergence is not a defect to patch in the analyser. It is evidence that the eligibility rule is wrong, and it sends the work back to the derivation.

## Known hazards, recorded before they are forgotten

**Disposal.** The graph disposes values it creates, decided per node by `GetShouldValueBeDisposed` and configured through `DisposeConstructedObjects`, `DisposeStaticMethodReturnValues`, and the method, property and constructor registries. A fast path with no nodes has nowhere to hang that behaviour. Either it reproduces the disposal decisions or expressions to which they apply are ineligible. Unresolved.

**Faults.** The graph localises a fault to the node that threw and keeps the rest of the evaluation intact. A compiled delegate throws once, for the whole expression. Whether the resulting `Evaluation.Fault` is equivalent enough is unestablished.

**Purity.** Re-invoking the whole lambda calls every method in it on every change, where the graph may call only the ones downstream of what moved. For a pure expression this is a performance difference; for an impure one it is a behavioural difference. The library already cannot promise evaluation counts, but the fast path widens the gap.

**Expression caching.** The graph shares nodes between observations of equal subexpressions, which is where its advantage over runtime-capture systems comes from. A fast-path observation shares nothing. At high fan-out over a shared subexpression, the fast path may lose. That is a measurement to make, not a reason to stop.

## Order of work

1. This document.
2. The analyser, returning the structured verdict, with no execution path behind it.
3. Tests over the full node taxonomy — the thirteen expression kinds `GetObservableExpression` dispatches — asserting eligibility and, for the ineligible, the offending subexpression.
4. The execution path.
5. A differential fuzzer running generated expressions down both paths and comparing complete notification sequences, which the `wrapper-deferral` work made possible by settling what crosses the boundary.

Nothing in step 4 begins before step 3 is green.
