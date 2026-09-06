# Benchmark comparison, 6 September 2026 — the guard that cost five times

`ReflectionExtensions.FastInvoke` has two implementations behind `#if IS_NET_7_0_OR_GREATER`: below .NET 7 it invokes a cached `DynamicMethod` delegate, and at .NET 7 and above it calls `MethodBase.Invoke`. Daniel confirmed the only reason for the split: *".NET 6's method invoke via Reflection is not at all fast."* Speed was the whole premise.

`2026-09-05-the-hundred-and-sixty-bytes.md` measured the constructor half and found the premise false on `net10.0` — the delegate at 2.57 ns against `MethodBase.Invoke` at 13.09, a factor of 5. It also recorded that **`MethodInfo` carries the identical guard and had never been measured**, and that nothing should change until it was. This measures it.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

`ConstructorInvocationBenchmarks`, now nine arms. Four new ones mirror the constructor set for `string.IndexOf(char)`, emitting the same IL the pre-.NET-7 branch emits — `Castclass` the instance, `Unbox_Any` each argument, `Callvirt`, `Box` the return.

## The measurement

Per call, the arms running a thousand iterations apiece.

| | constructor | method |
|--- |---: |---: |
| directly, no reflection | 1.611 ns | 2.070 ns |
| emitted `DynamicMethod` delegate | **2.565 ns** | **3.031 ns** |
| .NET 8 `Invoker`, fixed arity | 5.480 ns | 5.791 ns |
| .NET 8 `ConstructorInvoker` over a span | 6.231 ns | — |
| `MethodBase.Invoke` | 12.762 ns | 8.233 ns |

**The guard costs 10.2 ns per constructor invocation, a factor of 5.0, and 5.2 ns per method invocation, a factor of 2.7.** The premise it rests on is false in both halves on this framework.

Every arm allocated 23.44 KB, which is 1,000 boxed returns at 24 bytes and nothing else. The account closes exactly, so the reading is time alone and no path allocates more than any other.

The three constructor arms are controls against figures recorded on 5 September — 2.57, 5.50 and 13.09 ns — and reproduced at 2.565, 5.480 and 12.762, within 2.5%.

## What changed

Both guards are gone. `FastInvoke(ConstructorInfo, …)` and `FastInvoke(MethodInfo, …)` now use the emitted delegate on every target, which is what `net6.0` consumers have had all along.

The objection worth raising against that is trimming and ahead-of-time compilation, since `DynamicMethod` is hostile to both. It does not hold here, and the reason is in the project files rather than in an argument: neither `Epiforge.Extensions.Components.csproj` nor `Common\Project.props` sets `IsAotCompatible`, `IsTrimmable` or `PublishTrimmed`, so the package makes no such claim, and the `net6.0` target already ships the emitting path. This extends to the other four targets what one of them already does; it does not introduce IL emission into a library that had avoided it.

**`FastGetValue` and `FastSetValue` keep their guards.** They call `PropertyInfo.GetValue` and `SetValue` above .NET 7, which is a different API from `MethodBase.Invoke` and whose cost is not measured by anything here. Three more arms would settle them. Until then they stay as they are, for the same reason the `MethodInfo` half of `FastInvoke` stayed as it was until today.

## The before, recorded ahead of the run

`ObservationShapeBenchmarks` is the consumer instrument: its arms re-evaluate standing graph observations that call methods and invoke constructors, so they pay the guard once per evaluation. Measured earlier today, on the graph:

| arm | mean | allocated |
|--- |---: |---: |
| `ChangeEveryValueMemberRead` | 42.408 μs | 70.31 KB |
| `ChangeEveryValueMethodCalled` | 50.693 μs | 125 KB |
| `ChangeEveryValueMethodCalledForAPair` | 79.509 μs | 156.25 KB |
| `ChangeEveryValueMethodCalledForATuple` | 75.718 μs | 156.25 KB |
| `ChangeEveryValueProjected` | 82.095 μs | 156.25 KB |
| `ChangeEveryValueProjectedOverAConstantKey` | 77.039 μs | 156.25 KB |
| `ChangeEveryValueProjectedWithDirectSubscriptionAllowed` | 82.592 μs | 156.25 KB |
| `ChangeEveryValueWithNoObservation` | 8.745 μs | 46.88 KB |

## The prediction

**Allocation must not move on any arm.** Every path measured above allocates identically, so a change in the allocation column would mean the change did something other than what it is supposed to do. This is the strong half of the prediction and the one worth watching.

On time: the arms which construct a `KeyValuePair` or a `ValueTuple` per evaluation should each shed 10.2 ns per invocation, and `ChangeEveryValueMethodCalled` 5.2 ns per invocation. **The unreliable term is the count** — how many invocations an arm performs per operation is read off the arm's name and element count rather than measured, and a count read out of code has been wrong by a factor of two twice this month. At one invocation per element over a thousand elements the saving would be 10.2 μs and 5.2 μs respectively, taking `ChangeEveryValueProjected` to about 72 μs and `ChangeEveryValueMethodCalled` to about 45.5.

`ChangeEveryValueMemberRead` and `ChangeEveryValueWithNoObservation` invoke nothing and are the controls; they should not move.

## The consumer run, in which nothing happened

