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

### A proposal for readmitting them, not yet attempted

Recorded as a proposal with its hazards attached, because it has been reasoned and not tried. Nothing below has touched code.

The refusal rests on a misreading worth naming: the problem was never that the fast path *watches* too much. It is that watching too much causes *evaluation* too much, and evaluation has consequences — getters that raise notifications of their own, and a faulting taken branch that mints a fresh exception the wrapper announces because it compares faults by reference.

So do not subscribe lazily as the graph does. **Subscribe eagerly and ignore what cannot matter yet.**

Tag every planned subscription with the branch it lives under. Keep a mask of which branches the last evaluation actually took. A notification arriving from a source whose branch is dead returns without evaluating: no getter runs, no exception is minted, nothing is announced. The extra handler sits attached and does nothing, which is the one divergence from the graph with no observable consequence.

It is safe for a reason that can be checked rather than hoped. A dead branch becomes live only through a change in a test's value, and test sources are subscribed unconditionally and never filtered — so every transition that could matter arrives through a subscription that is always live. Evaluation reads current values, so a skipped notification cannot leave anything stale: a source in an untaken branch could not have affected the current value, and cannot affect it until the test moves, which wakes us.

The principle underneath, which the eligibility rule missed: **a fixed set of subscribed objects is what the fast path requires; a fixed set of *relevant* subscriptions is not.** Branches change relevance. Chained members change identity. The rule was written to exclude the second and caught the first by accident.

`&&`, `||` and `??` are all the same shape as `?:` — a selector and alternatives — so one mechanism readmits all four refused kinds.

**The hazard that would kill it.** Obtaining the mask requires rewriting the tree once per lambda so the compiled delegate records which branch it took. A careless rewrite is exactly how the dead branch's getter gets invoked: hoist the recording outside the conditional, or lift the branches into locals before the test, and both sides evaluate. That would be a side effect the graph never produces, on an expression the caller wrote specifically to guard against it.

So the rewrite has one non-negotiable property — **it must preserve short-circuiting exactly** — and coalesce is the hard case, since `a ?? b` needs a temporary so `a` is evaluated once without pulling `b` forward.

That property is falsifiable in one run. A test type whose dead-side getter throws, asserted never to be called, settles it. **That test is written before the rewrite, not after.**

Two other things could still sink it. The instrumented delegate may be measurably slower than the clean one, which would tax every eligible expression to serve four node kinds — a benchmark question, and the answer might be to instrument only lambdas that contain a short-circuiting node. And the mask is one evaluation old by construction; the argument above says that is safe, but it is an argument, and the differential fuzzer is what would test it.

For the avoidance of doubt: **this changes nothing today.** The four kinds remain refused, on the reasoning above them, until this proposal has been tried and measured.

Two things follow immediately.

**Every subscription is to the value of some subexpression.** So the question of whether a fixed set of subscriptions can reproduce the graph's set is exactly the question of whether those particular values can change.

**Eligibility is a property of the configuration, not only of the expression.** Five options and one ignored-property registry decide whether each of those subscriptions happens at all. The analyser takes `ExpressionObserverOptions` as input — the configuration itself, not an observer holding it — and the same lambda may be eligible under one configuration and not under another.

## The eligibility rule, as derived

An expression is eligible when every member access and every index operation has a target whose value cannot change while the observation lives.

Values that cannot change:

- a `ConstantExpression`'s value, by definition
- a parameter's value, which is replaced by a constant before observation begins
- a field of a compiler-generated closure type, reached from either of the above

The third deserves its reasoning written down, because it is what admits the archetype. `person => person.Rank > threshold.Rank` does not compile to that shape; it compiles to `person => person.Rank > closure.threshold.Rank`, so the comparison's right operand is a member access two deep. A compiler-generated closure does not implement `INotifyPropertyChanged`, so **the graph never subscribes to it either.**

An earlier draft continued: "A reassignment of the captured local goes unnoticed by both mechanisms. The fast path is not permitted to be blind to anything the graph can see; it is permitted to be blind to what the graph is already blind to."

