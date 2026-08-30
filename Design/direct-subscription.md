# Direct subscription

An observable expression is normally observed by building a graph of nodes, one per subexpression, each subscribing to its children and recomputing when they change. The graph exists because the *set of things worth subscribing to* can change as values change: in `a.B.C`, when `a.B` takes a new value, whatever was watching `.C` must let go of the old object and take hold of the new one.

Many expressions never need that. In `person => person.Rank > threshold.Rank` nothing that is subscribed to is ever replaced. Two objects are watched for one property each, for the lifetime of the observation, and every change means the same thing: run the lambda again. For those, a compiled delegate and a fixed set of subscriptions does the whole job, and the graph is overhead.

This document defines when that substitution is permitted. It governs the work, and nothing in the implementation may weaken it.

## The contract

> The fast path must subscribe to exactly the same set of change sources as the graph would, no more and no fewer.

This sentence was briefly weakened and has been restored. The reason for both is in **Deferred branches** below.

It also inherits the sentence established by `wrapper-deferral`:

> No consumer of an observable expression ever receives an evaluation that was not simultaneously true of every input that expression depends on.

A fast-path observation satisfies the second trivially — it has no interior structure to be transiently inconsistent — but it must still enter a `PropagationScope` at each of its roots, so that an application mixing both kinds of observation gets one settled notification per propagation rather than one per mechanism.

Subscribing to *more* than the graph would is not a safe direction to err in. It costs extra notifications, and the whole point of the boundary work is that extra notifications are the expensive thing. Subscribing to *fewer* is the failure that cannot be tolerated at all: silent staleness, where the value is wrong and nothing says so.

**So the analyser errs toward the graph, always.** Its default answer is ineligible, and eligibility has to be positively established; an expression kind it does not recognise, an option interaction it cannot reason about, or a shape it has not been taught are all ineligible by construction rather than by omission. A wrong "ineligible" costs a caller some speed they did not previously have. A wrong "eligible" costs them a value that is silently wrong. Those are not comparable, and no measurement will ever make them comparable.

Mitch Hedberg had the shape of it: an escalator can never break, it can only become stairs. This mechanism is permitted to become stairs. It is not permitted to become a hole.

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

### Four corrections the table was too coarse to carry

Found by reading the three node types line by line while building the subscription plan. The table above is right about which events exist; it was wrong about how they combine, and each of these is a way the fast path could have diverged silently.

**Dictionary and collection are alternatives, never both.** All three node types write `if (… dictionary …) else if (… collection …)`. A value implementing both gets one subscription, not two.

**In the member and constant cases the option is inside the branch condition, so excluding one changes which the other gets.** `if (dictionaryPermitted && value is INotifyDictionaryChanged) … else if (collectionPermitted && value is INotifyCollectionChanged)`. A value implementing both, with `DictionaryChanged` excluded, falls through and takes `CollectionChanged` — it is not simply left unsubscribed. The index case has no options at all, so there dictionary wins outright. The single rule covering all three is: *dictionary first, then collection, among those the options permit.*

**The index's `PropertyChanged` is additional and ungated.** It is a separate `if`, not part of the `else if` chain, and `IsIgnoredPropertyChangeNotification` is never consulted for it. An eligible index therefore always contributes two subscription sites to the same object.

**A member's property and a member's field subscribe to entirely different things.** A property subscribes `PropertyChanged` on *the target's* value. A field subscribes contents on *its own* value, and only when the target's type is compiler-generated. A field of an ordinary type subscribes to nothing at all, and no member expression ever does both.

There is also an asymmetry in the two `PropertyChanged` handlers which the fast path has to reproduce rather than tidy up: the member's acts when the reported name is its own **or is null or empty**, the index's acts only on an exact name match. That is why the plan names two property-changed kinds instead of one.

### Deferred branches, and why four node kinds are refused

The subscription-set instrument found this on its first run, which is the entire argument for having built it before the execution path.

**A node subscribes when it evaluates, not when it is constructed**, and four node kinds construct children they do not evaluate:

- `ObservableConditionalExpression` creates *both* branches with evaluation deferred, unconditionally
- `ObservableBinaryExpression` creates its right operand deferred for `Coalesce`, and for `AndAlso` and `OrElse` when the node's type is `bool`

