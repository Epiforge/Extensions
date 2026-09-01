# 1 September 2026 — two faults which disagreed, found by admitting method calls

No benchmark in this document. It records two defects which were in released packages, what exposed them, and why the fix is a strengthening rather than a change of mind.

## What exposed them

Admitting a method call to direct subscription meant that, for the first time, both mechanisms could evaluate the same call. Before it, every method call fell back to the graph, so nothing ever compared the two on a call which throws.

Two tests written to ask that question — `ACallOnANullReceiverFaultsAlikeInBothMechanisms` and `ACallWhichThrowsFaultsAlikeInBothMechanisms` — failed on net7.0 and later and **passed on net6.0**. That split is the whole diagnosis.

## The first defect: reflection wrapped what the method threw

`ReflectionExtensions.FastInvoke` invoked through `MethodInfo.Invoke` on net7 and later and through an emitted delegate on net6. Reflection reports an exception thrown by the target as a `TargetInvocationException` carrying the real one; the delegate reports it as thrown.

So `FastInvoke` **disagreed with itself across target frameworks**, in a public API, with no relation to any of this work. `s.Tag.Substring(100)` produced `TargetInvocationException` on net8 and `ArgumentOutOfRangeException` on net6.

It now passes `BindingFlags.DoNotWrapExceptions`, which is what the net6 path already did.

This reaches further than method calls. `ObservableMemberExpression` reads every property through `getMethod.FastInvoke`, and `ObservableIndexExpression` reads every indexer the same way. Property reads have been eligible for direct subscription for far longer than method calls, so this divergence was reachable and released.

## The second defect: a null target became a reflection error

Given `s.Tag.Trim()` with a null `Tag`, the graph handed the null to reflection, which refused with `TargetException: Non-static method requires a target`. The fast path ran a compiled delegate and got `NullReferenceException`.

`NullReferenceException` is what the expression means. The other is an artifact of how the graph happens to read values.

All four read paths now fault with `NullReferenceException` when the object is null and the member is not static: the method call, the property, the field and the indexer. The guard sits **after** the subscription bookkeeping in each node, so the unsubscribe and resubscribe which run when an object changes identity still run when it changes to null.

## The evidence that this was a fix and not a preference

`Observer.AlreadyFaulted` and `Observer.Fault` failed. Both observe `john.Name!.Length == 6` with a null name, and both already carried a preprocessor branch asserting `TargetException` on net7 and later and `NullReferenceException` below it.

The suite had been carrying a branch whose only purpose was to express this inconsistency. Somebody met it, pinned it, and moved on. Those two blocks are now one unconditional assertion of `NullReferenceException`, which is a strengthening: one expectation across five target frameworks where there were two.

**The old behavior could not be stated in a test without a preprocessor branch. The new behavior does not need one.**

## What it cost to find

The hazard behind the second defect was identified before the change was written, from a mirror of the same normalization, and dismissed on a claim which was only half checked. The first defect was reported as absent because a search covered only the files which happened to be staged rather than the repository.

The correction for the second is worth more than the first: **the test suite is a more complete search than any grep over a working copy.** Exactly two tests failed, which settled that nothing else pinned the old behavior — a question a grep had already answered wrongly.
