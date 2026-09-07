# The chain that could notify

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was refused

A property read through a target which can itself notify — `person.Partner!.Rank` — was the last refusal on the fast path. The reason given was sound: what has to be subscribed to changes as the target's value changes, and the fast path decided its whole plan once, when the observation was constructed.

## What the graph does, established before anything was written

Six tests went out and came back green against the unchanged library, and they are the model this had to reproduce. `NotifyingChainAttachment` pins them: the graph attaches to every link it can reach; when the intermediate is replaced it releases the one it left and attaches to the new one; when the intermediate becomes null it releases and faults, and attaches again if the chain reaches something a second time; replacing the middle of a three-link chain moves the tail as well; and two chains through one object still place one handler on it.

The mechanism behind that was already visible in `ObservableMemberExpression.Evaluate`, which compares the target value it read last against the one it reads now, unsubscribes, resubscribes, and only then invokes the getter — attach before read.

## What changed

Such a target becomes a **link**. `FixedSubexpressionRewriter` wraps it in a block which evaluates it once, records the value into an array passed alongside `values`, and yields it. After the delegate returns, the observation compares each link against the object its subscriptions are attached to; where they differ it detaches, attaches to the new object, and evaluates once more, so the result it keeps was read with the subscription in place. A link inside an untaken branch records nothing and attaches nothing, the recording being on the branch's own path.

This is the machinery built for deferred operands used a second time — the same recording array, the same attach-then-re-evaluate discipline, the same subclass — rather than a parallel one. An observation with neither links nor deferred operands still carries none of it.

## The readings

| arm | before | predicted | after |
|---|---:|---:|---:|
| `NotifyingChainDirect` | 3.71 MB | 1.34 | **1.56 MB / 498.1 μs** |
| `NotifyingChainGraph` | 3.71 MB | unchanged | 3.74 MB / 3,420.4 μs |
| `RankComparisonDirect` (control) | 1.14 MB | 1.14 | 1.14 MB / 350.0 μs |
| `ConditionalDirect` (control) | 1.14 MB | 1.14 | 1.14 MB / 346.5 μs |
| `IndexerReadDirect` (control) | 1.29 MB | 1.29 | 1.29 MB / 488.0 μs |
| `TwoObjectShortCircuitDirect` | 1.31 MB | 1.31 | 1.34 MB / 467.4 μs |
| `TwoObjectConditionalDirect` | 1.31 MB | 1.31 | 1.34 MB / 462.6 μs |
| every `Graph` arm | 2.78, 4.79, 3.71, 5.21, 5.19, 4.11 | unchanged | 2.81, 4.83, 3.74, 5.24, 5.22, 4.14 |

Every figure above was read twice, the suite having been run unchanged a second time to settle the graph movement. The two runs agree to the byte on all twelve arms.

## What they say

**A chain through a notifying value costs the fast path 1.56 MB against the graph's 3.74, and 498 μs against 3,420 — 2.4x the memory and 6.9x the time**, measured against its twin in the same run. Before this the two arms were identical to the byte, which is the instrument saying the shape was refused; they have now separated.

**Three controls held exactly** — the two shapes with no deferred operand and no link, and the indexer.

**Two arms rose by exactly 32 bytes per element**, which is the four reference fields the deferring observation now carries for link state. An expression with deferred operands but no links pays for them and uses none of them. That is a regression I introduced and it is removable the same way an earlier 8 bytes was, by keeping link state off the class that only needs group state.

## The prediction, which was wrong, and the term that closes it

Predicted 1.34, measured 1.56 — 220 bytes per element short. The missing term is the one this project already had on record and I failed to apply: **a distinct source object costs the subscription registry a source object, its lock and a dictionary entry.**