Reading a deferred node's `Evaluation` forces it, once, and clears the flag for good. So `subject => subject.Rank > 0 ? other.Rank : subject.Score`, observed while `Rank` is zero, subscribes to `subject` twice and **to `other` not at all**. Set `Rank` to one and the graph subscribes to `other` — and never lets go when the condition flips back, because the deferral is spent. The graph's subscription set is thus a property of the expression *and its history*: a subset at first, growing monotonically, settling at the full static set.

The first response to this was to accept the superset and weaken the contract to "never fewer, and equal at steady state." That was wrong, and the reasoning that overturned it is worth keeping, because the flaw was not in the direction of the error but in what counts as an error at all.

**Extra subscriptions are not extra work. They are extra evaluations of the taken branch, and evaluation is not free of consequence here.** A compiled delegate short-circuits exactly as C# does, so the fast path never evaluates the untaken branch — that much is fine. But when an input only the untaken branch reads changes, the fast path wakes and re-invokes the delegate, which evaluates *the taken branch*, calling its getters. The graph, not subscribed, sleeps through it.

Two consequences follow, and both are visible to consumers:

**Getters have side effects, including notifications of their own.** This repository's `TestPerson.Name` raises `PropertyChanging` and `PropertyChanged` for `NameGets` from inside its getter. An extra evaluation therefore makes the observed object announce a change to anyone watching it — not only to this observation. Nothing about the wrapper's compare-then-notify contains that; it escapes into the application.

**A faulting taken branch produces a new exception object each time.** The wrapper compares faults by reference, so a re-evaluation that throws afresh is a changed evaluation and *is* announced. An observation whose taken branch faults would therefore raise notifications, at moments the graph is silent, driven by changes to an input the expression is not currently reading.

Neither of these is silent staleness, and both are narrow. But the standard is not "is the failure survivable." It is whether the mechanism can be relied upon to do nothing the graph would not, and here it cannot. **So `Conditional`, `Coalesce`, and `AndAlso` and `OrElse` over `bool` are refused**, reported as `DeferredBranch`, and the contract's original sentence stands unamended.

The refusal mirrors the graph's own condition rather than approximating it: `AndAlso` and `OrElse` are refused only when the node's type is `bool`, because that is exactly when `ObservableBinaryExpression` defers. A lifted `bool?` conjunction is not deferred and is not refused.

There is an escape hatch worth telling callers about. The non-short-circuiting `&` and `|` are not deferred by the graph, so `person => person.A > 0 & person.B > 0` remains eligible where the `&&` form does not. That is a real choice a caller can make when they know both operands are cheap and safe to evaluate, and it is the kind of thing a visible cost model exists to let them make.

`TheGraphDoesNotSubscribeToAnUntakenBranchUntilItIsTaken` remains as a test, pinning the graph behaviour at two subscriptions before the branch is taken and three after, so the reason for the refusal cannot quietly stop being true.

Two things follow immediately.

**Every subscription is to the value of some subexpression.** So the question of whether a fixed set of subscriptions can reproduce the graph's set is exactly the question of whether those particular values can change.

**Eligibility is a property of the configuration, not only of the expression.** Five options and one ignored-property registry decide whether each of those subscriptions happens at all. The analyser takes `ExpressionObserverOptions` as input — the configuration itself, not an observer holding it — and the same lambda may be eligible under one configuration and not under another.

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

## The analyser is public

The analyser is a public type a caller can instantiate against an `ExpressionObserverOptions`, and ask about an expression without observing it. An observer holds its own instance and uses it for its own decisions; nothing about that is privileged.

It takes options rather than an observer, and the distinction is not cosmetic. An observer is a live object holding caches, graphs and subscriptions, and requiring one to answer a question about a configuration would mean building a machine to ask about its settings. It would also force a cycle: an observer holding an analyser holding that same observer, established mid-constructor, at the moment the object is least ready to be read.

Three things follow from making it public rather than internal.

**Tests can assert eligibility directly.** Instead of inferring from behaviour that an observation probably took the fast path, a test states that the expression is eligible under these options, and thereby knows what the observer will do with it. Every subsequent assertion in that test is then about the mechanism it intended to exercise, rather than about whichever mechanism happened to be chosen.