| arm | before | after |
|--- |---: |---: |
| `ChangeEveryValueMemberRead` | 42.408 μs | 45.601 μs |
| `ChangeEveryValueMethodCalled` | 50.693 μs | 51.040 μs |
| `ChangeEveryValueMethodCalledForAPair` | 79.509 μs | 80.954 μs |
| `ChangeEveryValueMethodCalledForATuple` | 75.718 μs | 75.593 μs |
| `ChangeEveryValueProjected` | 82.095 μs | 81.579 μs |
| `ChangeEveryValueProjectedOverAConstantKey` | 77.039 μs | 81.386 μs |
| `ChangeEveryValueProjectedWithDirectSubscriptionAllowed` | 82.592 μs | 83.390 μs |
| `ChangeEveryValueWithNoObservation` | 8.745 μs | 8.514 μs |

Allocation was byte-identical on all eight arms, which was the strong half of the prediction and is confirmed.

Every time movement is inside noise. The two arms which invoke nothing and were named in advance as controls moved 7.5% and 2.6%, in opposite directions, against a calibrated between-run drift of 8.6%. `ChangeEveryValueProjectedOverAConstantKey` moved 5.6% for no reason at all. **No arm shows the saving, and the prediction of 82 to 72 μs was wrong.**

## The arithmetic, which does not close

The element count was the term flagged as unreliable, and it is not the one that failed. It can be read off the allocation column against constants already on record: `ChangeEveryValueWithNoObservation` allocates 46.88 KB, which at the measured 48-byte floor per underlying property change is exactly 1,000 changes; `MemberRead` at 70.31 KB is 1,000 x 72 B, `MethodCalled` at 125 KB is 1,000 x 128 B, `Projected` at 156.25 KB is 1,000 x 160 B. Every one of those unit costs is on record from a different instrument. **One thousand invocations per operation, confirmed four ways.**

So the saving should have been 10.2 μs on an 82 μs arm — 12.4%, unmissable. It is not there, and the account has to be wrong somewhere.

`ObservableMethodCallExpression` and `ObservableNewExpression` do call `FastInvoke`; that premise was checked and holds. What was never checked is what `FastInvoke` costs, as against what the delegate inside it costs:

```csharp
return invokeConstructorDelegateByConstructor.GetOrAdd(constructor, InvokeConstructorDelegateByConstructorValueFactory)(arguments);
```

**The arms measured `compiled(arguments)`. `FastInvoke` is a `ConcurrentDictionary.GetOrAdd` and then `compiled(arguments)`.** `MethodBase.Invoke` has no such lookup in front of it. A dictionary hit on a reference key is plausibly the same order as the 10.2 ns the delegate saves, which would leave the two paths level — exactly what the consumer arms show.

That is a hypothesis, not a finding. What is a finding is the method error underneath it: **a benchmark of a component is not a benchmark of the thing that contains it**, and the whole case for this change rested on treating one as the other. The rule already in the orientation — an instrument answers only for the operations its arms actually perform — was applied to the *consumer* arms and not to the primitive ones.

`InvokeThroughFastInvoke` and `CallThroughFastInvoke` are added to settle it. They call the public API, so the difference between each and its compiled-delegate neighbour is the lookup, measured rather than supposed.

## Where this leaves the change

The guards are gone and stay gone for now: the primitive is faster, allocation is unchanged everywhere, and nothing regressed. But **the library-level benefit claimed for it is not observed**, and the release note as written promises a speed improvement whose consumer effect is zero as measured. If the lookup turns out to eat the saving, the honest options are to cache the delegate where the caller already has somewhere to put it — the graph nodes hold a `MethodInfo` and a `ConstructorInfo` per node and could hold the delegate beside it — or to revert the note to describe the primitive alone.

## The lookup, measured

| per call | compiled delegate | `FastInvoke` | `MethodBase.Invoke` |
|--- |---: |---: |---: |
| constructor | 2.640 ns | **6.084 ns** | 12.092 ns |
| method | 3.084 ns | **7.812 ns** | 8.584 ns |

**The `ConcurrentDictionary` lookup costs 3.44 ns for a constructor and 4.73 ns for a method** — more than half of what the delegate saves, and for methods more than all of it but a sliver.

So the guard's real price was never 10.2 and 5.2 ns. It is:

- **constructor: 12.092 to 6.084, 6.0 ns saved, a factor of 2.0** — not the 5.0 the delegate alone suggested
- **method: 8.584 to 7.812, 0.77 ns saved, a factor of 1.1** — which is next to nothing

And with that, the consumer run stops being a mystery. Six nanoseconds over a thousand invocations is 6.0 μs on an 82 μs arm, 7.3%, against a between-run drift of 8.6%. **The change was never going to be visible in `ObservationShapeBenchmarks`, and the account closes on the corrected figure where it did not close on the wrong one.** The title of this document was written before the number it names was measured properly; it is left as it stands, because the record of getting it wrong is worth more than a tidy heading.

The release note has been corrected to the measured figures. It had claimed 2.6 ns against 12.8 and 3.0 against 8.2, which are delegate-versus-reflection numbers describing a function that does more than invoke a delegate.

## What this opens

`FastInvoke` spends most of its advantage finding the delegate. Its callers in the graph — `ObservableNewExpression` and `ObservableMethodCallExpression` — hold a `ConstructorInfo` or a `MethodInfo` in a field for the lifetime of the node and look the delegate up afresh on every single evaluation.

Handing them the delegate to keep would take the constructor path from 12.092 ns to 2.640, **9.5 ns per invocation**, which at a thousand invocations is 9.5 μs on an 82 μs arm — 11.5%, and above the drift floor where 7.3% was not. It costs a public addition to `ReflectionExtensions` in Components, since the delegate types are private, and one field per node in Expressions.

That is a proposal and not a finding. Nothing here measures it.