It is derivable from this run without any new instrument. `TwoObjectShortCircuitDirect` exceeds the floor by 200 bytes and its second source is one shared object across all thousand elements; `NotifyingChainDirect` exceeds it by 420 and its second source is a *different* object per element. The link arrays cost 64 more than the flags array they replace, so the difference attributable to the source itself is about **156 bytes**, against the 160 recorded on 7 September for the graph's own registry. The two registries cost the same per distinct source, which is what one would expect of two classes doing the same bookkeeping, and it is now measured rather than assumed.

## The optimizer, found on the way and fixed first

The first attempt at this change failed `DifferentialFuzz` on two seeds where the graph returned a value and the fast path faulted. The cause was older than the change and had nothing to do with chains: `ExpressionObserver.ReplaceParameters` applies the configured `Optimizer` and `CompiledLambda` did not, so **the two mechanisms compiled different trees whenever an optimizer was configured.** It had been unreachable only because every shape which could expose it — a faulting operand inside something a constant-folder removes — was ineligible for other reasons.

`OptimizerAgreement` pins it on shapes eligible before any of this: `false & s.Tag!.Length > 0` and `true | s.Tag!.Length > 0` with a null `Tag`, where short-circuiting would prove nothing because both mechanisms skip the operand anyway. Each optimizing case first asserts that the optimizer really does rewrite that expression, so it cannot pass vacuously — a guard which fired on a fourth shape and correctly refused to let it pass, `tryVisit` leaving `(s.Rank + 0) * 1` alone.

The fix is in the caller rather than in `CompiledLambda`, because reading `ObserveWithoutOptimization` showed it shares both the entry point and the cache with `Observe`: optimizing inside the compile would have optimized the very calls that exist to opt out, and whichever entry point compiled a lambda first would have handed its delegate to the other. `Observe` now asks for the optimized form and `ObserveWithoutOptimization` for the original, and the two are cached apart.

## An unexplained movement, reproducible

**Every graph arm rose by 0.03 to 0.04 MB and stayed there across two runs.** Nothing in these commits touches the graph, and with `UseDirectSubscription` false the observer returns before reaching any changed code. Reading has not produced a mechanism, and by this project's rules that makes it a finding rather than a rounding.

What it is not: it is not the instrument, two identical runs having agreed to the byte. What it does not threaten: every comparison quoted above is between arms of the same run, so the chain's 2.4x and 6.9x stand whatever the cause.

The experiment which would settle it is a bisection, not another run: build and bench this suite at the commit before the optimizer alignment. If the graph arms read 2.78 there, one of the two commits reaches the graph in a way reading has not revealed; if they read 2.81, the movement predates both and the earlier figures were measured against something else.

## Tests

`SubscriptionStability` already asserted exactly what the fast path must now do — one handler on the subject, one on the current intermediate, none on the one it left — using a fixture that counts handlers per object. Its eligibility assertion was inverted and every other assertion kept, so it now proves that of the fast path, with a new sibling pinning the same proposition against the graph so the coverage is not silently retired.

Four cross-mechanism tests compare both mechanisms over identical worlds after every mutation on evaluation, attached objects and notification count. They do not compare handler counts, which the mechanisms never promised: a separate test pins that difference exactly, the graph holding two subscriptions where the fast path holds three for `s.Next!.Rank + s.Next!.Score`, because the fast path keys by property name where the graph keys by event.

Two standing tests asserted the refusal this removes and now use an array initializer, a shape the analyzer has no arm for and nothing plans to admit.

## Left open

- The 32 bytes on every observation with a deferred operand, which links made it carry and which it does not use.
- The graph movement above, and the bisection which would name it.
- Two source nodes which are equal but not identical still plan separate subscriptions, and the fast path still keys by property name where the graph keys by event. A chain read twice through one intermediate therefore holds one subscription more than the graph.
- The `ObserveWithoutOptimization` asymmetry is closed for evaluation, but a direct observation still materializes its `Expression` from the unoptimized lambda, so a consumer reading it sees the text they wrote where the graph would show the optimized form.