**Diagnostics stop depending on logs.** A caller who wants to know why their lambda is not being fast-tracked asks, and is told which subexpression is responsible. The library's standing weakness is that its cost model is invisible at the call site; a log message improves that only for someone already reading logs, and only after the fact.

**It is honest about what the mechanism is.** A caller who can inspect the decision can predict the behaviour of their own code. One who cannot is being asked to trust a heuristic they are not allowed to see.

The analysis takes an `Expression`: the one which will actually be observed, after parameters have been replaced by constants and any optimizer has run. Producing that expression is the observer's business, not the analyser's.

An earlier draft had the analyser accept a lambda and its arguments and reproduce the pipeline itself — optimize, replace parameters, analyse. That is a standing invitation to divergence. The analyser would have to reproduce the observer's normalization faithfully forever, and if the pipeline ever changed shape the analyser would go on confidently answering about a different expression than the one being built. Handing it the object the observer is about to use makes agreement structural rather than reconstructed, and there is then nothing left to disagree about.

The cost is that a caller holding a lambda cannot ask the question directly. That is a convenience for the observer to offer, at the point where it already computes the normalized expression, and it is not built until the execution path needs it.

The result is a structured value: eligible, or ineligible with the offending subexpression and a reason, as the previous section requires.

## The subscription plan

The verdict says whether. The plan says what, and it is what makes the execution path a transcription rather than a second derivation.

A plan is an ordered list of subscriptions, each naming three things: the expression whose value is resolved once to yield the object subscribed to, the event attached to it, and the member name the handler acts upon. That is the table above, instantiated for a particular expression under particular options. The order is the graph's own — for an index, contents before indexer; for an operand, the operand's own subscriptions before those taken on it — so that a plan reads as the script the graph would perform.

Several decisions inside it are worth stating, because each had a plausible alternative.

**It is produced by the same walk as the verdict, not a second one.** The recursion carries a nullable collector; `Analyze` passes null and allocates nothing, `Plan` passes a list. Had planning been its own traversal, the two would have been separate statements of one rule, free to disagree about which subexpressions matter. This way there is nothing to keep in agreement.

**The verdict-only path stays allocation-free.** Ineligibility is the common answer for anything nontrivial, and the observer asks for a verdict far more often than it will ask for a plan.

**A plan names sites, not attachments.** Whether anything actually attaches at a site depends on which notification interfaces the value implements, which the analyzer generally cannot know without invoking something. It does not invoke anything. The one exception costs nothing: a `ConstantExpression` carries its value as a field of the node, so where the source is a constant the analyzer reads it, resolves `DictionaryOrCollectionChanged` down to the exact event or to no subscription at all, and drops property-changed sites on values that do not notify. That prune is what keeps a plan legible — without it every parameter-replacement constant and every integer index argument would contribute a site that resolves to nothing.

The rule dividing those cases is worth stating as a rule, because it will be tempting to cross: **the analyzer inspects what the expression tree already holds and invokes nothing.** Reading a closure field would resolve more sites, and it is exactly the work the fast path exists to defer to execution.

**Repeated subexpressions are planned once, by the graph's own comparer.** An earlier draft of this section said the opposite, and said it confidently: that `person.NameGets + person.NameGets` plans two subscriptions because the graph builds two member nodes. The graph does not. Every node cache in the observer is a dictionary keyed by `ExpressionEqualityComparer.Default`, so the second occurrence is a cache hit, the existing node's observation count is incremented, and `OnInitialization` — which is where every subscription is made — does not run again. One node, one subscription.

Planning two would have been a divergence in the direction of subscribing to *more* than the graph does. Not the fatal direction, but the expensive one, and the whole point of the boundary work is that extra notifications are the expensive thing.

So the planner carries a set of the expressions it has already reached, keyed by that same `ExpressionEqualityComparer.Default`, and a repeat contributes nothing and is not descended into — which is precisely what a cache hit does. Using the observer's own comparer rather than a rule of our own means the plan does not need to know what expression equality means in this library; it needs only to ask the same question the caches ask. There is one comparer the observer uses elsewhere, `ConstantExpressionExpressionEqualityComparer`, reserved for constants whose value is itself an `Expression`; such a value is not a notification source, so the distinction cannot change a subscription set.