**That is false, and it is the load-bearing sentence of this section.** They are not blind in the same way. Neither subscribes, but the graph *caches* — `ObservableMemberExpression` for `closure.threshold` evaluates once, at initialization, and has no dependency that can ever wake it, so it holds that value for the observation's life. The fast path caches nothing: its compiled delegate dereferences the closure field on every evaluation. So a reassignment of the captured local is unnoticed by the graph **permanently**, and unnoticed by the fast path **only until something else wakes it**, at which point it silently adopts the new object while remaining subscribed to the old one.

The fast path's behaviour was worse than "reads afresh" makes it sound. It stayed subscribed to the object the local held at construction and reported a value computed from the object the local holds now — **woken by one object and answering about another**, with a value depending on the timing of changes to an input it no longer read.

**This is fixed.** Every closure field chain in a lambda is now rewritten, once per lambda, into a read from an array of values resolved when an observation is constructed. `s => s.Rank + closure.other.Rank` compiles to `(s, values) => s.Rank + ((Recorded)values[0]).Rank`, and `values[0]` holds whatever the local held at that observation's construction. Reassigning the local afterwards changes nothing, which is what the graph does and what the semantics require. `ACapturedLocalKeepsTheValueItHeldWhenTheObservationBegan` asserts both mechanisms agree for a single observation.

The values are resolved **per observation, not per lambda**. That was asserted as matching the graph, and it does not — the claim was made without checking, in the same paragraph that had already been wrong once about closures.

**What the graph actually does is share one frozen value between overlapping observations.** Its node cache is keyed by structural expression equality, so a second `Observe` of a structurally-equal lambda finds the *existing* `ObservableMemberExpression` for `closure.other` — evaluated once, when the first observation was created — increments its observation count, and returns it. Both observations then report the object the local held at the *first* one's construction. Once every observation releases the node it is rebuilt, and a later observation reads the field afresh.

`TheGraphSharesOneFrozenClosureValueBetweenOverlappingObservations` and `DirectSubscriptionFreezesAClosureValuePerObservation` pin both.

**This residual divergence is not being chased, and the graph is not defective in it.** An earlier draft of this paragraph called it an artifact leaking into semantics, which is calling it a defect while claiming not to. It is not. Node sharing is the graph's performance model, and sharing state between structurally identical subexpressions is what sharing means; two `Observe` calls only look independent if you forget that.

The right frame subsumes both discoveries in this section. **Reassigning a captured local is a mutation the library cannot observe** — display classes do not notify — so everything downstream of one is *unspecified*, not wrong. Freeze-at-first-observation and freeze-per-observation are two answers to a question with no defined answer. Matching them would mean rebuilding the node cache for fixed subexpressions, at the structural-equality cost deliberately removed elsewhere, to agree about undefined input. There is no upside.

**The change was still worth making, for a reason that stands regardless.** Before the rewrite the fast path was woken by one object and evaluated from another: not a stale reading, not a fresh one, but the value of no expression over any consistent set of inputs, with a composition depending on notification timing. Now each mechanism answers coherently about one closure instance for its whole life. That is a move from *no defensible reading* to *a defensible reading*, which is worth having whether or not it closes the gap.

**Reassigning a captured local while an observation is live is unsupported.** That is the statement to document for consumers, rather than a claim of equivalence which has now had to be retracted twice.

**Unmeasured:** the rewrite substitutes a boxed array read with a bounds check for a direct field read on every evaluation, and allocates one array per observation. The propagation and construction cost of that has not been measured, and the next benchmark run should be read with it in mind.

**The graph is not defective here, and that matters for how this gets fixed.** A captured local's reassignment is a plain field write on a compiler-generated class; nothing notifies, and observing what notifies is the library's whole premise. The closure is part of what was observed, fixed at the moment of observation, in exactly the way the argument is — nobody expects `Observe(lambda, subject)` to start reporting on a different subject. The graph delivers a coherent observation of the closure it was given. The fast path delivers an incoherent mixture.

So the fix is not "make the fast path match the reference implementation." It is that **freezing the captured values is the correct semantics**, and the fast path currently fails to implement them.

**This is a divergence in the direction the contract forbids and it is not yet fixed.** It is not reachable by the differential fuzzer, which never reassigns its captured locals; it was found by reasoning outward from a divergence the fuzzer *did* find, and then confirmed by reading `ObservableMemberExpression`.

The general shape, which subsumes both discoveries: **the graph evaluates incrementally from cached node values, and the fast path evaluates from scratch. Wherever an input is not subscribed — for any reason — the graph freezes it and the fast path does not.** Every instance of this is a divergence, and they must be enumerated rather than met one at a time.

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

