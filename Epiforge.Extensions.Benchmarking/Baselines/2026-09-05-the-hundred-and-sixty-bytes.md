# 5 September 2026 — the hundred and sixty bytes

A projected dictionary query costs **160 bytes more per value change** than a filtered one, with nothing subscribed to either. It has been open since 4 September, and `2026-09-04-the-invocation-wrapper.md` closed by excluding the one candidate anyone had named. This pass prices it.

## What the figure actually is, which is not what it has been called

The orientation has carried it as *"a select observation costs 160 bytes more per evaluation than a where observation."* That is a paraphrase, and it asserts more than the measurement does.

The measurement is a difference between two **query** arms:

| arm | per value change |
| --- | ---: |
| `ChangeEveryValueInAWhereQuery` | 96 B |
| `ChangeEveryValueInASelectQuery` | 256 B |

Both figures are current — they have held across every run since 4 September. But each is a query layer *plus* an observation, and the two differ in both. Attributing the whole 160 to the observation is exactly the move this series keeps having to undo: a difference between two arms named for the part of them that seemed interesting.

**Nothing about the observation has been isolated, and until it is, the 160 has no address.**

## The instrument

One arm added to `DictionaryPropagationBenchmarks`, which already carries the four figures the subtraction needs.

`ChangeEveryValueProjectedWithoutAQuery` observes a pair **projection** per element — `pair => new KeyValuePair<int, int>(pair.Key, pair.Value.Rank)` — exactly as `ChangeEveryValueObservedWithoutAQuery` observes a pair **predicate**, same construction, same thousand elements, no query layer at either.

| arm | on record | what it does here |
| --- | ---: | --- |
| `ChangeEveryValueWithNoObservation` | 48 B | control, the value's own two `EventArgs` |
| `ChangeEveryValueObservedWithoutAQuery` | 96 B | a predicate observed, no query |
| `ChangeEveryValueProjectedWithoutAQuery` | — | a projection observed, no query |
| `ChangeEveryValueInAWhereQuery` | 96 B | the filtered query |
| `ChangeEveryValueInASelectQuery` | 256 B | the projected query |

Four of the five replicate figures already on record, which is what licenses the fifth.

The subtraction: **projection minus predicate is the observation's share of the 160.** Whatever is left belongs to the query layer.

## The prediction, before the run

Built as a count of objects at unit costs already on record, since that is the form which has landed and the form which hedging replaces with nothing.

A predicate observation costs 48 B above the floor, established on 2 September as exactly two boxed integers — `pair.Value.Rank` read through reflection, and the result of `& 1` — the boolean itself being a shared box.

A projection observation should cost, above the same floor:

| | |
| --- | ---: |
| `pair.Value.Rank`, boxed as before | 24 B |
| `EvaluationResults`' `object?[2]` for the constructor's arguments | 40 B |
| the `KeyValuePair<int, int>` which `FastInvoke` boxes on the way out | 24 B |
| `pair.Key`, boxed — **only if it is re-read rather than treated as fixed** | 0 or 24 B |
| **total above the 48-byte floor** | **88 or 112 B** |

So **`ChangeEveryValueProjectedWithoutAQuery` lands at 136 B, or 160 B if the constant key is re-read each time.** The band is honest rather than hedged: the two outcomes differ by one identifiable object and the arm will say which.

Either way the observation's share is **40 or 64 bytes of the 160**, and the falsifiable claim is this:

> **Most of the 160 is the query layer, not the projection.** Between about 96 and 120 bytes of it is what `ObservableDictionarySelectQuery` spends per value change beyond what `ObservableDictionaryWhereQuery` spends, and the `Expression.New` everyone has been pointing at is the minority term.

What each outcome means:

- **The projection arm lands at 136 or 160.** As above: the query layer owns the majority and the next pass belongs there, not in `ObservableNewExpression`.
- **It lands near 256.** The projection owns the whole 160, the paraphrase was right by luck, and `EvaluationResults` and the result box are the targets.
- **It lands near 96.** The projection costs nothing over the predicate, everything is the query layer, and the `Expression.New` candidate is dead.

## Why this arm and not a sweep

`EvaluationResults` allocates a fresh `object?[]` on every evaluation of six node types and is the general version of one line in that table. It is worth pricing on its own, and this arm prices it in one concrete place first — 40 of a number that is already measured — rather than starting from a sweep with no target. If the projection arm shows the array is a real share, the general question has a figure behind it instead of an argument.

