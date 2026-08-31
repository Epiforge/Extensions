# 31 August 2026 — deferred branches, and two crashes found on the way

No benchmark in this document. It records why the largest remaining ineligible shape is going to stay ineligible, and two defects turned up while establishing that.

## Why deferred branches cannot be admitted

`&&`, `||`, `?:` and `??` are refused as `DeferredBranch`. `ShortCircuitGraph` and `ShortCircuitDirect` in `EligibilityMatrixBenchmarks` are the most expensive pair in the matrix by a factor of three, so the prize is real.

The obstacle is not evaluation. A compiled delegate short-circuits at runtime exactly as C# does, so it never evaluates an untaken branch and never sees an exception from one. It is subscription.

The fast path plans once, compiles once, and attaches once when the observation is constructed. It has no way to attach to a source only if a branch turns out to be taken, because by the time the delegate runs, attaching is over. So it would attach to everything, including sources in branches the graph never touches.

That is not a difference in value or in notification — an extra source changing causes a re-evaluation which produces the same answer and, since `2026-08-31-result-boxing.md`, allocates nothing and announces nothing. It is a difference in how many attachments exist, how often the thread wakes, and which objects are held alive.

**And it is a difference the library already forbids.** `SubscriptionAgreement.AssertAgreement` compares the analyzer's plan against what the graph attaches with `CollectionAssert.AreEqual` — exact equality, not containment. `TheGraphDoesNotSubscribeToAnUntakenBranchUntilItIsTaken` pins the graph's laziness at two attachments before the branch is taken and three after.

Preserving that invariant while admitting short-circuiting operators would require deciding subscriptions at evaluation time, after the branch is known. That is what the graph's nodes do. Building it into the fast path would be rebuilding the graph inside the mechanism that exists to avoid the graph.

**Recommended against.** Not on a measurement, and not on difficulty — on the grounds that the two mechanisms would stop agreeing about something this codebase has chosen to guarantee.

An earlier note in this session proposed admitting them when the plan contains no *forcing* subscription. That reasoning was wrong twice over: `ForcesNotification` is set only when a subscription source is reference-equal to the entire lambda body, which a short-circuiting node never is, so no such site can exist in the shapes concerned; and the real obstacle is the count of attachments, which no property of the sites would have changed.

## A crash in planning

Establishing the above needed a test of a fixed chain with a null in the middle, which is what `x != null && x.y` exists to guard. That test did not reach the null. It threw during planning:

```
System.NotSupportedException: the analyzer planned a subscription to
value(...<>c__DisplayClass3_0).outer, which the execution path cannot resolve once per observation
```

`outer` is a captured local, so its declaring type is compiler-generated, so `AnalyzeMember` adds a *contents* subscription on the closure field itself — the feature which watches a captured collection. Meanwhile `FixedSubexpressionRewriter` substituted the whole chain `outer.Linked.Linked` as one unit and never descended into it, so `Field(closure, outer)` was never recorded as a fixed subexpression. `Site` looked for that source, did not find it, found it was neither parameter nor constant, and threw.

**No null is required.** Any two-level field chain rooted at a captured variable crashes, whatever the values. It became reachable with the fields-as-fixed-targets change and has never been in a released package.

`Site` now appends a fixed source it cannot find and takes that index. This works because `CompiledLambda` snapshots the list *after* calling `Site` for every subscription, so the append lands in the array the observation is given, and the indices already compiled into the body are lower and undisturbed.

## A crash in resolution

Underneath that was the defect originally being hunted. `Resolve` walked a fixed chain with `field.GetValue(target is { } ? Resolve(target) : null)`, which conflates two different nulls: a **static** field, which correctly takes a null target, and an instance field whose **target resolved to null**, which throws. Resolution happens in `DirectObservation` before the observation exists and outside any try/catch, so the exception escaped `Observe` entirely, where the graph gives back a faulted observation.

The two cases are now separate patterns. A null target yields null, the null flows into the frozen values, the compiled delegate dereferences it, and `Evaluate`'s existing catch turns that into a fault — which is what the graph produces.

## What the fuzzer did not know

`DifferentialFuzz` reads *properties* off the captured `other`, and *fields* off the parameter and off a genuine constant. It never read a field off a captured local, which is the one combination that triggers the planning crash.

It now generates `other.Linked.Rank`, and `Other` begins with a non-null `Linked` so the leaf exercises the path rather than faulting on every draw. The new leaf changes `rng.Next(10)` to `rng.Next(11)`, so every seed generates a different program: four hundred and fifty programs' worth of fresh shapes, which passed.

This is the third time in this series that a shape was missing from a generator's grammar and the miss was found by a hand-written test. The pattern is stable enough to state plainly: a generator covers what its grammar knows, and its grammar is a list of things somebody already thought of.

## No release note

Both defects arrived with unreleased changes and are fixed before any package contains them. The release note for fields as fixed targets describes the feature that now works; a note about a crash nobody could have encountered would be noise.