**Repeated subexpressions are planned twice, and the registry collapses them.** This position has moved twice and the history matters, because each move was forced by evidence rather than taste.

The first draft claimed the graph builds two member nodes for `person.NameGets + person.NameGets` and attaches two handlers. It does not: every node cache is keyed by `ExpressionEqualityComparer.Default`, so the second occurrence is a cache hit and `OnInitialization` — where every subscription is made — never runs again. One node, one subscription. The planner was given a `HashSet<Expression>` under that same comparer so the plan would match.

Measurement then showed what that cost. `Analyze` and `Plan` walk the same tree under the same rules; `Analyze` costs **12.6 ns and allocates nothing**, `Plan` costs **1,097 ns and allocates 860 bytes**. The entire difference is the dedup set, because `ExpressionEqualityComparer` builds an allocated diagram for every node it hashes, and every observation is handed a freshly rebuilt tree so nothing caches across them. That one set was 40% of the fast path's whole per-observation construction cost, and it was the largest single reason the mechanism was *slower* than the graph it replaces.

The resolution is that the dedup had become redundant without anyone noticing. **The registry deduplicates by resolved object identity**, which is both cheaper and more accurate than expression equality: two plan entries naming the same source, kind and property name find the same registry entry and produce one real `+=`. So the planner now dedupes by reference identity only — free, and it still collapses the case where one expression object is reached twice — and the registry does the rest.

**This changes what a plan means, and the change is deliberate.** `DirectSubscriptionPlan.Subscriptions` lists *sites to attach*, not subscriptions that will exist. A site may appear twice; the registry will attach once. The contract is unaffected, because the contract has always spoken of the *set* of change sources, and that set is what the registry realises. The subscription-agreement instrument compares sets accordingly, which is what its governing sentence said all along even while the test compared multisets.

What the instrument gives up by comparing sets is the ability to detect one handler where the graph uses two. That is not a failure mode: every observation wanting a source is still notified through the registry's intrusive list, so no source goes unwatched. The failure that matters — a source the fast path never attaches to at all — is still caught, and caught by construction.

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

### Eligibility is decided once, per lambda

Originally once per *observation*: the observer normalized the lambda, planned the normalized tree, and took the fast node if the plan was eligible. That was correct and it was also the whole per-observation cost, since both the normalization and the plan are functions of the lambda's *shape*, which every observation of one lambda shares.

Eligibility is now decided once per lambda object and cached beside the compiled delegate. Nothing about the answer depends on the argument: every eligibility test in the analyzer reads the tree's shape, the declared types, and the `PropertyInfo`s — never a value. So the verdict for `person => person.Rank` is the same verdict for every person, and computing it per observation was recomputing a constant.

Making this work required naming an equivalence the analyzer had never had to state, because until now it only ever saw trees whose parameters were already gone:

**A parameter is analyzed as the argument that will replace it.** `ReplaceParameters` turns the parameter into a `ConstantExpression`, and `AnalyzeConstant` plans a contents subscription against it under the *constant* options. So `AnalyzeParameter` must plan the same thing under the same options, or a fast path over a collection argument would silently stop watching that collection's contents.

The two plans are equivalent in what they **attach**, not in what they **list**. A normalized plan can read the constant's value and decline to list a subscription the value cannot satisfy; a lambda plan cannot see the value and lists it, leaving `ResolveKind` to discard it at attach time. `p => p` over a non-collection is the clean example: one listed subscription against zero, and no attachment either way. This is pinned by `APlanFromALambdaBodyListsAContentsSubscriptionTheNormalizedOneCanRuleOut`, because the asymmetry is the sort of thing a later reader would take for a bug.

### Sites: what the plan becomes when it outlives the observation

A plan names sources as `Expression`s, which is right for a plan the consumer inspects and wrong for one cached across observations — the sources it names must resolve to *this* observation's objects. Each subscription is therefore compiled once into a **site**, which is the subscription plus an instruction for finding its source:

- **the argument** — the source was the lambda's parameter
- **a frozen value** — the source was a closure field chain, so the site holds its index into the same values array `FixedSubexpressionRewriter` built, and reads the value that array froze when the observation began
- **a constant** — the source was a `ConstantExpression` or a `Quote`, whose value is the same object for every observation of this lambda, so it is resolved once and held