## The result, and the prediction which was wrong

| arm | per value change |
| --- | ---: |
| `ChangeEveryValueWithNoObservation` | 48 B |
| `ChangeEveryValueObservedWithoutAQuery` | 96 B |
| **`ChangeEveryValueProjectedWithoutAQuery`** | **256 B** |
| `ChangeEveryValueInAWhereQuery` | 96 B |
| `ChangeEveryValueInASelectQuery` | 256 B |

Predicted 136, or 160 at the outside. **Measured 256** — ninety-six bytes past the top of a band that was supposed to be one object wide.

**The falsifiable claim was refuted outright.** It said most of the 160 belongs to the query layer and the projection is the minority term. The truth is the opposite and it is exact:

- projection observation − predicate observation = **160**
- select query − where query = **160**
- **select query = projection observation, to the byte. The dictionary query layer adds nothing per value change, for either shape.**

The whole 160 is the observation. The paraphrase this document opened by calling overstated — *"a select observation costs 160 bytes more per evaluation than a where observation"* — **was right, and the correction was wrong.**

## Why that was avoidable

The orientation already recorded that a filtered dictionary query with nothing subscribed costs **96 bytes, "the observation's own cost, no query layer at all."** Given that, and given the where arm and the predicate arm both reading 96, the select arm's 256 had only two places to be, and one of them had already been measured to be empty. The leading expectation should have been that the query layer adds nothing here either, exactly as it adds nothing there.

Instead the prediction assumed an asymmetry between the two query layers without naming any mechanism for one. **A figure already in the orientation contradicted the prediction before it was made.** That is not the reading-prices-badly failure this series keeps recording; it is a plainer one — not checking a new prediction against the figures already on hand.

The band itself was also false comfort. Quoting 136-or-160 made the prediction look calibrated while the real uncertainty was never between those two; it was in the terms the model did not contain at all.

## What is actually unexplained now

A projection observation costs **208 bytes above the floor**, against 112 that can be named:

| | |
| --- | ---: |
| `pair.Value.Rank`, boxed | 24 B |
| `pair.Key`, boxed | 24 B |
| `EvaluationResults`' `object?[2]` | 40 B |
| the `KeyValuePair<int, int>` boxed by `FastInvoke` | 24 B |
| named | **112 B** |
| measured | **208 B** |
| **unaccounted** | **96 B** |

Ninety-six bytes with no address. By the rule this repository runs on, that is a finding and not a rounding error, and it is the same size as the hole the boxed dictionary path opened and closed earlier today.

## The next arm, and no prediction attached to the residue

`ChangeEveryValueMemberReadWithoutAQuery` observes `pair => pair.Value.Rank` — the member chain alone, no constructor, no key read. It splits the projection into the part it shares with the predicate and the part only it has.

- **member read − floor** is what two member accesses and one boxed `int` cost.
- **projection − member read** is what the `new` node and the key read cost together, which is where the 96 must be if the member chain is ordinary.

The predicate arm implies the member chain should read **72 B** — 48 floor plus one boxed `int` — since 2 September established the predicate's 48 above floor as two boxed integers, one of them the `& 1`. If the member arm reads much above 72, the residue is in the member chain and the constructor is innocent. If it reads 72, the whole 96 sits in the `new` node and `EvaluationResults` is a bigger term than 40.

**No number is predicted for the residue itself.** Two models of this expression have now been written down and both were short; a third guess before the arm reports would be a guess dressed as a prediction, which is the habit the last entry in this document was about.

## The split

`ChangeEveryValueMemberReadWithoutAQuery` reads **72 B** — the 48-byte floor and one boxed `int`, exactly what the predicate arm implied and nothing more.

| arm | per value change | above the floor |
| --- | ---: | ---: |
| no observation | 48 B | — |
| member read, `pair.Value.Rank` | **72 B** | 24 |
| predicate, `(pair.Value.Rank & 1) == 0` | 96 B | 48 |
| projection, `new KeyValuePair<int, int>(pair.Key, pair.Value.Rank)` | 256 B | 208 |