**Duplicates that survive are real.** Deduplication is per node, not per subscription, so one event of one object can still be subscribed twice by two *different* nodes. An index over a constant collection does exactly that: the constant is a node which watches its own contents, and the index is a node which watches the contents of the object it indexes. Both really happen, with two handlers. That is why the index test asserts three subscriptions and asserts that two of them are equal.

**The plan is public, and it was going to be internal.** The argument for internal was that publishing a shape before anything consumes it is how you publish the wrong shape. The argument that beat it is that tests in this repository do not get visibility the library does not otherwise expose, so an internal plan is an untestable plan — and the whole reason to build it before the execution path is to have it under test first. The window in which this is a free breaking change closes when 4.0.0 ships.

### A tightening found while doing it

`IndexExpression.Indexer` is null for an array access, and the graph dereferences it unconditionally, so `GetObservableExpression` would throw on one. The analyzer previously declared such an expression eligible, which would have handed the fast path an expression the graph cannot observe — a divergence in the direction the fast path is not permitted to err, even though here the fast path is the one that works. It now refuses a null indexer as an unsupported kind.

## Turning it off

`ExpressionObserverOptions` gains `UseDirectSubscription`, mirrored on `IExpressionObserver` as the other options are, so that a caller can decline the mechanism for a whole observer.

It is public rather than internal for a reason that is not about consumers. The differential fuzzer's entire purpose is to run one expression down both paths and compare, so a switch has to exist; and tests in this repository do not get visibility the library does not otherwise expose. Making the switch public means the fuzzer exercises the same mechanism a caller does, rather than something adjacent to it that happens to be reachable from a test assembly.

The consumer case is independently sufficient. This mechanism's failure mode is silent staleness, which is the hardest kind of defect to attribute from outside a library. A caller who suspects it should be able to falsify that suspicion with one flag in one minute. Refusing them that would be asserting a confidence this document has already declined to assert.

**It defaults to on.** An earlier draft of this section proposed defaulting it off for a first release while evidence accumulated, which was incoherent. That is a rollout model for software with telemetry — ship dark, watch, flip — and this is a library that reports nothing back. Nobody would enable an off-by-default option, so no evidence would accumulate; the only effect would be to withhold the benefit. The evidence has to exist before shipping and we have to produce all of it ourselves, which the next section specifies.

The mechanism is not labelled experimental either. A label gathers no data without telemetry; it transfers blame rather than reducing risk. The one thing it would legitimately buy is licence to remove the feature without a major version, and the option already buys something better — a consumer in trouble fixes it themselves in one line without waiting for a release.

Adding the member to `IExpressionObserver` is a breaking change for anyone implementing that interface.

An earlier draft of this paragraph said Expressions "is already at an unreleased 4.0.0," which was an assumption stated as a fact. The csproj says **3.0.1**, with `PackageValidationBaselineVersion` at 3.0.0, and Daniel sets versions at release time rather than as work lands. So this change does not arrive somewhere already broken; it is one of the things that *forces* the next release to be a major one. The other is the removal of `INotifyDisposalOverridden` during `wrapper-deferral`.

That makes `CompatibilitySuppressions.xml` more than build noise. It is the accumulating list of reasons the next version cannot be 3.0.1, and it should be read that way at release time rather than regenerated reflexively.

## The execution path

Three decisions settle its shape, and the first settles most of it.

### The fast path is a node

Every `Observe` overload funnels through one private method that does `new ScopedObservableExpression<…>(this, expression, GetObservableExpression(expression, false), arguments)`. The wrapper is what a consumer holds, and it already owns everything the contract cares about: `PropagationScope` enlistment, compare-then-notify, the typed `Evaluation`, `Disposing` and `Disposed`, `Arguments`, `Observer`. All of it is written against an `ObservableExpression`.

So the fast path is an `ObservableExpression` subclass. It overrides `OnInitialization` to resolve its plan's subscriptions instead of building children, and `Evaluate` to invoke a compiled delegate instead of combining child evaluations. The observer changes in exactly one place: which node it hands the wrapper. Nothing about notification, scoping or disposal is written a second time.

That matters more here than the alternative's speed. Every defect in this branch has come from one decision being expressed twice — the subscription rules, the duplicate-collapsing rule, the deferral behaviour. A parallel implementation of the notification contract would be the largest such duplication yet, and it would be the one whose divergence a consumer sees.