Anything else throws. That is not defensiveness for its own sake: a closure chain that reached the constant branch would be dereferenced at *cache* time and frozen across every observation of the lambda, which is the frozen-input divergence recorded below wearing a new hat and doing more damage. A loud failure is the only acceptable outcome, so the fallback is `NotSupportedException` rather than a call to the general-purpose resolver.

### One consequence worth stating: the plan and the delegate now agree

The fast path has always compiled the **raw** lambda body while planning the **optimized** normalized tree. With no `Optimizer` configured — the library's default, though *not* the test suite's, since `ExpressionObserverHelpers.Create` sets one on every observer it builds — those are the same expression. With one configured they are not, and planning the raw body removes the disagreement by construction.

**How much that was worth is less than it first appears, and the earlier draft of this paragraph overstated it.** The claim was that a plan derived from a tree the delegate does not evaluate could omit a subscription the delegate depends on. It could omit the subscription — but if the optimizer eliminated that subexpression, the optimizer asserted the value does not depend on it, so no notification was due. For any semantics-preserving optimizer the old arrangement was safe, and the new one merely over-subscribes in the same case, which is also safe. The honest statement is that this makes the fast path's authority unambiguous, not that it fixed a defect.

An observation is still one mechanism or the other for its whole lifetime, with no per-change re-decision and no fallback mid-life.

### Normalization becomes something the fast path only pays for if asked

With eligibility known from the lambda alone, `ReplaceParameters` is no longer needed to *decide* anything — and the fast node never needed its output to *evaluate*, since it holds a compiled delegate and an argument. It was needed for exactly two things, both of which are debugging affordances: `ToString()`, and the `{Expression}` in the trace log.

So the normalized tree is now built on demand. `ObservableExpression.Expression` became a property over a nullable field with a virtual `Materialize()`; graph nodes assign the field in their constructor and never reach the fallback, and the fast node overrides `Materialize()` to normalize its lambda against its argument at the moment somebody asks. The wrapper does the same, deferring to its node.

Two things make this safe rather than clever:

- **The field was `internal readonly`, so the swap to a property cannot break a caller.** A readonly field can be neither assigned nor passed by reference from outside its declaring constructor, which leaves reading as the only thing any call site was doing, and reads are source-compatible with a property. This is a proof, not a survey.
- **Nothing compares `Expression` by reference any more.** The one place that did was the fast path's `forcesNotification` test, which is now decided at cache time and baked into the site. The lazy field is therefore free to race benignly: two threads may both materialize, and two structurally equal trees are as good as one.

The cost is that an observer with a logger attached materializes on initialization and again on disposal, because `LogTrace` takes the expression as a structured argument and evaluates it. Null-conditional short-circuiting means an observer without a logger — the default — pays nothing. Tracing is a debugging mode and the value logged has to be right, so this is the correct trade rather than a regression to fix.

**One divergence deliberately chosen.** The fast node materializes with `ReplaceParametersWithoutOptimization`, even under `Observe`, which optimizes. That means with an `Optimizer` configured the two mechanisms print different strings for the same observation. This is the right way round: the fast path compiles and evaluates the *raw* body, so the raw body is what its `ToString()` should show. Printing an optimized tree the delegate does not evaluate would be the more comfortable answer and the less true one.

The same fact deserves stating without the comfort of the `ToString()` framing: **under a configured optimizer the two mechanisms evaluate different trees.** The graph evaluates the optimized one, the fast path the raw one. They agree on values only because a correct optimizer preserves semantics — the library takes `Func<Expression, Expression>` and cannot check that, so the guarantee is the caller's, not ours. The differential fuzzer now enables `ExpressionOptimizer.tryVisit` on half its seeds, which is what turns that reasoning into evidence; before this it had never run with an optimizer at all, despite the rest of the suite running under one throughout.

### Order

1. `UseDirectSubscription` on the options, the interface and the observer. It is a breaking interface change, and one of the reasons the next release must be a major version.
2. The registry and the fast node, wired into the observer.
3. Differential tests: every case in `SubscriptionAgreement` run down both mechanisms and compared, which is the step the instrument was built to make possible.

## What the fuzzer found on its first run