**The member chain is ordinary and the constructor node owns everything unexplained.** Projection minus member read is **184 bytes** for the `new` node and the key read together, of which 88 can be named — the boxed key, the `object?[2]`, and the boxed result. The 96-byte hole did not move or shrink; it is now pinned to one node's `Evaluate`.

## Four candidates from reading, three of them dead

With the site localized, reading is fit to rank what is in it. It produced four candidates in sequence and killed three of them itself, each by the next pass over the same code:

1. **`IsConstructedTypeDisposed(Type, params Type[])` allocating a params array and a fresh `EquatableList<Type>` per call.** Dead: `ObservableNewExpression` reaches an `internal` overload taking the `EquatableList<Type>` directly, which allocates neither.
2. **The `disposeConstructedTypes` hash lookup, keyed by a tuple containing that list.** Dead: the expression is `DisposeConstructedObjects || disposeConstructedTypes.Contains(…)`, and `DisposeConstructedObjects` **defaults to `true`**, so the lookup never runs in this benchmark.
3. **Disposal work in the `Evaluation` setter.** Dead: `GetShouldValueBeDisposed()` does return true here, but the previous value is a boxed `KeyValuePair<int, int>`, so the body is three type tests and no allocation.
4. **`ConstructorInfo.FastInvoke`.** Survives.

**Every one of those was a confident reading, and three were wrong within a page of each other.** That is not the usual failure recorded here — reading pricing badly — it is reading *ranking* badly, four times, on a site small enough to hold in one screen. The residue is not going to be identified this way.

## A finding worth having regardless of the residue

`ReflectionExtensions.FastInvoke`, for both `ConstructorInfo` and `MethodInfo`, is guarded:

- **`IS_NET_7_0_OR_GREATER`: it calls `MethodBase.Invoke`.** Plain reflection.
- **Otherwise: a cached `DynamicMethod` delegate**, emitting `Newobj` and a `Box`.

So on **every target this suite currently benchmarks**, the method named `FastInvoke` takes the reflection path, and the compiled-delegate path exists only for `net6.0`. Whether that is right is a real question — .NET 7 and 8 improved reflection invocation substantially, and the 2 September figure for a method-call node (56 bytes: one argument array and one boxed result, nothing else) suggests the `MethodInfo` side is fine. The `ConstructorInfo` side has never been measured.

This is the shape of the first entry under habits that cost sessions real time — a name that reads as settled with a framework guard immediately above it.

## The instrument, and what each outcome means

`ConstructorInvocationBenchmarks`, three arms, `DefaultJob`, a thousand invocations each, all on the same `KeyValuePair<int, int>` constructor with the same pre-built and pre-boxed argument array, so nothing in the difference is argument boxing.

| arm | what it is |
| --- | --- |
| `ConstructDirectly` | `new KeyValuePair<int, int>(1, 2)` boxed — the floor, and a control which must reproduce **24 B** |
| `InvokeThroughACompiledDelegate` | the `net6.0` path, emitted here exactly as `ReflectionExtensions` emits it |
| `InvokeThroughReflection` | the `net7.0`-and-later path, `Invoke(BindingFlags.DoNotWrapExceptions, null, arguments, null)` |

- **Reflection costs about 96 bytes more than the delegate.** The residue is identified, the fix is to restore the compiled delegate for constructors on current targets, and it pays on every `new` node in every observation.
- **The two invocation arms agree.** `FastInvoke` is innocent, the guard is fine as it stands, and the 96 bytes are somewhere this investigation has not looked at all — at which point the next move is bisection inside `Evaluate` and not another reading.
- **The control is not 24 B.** Nothing else in the run may be quoted.

No number is predicted for the reflection arm. Four readings of this site have now been wrong; a fifth guess would be worth less than the run.

## The fourth candidate is dead too

| arm | per invocation | allocated |
| --- | ---: | ---: |
| `ConstructDirectly` | 1.63 ns | **24 B** |
| `InvokeThroughACompiledDelegate` | 2.57 ns | **24 B** |
| `InvokeThroughAConstructorInvoker` | 5.50 ns | **24 B** |
| `InvokeThroughAConstructorInvokerWithASpan` | 6.27 ns | **24 B** |
| `InvokeThroughReflection` | 13.09 ns | **24 B** |

**Every path allocates exactly the boxed result and nothing else.** The control reproduced 24 B, so the instrument is sound, and `ConstructorInfo.Invoke` is innocent: it is not the 96 bytes, and neither is any other way of calling that constructor.