**The cost, stated rather than hidden.** A node holds its value as `(Exception? Fault, object? Result)`, so a value-typed result is boxed on every evaluation. The ceiling measurement attributes exactly 24 bytes per notification to that box, a third of the propagation allocation advantage. Reusing the wrapper gives it up. It is given up deliberately, because the same measurement shows the speed advantage is path length rather than allocation — the graph walks constant to member to wrapper through two dependent lists, where the fast path invokes one delegate — and path length survives intact. If the remaining bytes prove worth a typed path later, that is a decision to make against a benchmark of the built thing, not against a guess about it.

### Subscriptions to a shared source are shared

Required, not optional: `2026-08-30-direct-subscription-ceiling.md` shows a fast path which subscribes per observation is quadratic in the number of observations and loses to the graph beyond about a thousand.

The observer therefore holds a registry keyed by the resolved source object's *identity*, the event kind, and the property name. Each entry owns exactly one real `+=` against the source and an intrusive list, with tombstones, of the fast nodes that want it — the same structure `ObservableExpression` uses for its dependents, because removal during notification is the hazard in both cases. The last node to leave takes the real subscription with it.

The list structure is duplicated rather than extracted, and the distinction from the duplications this document has refused elsewhere is deliberate: those duplicated a *decision* — which event, which name, which branch — that could drift into disagreement. An intrusive linked list contains no decision. Extracting it would mean editing the notification walk that `wrapper-deferral` benched, without a benchmark in hand to confirm the added indirection is free. Recorded as a candidate for unification once both are measured.

### Eligibility is decided once, at observation

The observer computes the normalized expression already. It asks the analyzer for a plan, and takes the fast node when `UseDirectSubscription` is on and the plan is eligible. There is no per-change re-decision and no fallback mid-life: an observation is one mechanism or the other for its whole lifetime.

This is also where the convenience anticipated under **The analyser is public** gets built, since the observer is the only place that holds both the lambda and its normalization.

### Order

1. `UseDirectSubscription` on the options, the interface and the observer. It is a breaking interface change, and one of the reasons the next release must be a major version.
2. The registry and the fast node, wired into the observer.
3. Differential tests: every case in `SubscriptionAgreement` run down both mechanisms and compared, which is the step the instrument was built to make possible.

## The evidence required before shipping

The number of fuzzer runs is not the bar. What the generator is *capable of emitting* is.

**Subscription-set equality is the primary instrument**, because it tests the contract's own sentence rather than a downstream consequence of it. A recording notifier logs every `+=` and `-=` performed against it; the same expression is observed once each way; the sets of subscribed pairs of object and event must be equal. This is deterministic. Comparing notification sequences only exposes a wrong subscription set when some generated mutation happens to touch the difference, whereas comparing the sets exposes it always.

### The instrument, built before the execution path

The comparison exists now, ahead of anything that executes a plan, and the sequencing is the point: with no execution path, a divergence has exactly one possible cause. The plan is wrong. Nothing else is in the picture to blame.

`SubscriptionLog` records every `+=` and `-=` performed against the recording types with the target's identity and the event's name. One expression is observed by a real `ExpressionObserver`; the plan for the same expression is computed; each planned subscription's source is resolved to its value, `DirectSubscription.ResolveKind` turns the site into the event actually attached there, and the two sorted multisets must be equal. Disposal must then return the log to zero outstanding, and the observer's cache to empty.

`ResolveKind` lives in the library rather than in the test, because it is the site-to-attachment rule the execution path needs anyway, and it is fully determined by the four corrections above. Writing it twice would be writing the divergence in by hand.

Two limits worth stating plainly.

**The test reproduces parameter replacement rather than obtaining it.** `ReplaceParametersWithoutOptimization` is internal, so the test substitutes `Expression.Constant(argument, parameter.Type)` itself, which is what the observer does. This is sound here only because the comparison is over *resolved objects and event names*, never over expression identity — a structurally different but equivalent tree reaches the same objects and compares equal. The observer-side convenience the analyser section anticipates would remove the reproduction entirely, and gets built with the execution path.

**Indexers are outside the instrument, for the reason already recorded.** `collection[0]` reaches the observer as a `get_Item` call which the observer normalizes and the analyser does not, so there is no lambda for which both mechanisms see an index. That is not a coverage gap in the mechanism, since the fast path cannot be reached through an indexer today either. It closes when normalization is shared.

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