Two of its three seeded methods failed immediately, on different generated expressions, with one root cause — which is the argument for having built it.

**A faulted expression announced more often on the fast path than on the graph.** Values agreed at every step; the divergence was in notification counts, which is exactly the failure a value-only comparison would have missed.

The cause: an expression re-evaluated while faulted throws a **new exception instance every time**, and the wrapper compared faults with `ReferenceEquals`. So every re-evaluation of a faulted expression looked like a change and was announced. The two mechanisms then re-evaluate at different rates — the graph's intermediate nodes act as filters, so a source notification that leaves an operand's value unchanged never reaches the node that throws, while the fast path re-invokes the whole delegate on any subscribed source. `Recorded.Rank` raises `PropertyChanged` even when set to the value it already held, so this is reached constantly.

This was recorded last night as a hazard of readmitting conditionals — "a faulting taken branch produces a new exception object each time" — and the scope was understated. It needs no conditional. It afflicts **any** faulting eligible expression, because the graph filters through node values and the fast path does not.

**The fix is at the wrapper, and it is a behaviour change for both mechanisms.** Faults are now compared by type and message rather than by identity. Two exceptions of the same type and message are the same fault, so an expression that keeps throwing the same thing announces once and then stays quiet until something actually changes.

That is a change from released behaviour: a consumer watching a faulted observable expression previously received a notification every time any input moved, each reporting the same fault. It is hard to construe that as a feature, and the next release is a major, but it is a change and it is recorded here rather than absorbed.

What it deliberately does not do is make fault identity stable — `Evaluation.Fault` keeps the instance from the first throw of a run, so the stack trace is the first occurrence rather than the latest. Making the fast path reuse its own exception instance was considered and rejected: it would have traded this divergence for the mirror-image one, since the graph does mint a new instance whenever it genuinely re-evaluates.

The node's own `Evaluation` setter still compares faults by reference, so a re-throwing node still notifies its dependents and they still recompute. That is wasted work which the wrapper now absorbs, and it is left alone because changing it would alter when downstream nodes evaluate, which is a larger question than this fix.

## The frozen-input divergence, and why the normalization redesign is now required

The fuzzer's second finding, and the first one it produced in the fatal direction: **the values disagreed.** Seed 1047, `s => -s.Rank + other.Score`, with change notifications for `Rank` ignored by the options. The graph said -5, the fast path said -4.

The cause is the general shape stated under the eligibility rule. An ignored property is never subscribed, so the graph's node for `s.Rank` evaluates once and freezes. Nothing can wake it — its only dependency is the constant holding the argument. Meanwhile a change to `other.Score` wakes both mechanisms, and the fast path re-invokes the whole delegate, reading the current `s.Rank` and quietly incorporating a change the graph had frozen out.

**Two instances are now known, and they are the same defect:**

| unsubscribed input | graph | fast path |
|--- |--- |--- |
| a property whose change notifications are ignored | freezes the value read at initialization | reads it afresh on every evaluation |
| a captured local which is reassigned | freezes the object held at initialization | reads the field afresh on every evaluation |

**The first is fixed by refusal.** An expression touching a property with ignored change notifications is now ineligible, reported as `IgnoredChangeNotification`. There is no cheaper fix: the fast path has no per-node cache to freeze the value in, and any full re-evaluation reads the property. The cost is that one ignored property disqualifies the whole expression, which is acceptable because ignoring a property is explicit and rare.

**The second is fixed by freezing**, since refusal was not available — refusing closure-field targets would refuse the archetype this whole mechanism exists to serve. `FixedSubexpressionRewriter` lifts every closure field chain out of the lambda into an array read, once per lambda, and the observer resolves that array per observation.

Only closure field chains are lifted. A `ConstantExpression`'s value cannot change, so freezing it would buy nothing and cost a boxed array slot; and the parameter is the argument, already fixed at construction by being passed in.

**What this leaves for the performance redesign.** Freezing was the correctness half of the change identified earlier, and it is the smaller half. The larger one remains: the analysis and the plan are still computed per observation from a freshly normalized tree, and `ReplaceParameters` still runs every time. Determining eligibility and planning from the lambda, cached alongside the evaluator that now lives beside it, would remove both from the per-observation path. That is now purely a performance change, which is a much better position to attempt it from.

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