**All four candidates reading produced for this residue are now dead.** Three were killed by reading itself and the fourth by measurement. The residue stands, unattributed, and nothing about the invocation path will explain it.

## The accounting ambiguity that has to be resolved first

The residue is either 96 or 120 bytes and this document has not been careful about which.

Per value change the `new` node certainly spends **40** on `EvaluationResults`' `object?[2]` and **24** boxing the result — 64. The boxed `pair.Key` is 24 more **only if that node re-evaluates on every change**, which it should not: the key is constant for the life of the observation and nothing it depends on moves. If it re-boxes, the named total is 88 and the residue is 96; if it does not, the named total is 64 and the residue is **120**.

`ChangeEveryValueProjectedOverAConstantKeyWithoutAQuery` observes `pair => new KeyValuePair<int, int>(0, pair.Value.Rank)` and settles it. Reading 256 means the key contributes nothing per change and the residue is 120; reading 232 means it re-boxes and the residue is 96. Either way the number to attack is finally exact.

**No candidate is proposed for the residue in this document.** Four have been and four were wrong; the next one should come out of an arm.

## One check that could have invalidated everything, and did not

`ObserveWithoutOptimization` disables the optimizer, not direct subscription, and the observer in this instrument is constructed with the default `UseDirectSubscription = true`. Had the projection been eligible for the fast path, `ObservableNewExpression` would never have run and every figure here would have been about the wrong mechanism.

It is not eligible. All three observation arms read `pair.Value.Rank` — a property through a property — which the analyzer refuses outright. The graph is what runs.

This was checked late rather than first, which is the wrong order for an assumption the orientation names as having cost a session an hour.

## `FastInvoke` is the slowest way to call a constructor on every current target

A finding that has nothing to do with the 160 and should not be lost inside it.

`ReflectionExtensions.FastInvoke(ConstructorInfo, …)` is guarded: below .NET 7 it uses a cached `DynamicMethod` delegate; at .NET 7 and above it calls `MethodBase.Invoke`. Measured on `net10.0`, per invocation:

- the compiled delegate: **2.57 ns**
- `MethodBase.Invoke`, the path actually taken: **13.09 ns**

**The guard costs 10.5 nanoseconds per constructor invocation, a factor of five, on every framework this library currently ships to.** Allocation is identical, which is why nothing in the allocation-led work of the past week noticed it.

The .NET 8 `ConstructorInvoker` is not the answer either: 5.50 ns with fixed arity, 6.27 ns through a span, both slower than the hand-emitted delegate. So the fix, if there is one, is to **remove** the `IS_NET_7_0_OR_GREATER` branch rather than add an `IS_NET_8_0_OR_GREATER` one — strictly less conditional compilation, not more.

Two things are unknown and neither should be guessed:

- **Why the guard is there.** `DynamicMethod` needs runtime IL emission, which NativeAOT and full trimming forbid. Nothing in `Common\Project.props` or any `csproj` sets an AOT or trimming property, so nothing in the build declares that constraint — but absence of a declaration is not the reason the guard was written, and Daniel is the only source for that.
- **Whether `MethodInfo` behaves the same.** Only constructors were measured. The 2 September figure for a method-call node — 56 bytes, one argument array and one boxed result — says nothing about time, and the `MethodInfo` overload carries the identical guard. It wants the same instrument before anything is changed.

## The ambiguity is resolved, and against me

`ChangeEveryValueProjectedOverAConstantKeyWithoutAQuery` reads **256 B**, identical to the projection over a real key. The constant `pair.Key` contributes **nothing** per value change — its node evaluates once and its box is read thereafter — so the named total is 88, not 112, and the residue is the larger of the two figures:

| | per value change |
| --- | ---: |
| floor | 48 B |
| `pair.Value.Rank`, boxed — measured by the member-read arm | 24 B |
| `EvaluationResults`' `object?[2]` | 40 B |
| the `KeyValuePair<int, int>` boxed on the way out | 24 B |
| **named** | **136 B** |
| **measured** | **256 B** |
| **residue** | **120 B** |

The two projection arms differ in time — 103.951 μs against 96.820, about 7% — on byte-identical allocation, which is what a constant argument node instead of a member one should look like. Nothing hangs on it.