**Expression caching. Measured, and it is a condition rather than a hazard.** The graph shares nodes between observations of equal subexpressions. A naive fast path shares nothing, and `2026-08-30-direct-subscription-ceiling.md` shows what that costs: construction over a closure source shared by N observations grows 113× for a tenfold increase in N, against the graph's 14.8×, because every observation performs its own `+=` and `Delegate.Combine` copies the invocation list each time. The crossover is near a thousand observations, and beyond it the mechanism is asymptotically worse than what it replaces.

So the execution path **must** share one real subscription per resolved source, keyed by the source object, the event kind and the property name, with the observations wanting it held in an intrusive list — which `ObservableExpressionSubscription` already is. This is not an optimization to add later. It is designed in from the first line, or the mechanism is a regression on the workload it exists for.

The same measurement settles the rest of the question: on the shapes that survive eligibility, direct subscription propagates a change 4–5× faster with a third less allocation, and constructs an observation 60–75× faster with a thirteenth of the memory. A node holds its value as `(Exception?, object?)` and therefore boxes every value-typed result; a fast path holds it typed and boxes nothing, which is where a third of the propagation advantage comes from.

## Where the analyser is conservative rather than correct

Each of these is a place the mechanism becomes stairs when it might have been an escalator. They are listed so that relaxing one is a deliberate act with evidence behind it, rather than something that drifts in.

- **Conditionals, coalesces, and short-circuiting conjunctions and disjunctions over `bool`** are refused, for the reasons under **Deferred branches**. This is the only entry here refused for a reason found by measurement rather than by reading, and the only one where the relaxation was tried and withdrawn. `&` and `|` remain eligible.
- **Method calls, invocations, `new`, member init and array init** are refused outright. Their disposal and purity semantics are among the unresolved hazards below.
- **Operators implemented by a method** are refused, for the same reason: the graph treats them as method calls and may dispose their results. This is far more costly than it sounds. `String` declares `op_Equality` and `op_Inequality`, so every comparison of two strings arrives as a binary expression with a method attached, and `person => person.Name == "Emily"` — as ordinary a predicate as exists — is refused. The relaxation is available and principled: ask `IsMethodReturnValueDisposed` about the operator's method, exactly as members are already asked `IsPropertyValueDisposed`, and refuse only when the answer is yes. It is not taken here because widening the eligible set is the dangerous direction, the fuzzer does not yet exist to check it, and it surfaced as a failing test rather than as a decision. It should be made deliberately, with evidence, and not as a reflex to a red bar.
- **Members and indexers whose values are registered for disposal** are refused, since disposal is a graph behaviour with nowhere to live on a fast path.
- **Every static property is refused under default options**, which was not intended and was found by a test written to check something else. `DisposeStaticMethodReturnValues` defaults to `true`, a static property's getter is a static method, and `IsPropertyValueDisposed` therefore answers yes for all of them — so `DateTime.Now` is ineligible for the same stated reason as a property a caller deliberately registered. The rule is not wrong: the graph really would dispose that value, and a fast path really has nowhere to do it. But the reach is much wider than "members registered for disposal" suggests, and anything reading a static configuration or clock is excluded. Worth revisiting with the same evidence that would relax method-backed operators, since it is the same unresolved hazard.
- **Indexers are refused in practice, and by accident of shape.** C# compiles `people[0]` in an expression tree to a call of `get_Item`, not to an `IndexExpression`. The observer normalizes such calls back into index and member access before building its graph; the analyser does not, so every indexer arrives as a method call and is refused. The analyser is therefore correct but blind here, and closing it means either sharing the observer's normalization or performing the analysis after it. That is a design question, not an oversight, and it is deferred rather than answered.

## Order of work

1. This document.
2. The analyser, returning the structured verdict, with no execution path behind it.
3. Tests over the full node taxonomy — the thirteen expression kinds `GetObservableExpression` dispatches — asserting eligibility and, for the ineligible, the offending subexpression.
4. The subscription plan, with tests, so that the execution path is a transcription of something already established rather than a fresh derivation.
5. The execution path.
6. A differential fuzzer running generated expressions down both paths and comparing complete notification sequences, which the `wrapper-deferral` work made possible by settling what crosses the boundary.

Nothing in step 5 begins before step 4 is green.