**The number to attack is 120 bytes, and it is now exact.** It has been quoted as 96 twice in this document, on an assumption about the key that was never checked until now.

## The next arm is a control with an account that already closes

`2026-09-02-evaluation-sequences.md` priced a method-call node at **56 bytes per evaluation — a one-element argument array and a boxed result, and nothing else**. That is a structurally identical node: read the operands into an `object?[]`, invoke through reflection, box what comes back. Its account closes exactly. The constructor node's does not, by 120.

`ChangeEveryValueMethodCalledWithoutAQuery` observes `pair => pair.Value.Rank.CompareTo(0)` — one member read, one method call with one argument, a boxed `int` result. On the 2 September model it should read **128 B**: 48 floor, 24 for the boxed `Rank`, 32 for the argument array, 24 for the result.

- **It reads 128.** The method-call account still closes and the residue is specific to `ObservableNewExpression`, which narrows the search to what that node does and a method-call node does not.
- **It reads about 248.** The residue is common to both node types, and 2 September's "56 bytes and nothing else" has stopped being true since — which would be a larger finding than the one this document is chasing, and would mean something changed underneath a figure the orientation still quotes.

Either outcome is worth the arm. There is no third interesting result.

## A fifth candidate, named and not acted on

`ObservableExpression`'s `defaultResult` is `ExpressionObserverOptions.CannotBeDisposed(type) ? type.FastDefault() : null`, and `FastDefault` is recorded in the orientation as allocating a box on every call for a value type — the delegate is cached, the box is not. `KeyValuePair<int, int>` is a struct, therefore sealed and not disposable, therefore `CannotBeDisposed` is true.

**This is the fifth reading-derived candidate for this residue. The previous four were each stated with confidence and each was wrong.** It is written down so it is not lost and so the next session can see it was considered, not because it is believed. It goes to an instrument, not to a change, and the method-call arm above may make it irrelevant before anyone spends a pass on it.

## The control closed, exactly

`ChangeEveryValueMethodCalledWithoutAQuery` reads **128 B**, against 128 predicted: 48 floor, 24 for the boxed `Rank`, 32 for the one-element argument array, 24 for the boxed `int` result.

**A method-call node still costs what `2026-09-02-evaluation-sequences.md` said it costs, four days and several changes later, and its account still closes to the byte.** So the 120 bytes are not something the graph does to every node of this shape. They belong to the projection and not to the general machinery.

| observation | measured | named | residue |
| --- | ---: | ---: | ---: |
| `pair.Value.Rank` | 72 B | 72 | **0** |
| `pair.Value.Rank.CompareTo(0)` | 128 B | 128 | **0** |
| `new KeyValuePair<int, int>(pair.Key, pair.Value.Rank)` | 256 B | 136 | **120** |

Two node types close and one does not. That is the narrowest the search has been.

## Two explanations left, and one arm separates them

The projection differs from the closing method call in two ways at once, and the difference has never been split:

1. **The node type.** `ObservableNewExpression` rather than `ObservableMethodCallExpression`. The obvious asymmetry between them is that `GetShouldValueBeDisposed()` returns **true** for a constructed value — `IsConstructedTypeDisposed` begins `DisposeConstructedObjects || …` and that option defaults to `true` — where a method call's returns false.
2. **The result type.** `KeyValuePair<int, int>` rather than `int`. `KeyValuePair<TKey, TValue>` does not implement `IEquatable<>`, so the default equality comparer for it is the object-based fallback rather than a devirtualized one, and the `Evaluation` setter compares the old result against the new on every assignment.

`ChangeEveryValueMethodCalledForAPairWithoutAQuery` observes `pair => KeyValuePair.Create(pair.Value.Rank, 0)` — **a method call whose result is the same pair type**. It holds the node type fixed at the one that closes and changes only the result type.

Named, on the model that has now closed twice: 48 floor, 24 for the boxed `Rank`, 40 for the two-element argument array, 24 for the boxed pair — **136 B**.

- **It reads 136.** The result type is innocent and the residue belongs to `ObservableNewExpression` itself, where the disposal asymmetry is the first thing to look at.
- **It reads about 256.** The node type is innocent, `new` was never the problem, and the residue attaches to any observation whose result is a struct without `IEquatable<>` — which would be a far broader finding than this document has been chasing, and would reach every projection in every query.

There is no third interesting outcome, and this arm is the first in the investigation whose two possible results point at genuinely different parts of the library.

## The node type is innocent; the result type is not

`ChangeEveryValueMethodCalledForAPairWithoutAQuery` reads **256 B** against 136 predicted.

| observation | result type | measured | named | residue |
| --- | --- | ---: | ---: | ---: |
| `pair.Value.Rank` | `int` | 72 B | 72 | 0 |
| `pair.Value.Rank.CompareTo(0)` | `int` | 128 B | 128 | 0 |
| `KeyValuePair.Create(pair.Value.Rank, 0)` | `KeyValuePair<int, int>` | **256 B** | 136 | **120** |
| `new KeyValuePair<int, int>(pair.Key, pair.Value.Rank)` | `KeyValuePair<int, int>` | **256 B** | 136 | **120** |

**A method call returning the pair costs exactly what a constructor returning it costs.** `ObservableNewExpression` was never the problem. Every candidate this investigation raised against that node — five of them, four from reading and one from the invocation instrument — was aimed at the wrong half of the difference.

The 120 bytes attach to **the result type**, and therefore to any observation whose result is a struct without `IEquatable<>`, whatever node produces it. That reaches every projection in every query, which is a far larger surface than the one `2026-09-04-the-invocation-wrapper.md` opened this question on.

The live hypothesis, and it is a hypothesis: `KeyValuePair<TKey, TValue>` does not implement `IEquatable<>`, so the default equality comparer for it is the object-based fallback rather than a devirtualized one, and the `Evaluation` setter compares the old result against the new on every assignment. `int` and every other type measured here does implement it. **`ChangeEveryValueMethodCalledForATuple` in the new instrument tests exactly that:** `ValueTuple<int, int>` has the same size, the same field types and the same shape as `KeyValuePair<int, int>` and **does** implement `IEquatable<>`. If it reads 136 the cause is the interface; if it reads 256 the cause is something else about the type and the interface is exonerated.

## The instrument moved, and why each arm was kept

Four of the five arms added to `DictionaryPropagationBenchmarks` during this investigation are not about dictionary propagation. They use a dictionary only as a source of property changes and do not need one at all — what they measure is what an observation costs per underlying change, by expression shape. They have moved to `ObservationShapeBenchmarks`, which builds no dictionary, and `DictionaryPropagationBenchmarks` is byte-for-byte back to what it was before they went in.

**Nothing was retired.** Each arm was weighed against whether it could be useful again:

| arm | why it stays |
| --- | --- |
| member read, 72 B | the cheapest observation on record; the control for any future graph-evaluation work |
| method call returning `int`, 128 B | reproduces the 2 September method-call model in a current instrument; the check for any change to `EvaluationResults` or `FastInvoke` |
| method call returning a pair, 256 B | the arm that separated result type from node type; the before-and-after arm for whatever fixes this |
| projection, 256 B | anchors the whole finding — a select query costs exactly its projection observation |
| projection over a constant key, 256 B | the weakest: a duplicate of its sibling on allocation. Kept because it is the only arm pinning that a constant argument node costs nothing per change, and `FixedSubexpressionRewriter` makes that an area a future change could break |

`ChangeEveryValueProjectedWithoutAQuery` stays in **both** classes: in the dictionary suite because it anchors the select-query result there, and in the shape suite because a new instrument must replicate figures already on record.

## A process failure worth more than the finding

Five arms were added to `DictionaryPropagationBenchmarks` one at a time, each chosen by the previous result, each requiring Daniel to run a suite that grew every time and to leave his machine alone while it ran. Every step was locally justified and the sequence was indefensible.

Two things would have prevented it, both available from the start:

- **`Program.cs` is `BenchmarkSwitcher.FromAssembly(…).Run(args)`, so `--filter` has always worked.** `--filter *ObservationShape*` runs one class. Nobody has to run the whole assembly to answer one question, and it was never offered because the file was never read.
- **An investigation gets its own class.** Growing a subject-specific suite because it already holds the controls you want moves the cost of that convenience onto whoever runs it, permanently.

**Design the arm set as a block.** Both the method-call control and the returns-a-pair variant were foreseeable the moment the member-read arm came back clean; sending them one at a time bought nothing and cost two runs.
