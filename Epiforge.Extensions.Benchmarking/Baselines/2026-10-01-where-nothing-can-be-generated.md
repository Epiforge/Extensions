# Where nothing can be generated

*2026-10-01: the libraries run where the runtime cannot generate code, under Native AOT and on iOS, and what that costs*

## How this was measured

`DynamicCodeBenchmarks` ran at `735b631` on the reference machine (Intel Core Ultra 9 275HX, Windows 11 25H2, .NET 10.0.12), in three jobs: `current`, `7.0.4` (the last release, by `[AgainstReleasedExpressions]`), and `no dynamic code` (`[WithoutDynamicCode]`, this repository built with `/p:DynamicCodeSupport=false`). Each job ran three launches of ten 250 ms iterations after four warmup iterations. The run took 4 minutes 17 seconds. The arms work over a thousand people: building and disposing a filtered view; flipping a bit of every person's rank through a filtered view on direct subscription and through one on the graph; the same flips unobserved, which is the floor; and one raise under a thousand fast-path observations of one person, which is a thousand evaluations.

The `no dynamic code` job uses the same toolchain as the others, so its process runs on the JIT with `RuntimeFeature.IsDynamicCodeSupported` false. It measures the paths the libraries take without code generation, not an ahead-of-time compiler. BenchmarkDotNet labels that job's runtime "NativeAOT" because it counts any process in which `RuntimeFeature.IsDynamicCodeCompiled` is false as ahead of time (`RuntimeInformation.IsAot`); every per-benchmark line of the other two jobs says RyuJIT.

Tests at `735b631` on the reference machine, `RunAllTests.ps1`, all passing: Components 207, Collections 653, Expressions 1,055 and Comparisons 48 on each of five frameworks; Components without dynamic code 163 and Expressions without dynamic code 1,054 on each of net8.0, net9.0 and net10.0. Nothing hung.

## What failed without code generation

With dynamic code off at `38ec589`, the Expressions tests failed 686 of 1,055, and a probe of 26 checks (7 expression shapes and 6 queries, where, order by, group by, sum, select and max, on each mechanism) passed only `query direct order by`, under Native AOT on macOS on Apple Silicon and on Linux on x64. In the iOS 26.5 simulator it passed nothing, in Debug or in Release. Four walls stood in the way:

- **Generated methods.** `ReflectionExtensions` and `FastInvoker` built a `DynamicMethod` for every constructor, method and property they invoked, and `FastDefault` reached one for every value type, so both mechanisms failed at the first `int` or `bool` with `PlatformNotSupportedException`.
- **Generic instantiation over value types.** `FastComparer` and `FastEqualityComparer` made a `TypedComparer<T>` by `MakeGenericType`. Native AOT has no code for that instantiation over a value type unless something in the program names it.
- **Delegates of more than two parameters, on Mono.** Direct subscription compiled each lambda to `Func<TArgument, object?[], bool[], object?[], object?[], TResult>`. Without code generation `Expression.Compile` returns an interpreted lambda, and turning that into a delegate takes a thunk. `DelegateHelpers.GetCSharpThunk` in System.Linq.Expressions has thunks pre-built for up to two parameters; for more, it emits a `DynamicMethod`, which Mono in ahead-of-time-only mode refuses with `ExecutionEngineException: Attempting to JIT compile method '(wrapper dynamic-method) … Thunk…'`. In the simulator, a probe compiling `argument.Rank > 1` under five delegate shapes found that the parameter count decides it: one-parameter `Func<ProbePerson, bool>` and a non-generic two-`object?` delegate worked, and every five-parameter shape failed, whether generic over the consumer's type, generic over `object` or non-generic over `object?`. Native AOT passed every direct check with the five-parameter delegate; its SDK sets `System.Linq.Expressions.CanEmitObjectArrayDelegate` false, which sends `CreateObjectArrayDelegate` down a path that emits nothing.
- **That switch is not a remedy on Mono.** Setting `CanEmitObjectArrayDelegate` false in the iOS app made System.Linq.Expressions throw `MissingMethodException: Could not find CreateObjectArrayDelegate`, because the runtime method it looks for is not in Mono, and it broke the graph's two-parameter compiles as well.

A plain .NET for iOS app compiles ahead of time even in Debug; the Debug-build interpreter is .NET MAUI's default, not the platform's. With `UseInterpreter` on, the simulator ran with `IsDynamicCodeSupported` true and passed all 26 checks before any change.

## What changed

- **Components invokes, constructs, defaults and compares through reflection when `RuntimeFeature.IsDynamicCodeSupported` is false.** `ReflectedInvocation` uses `ConstructorInvoker` and `MethodInvoker` on net8.0 and later, and `MethodBase.Invoke` with `BindingFlags.DoNotWrapExceptions` before them, so a fault arrives as itself on either path. `FastInvoker` keeps its per-arity checks for the wrong number of arguments. `FastDefault` uses `RuntimeHelpers.GetUninitializedObject`, or null for `Nullable<T>`. `FastComparer` and `FastEqualityComparer` use a reflected comparer for every type: nulls first, then the type's own `IComparable<T>` or `IEquatable<T>`, found through `GetInterfaces` without `MakeGenericType`, and otherwise the non-generic interface. The emitting paths are behind `[RequiresDynamicCode]` methods, called under `if (RuntimeFeature.IsDynamicCodeSupported)`; net8.0's analyzer does not recognize that guard, so those callers carry a justified `IL3050` suppression.
- **Direct subscription compiles each lambda to a delegate of two parameters**, `Func<TArgument, DirectObservableExpression<TArgument, TResult>, TResult>`. The body's four former parameters are assigned in a block from the observation's own fields, so no field and no allocation is added to any observation.
- **Components, Collections and Expressions are marked `IsAotCompatible`** on net8.0 and later and build without a trimming or AOT warning on every framework. What needs generated code or untrimmed metadata says so: `ExceptionExtensions.GetFullDetails`, `GetImplementationEvents`, `GetImplementationMethods` and `GetImplementationProperties`, `DynamicPropertyChangeNotifier`, the three dynamic disposables, and `XmlSerializerPooledObjectPolicy`. Where Expressions rebuilds a node of a caller's expression with the same types, its suppression says that whatever the node needs the caller's expression needed first. `ObservableDictionaryQuery` reads `KeyValuePair` members by `PropertyInfo`, and `ExpressionDiagramGenerator` uses `Enum.GetValues<ExpressionType>()`.
- **`IsDisposed` reads with acquire semantics** in all six disposable bases. `DisposalExtensions` tests poll `while (!dependency.IsDisposed) {}` while another thread disposes, and with a plain field read the JIT read it once: `DisposeAsyncWhenDependentDisposedNoFactoryArguments` hung on net10.0 on Apple Silicon and on Linux. No test was touched.

Components is 4.4.0, validated against 4.3.0, and Expressions 7.1.0, validated against 7.0.4. Collections is unchanged at 4.3.1.

## A defect the proof of concept had

The first proof of concept compared value types boxed, through the non-generic `Comparer.Default` and a boxed equality comparer. `DynamicCodeAgreement` caught it before the round was written. A struct which implements only `IComparable<T>` made the non-generic comparer throw, a type implementing both orderings was ordered by the wrong one, and boxed equality ignored `IEquatable<T>` where it disagrees with `Equals(object)`. The round's comparers call the type's own generic interface, as the default comparers do.

## How it was checked

- `DynamicCodeAgreement` (13 tests, Components) states what the two paths must agree on: ordering by `IComparable<T>` when it is the only ordering and in preference to `IComparable`; nullable and enum ordering; equality by `IEquatable<T>` over `Equals(object)`; `FastDefault` of value types, `int?` and a struct; constructors and methods of arity 0 to 3 through `FastInvoke` and `GetFastInvoker`; properties and indexers; a fault arriving as itself; and an invoker refusing the wrong number of arguments.
- `Epiforge.Extensions.Components.WithoutDynamicCode.Tests` and `Epiforge.Extensions.Expressions.WithoutDynamicCode.Tests` build the existing tests with `DynamicCodeSupport` false on net8.0, net9.0 and net10.0. net6.0 and net7.0 ignore that switch. Components leaves out the eight files whose tests create NSubstitute substitutes, and Expressions leaves out `ConcatenationUnderChange`, whose `DispatchProxy` needs dynamic code; both are test scaffolding. Before the change, in the sandbox, they failed 11 of 163 and 685 of 1,054.
- `Epiforge.Extensions.AotProbe` runs the 26 checks and prints the framework, the runtime identifier, `IsDynamicCodeSupported`, `IsDynamicCodeCompiled` and whether a `DynamicMethod` can be made; `Epiforge.Extensions.AotProbe.iOS` runs the same checks in an iOS app and writes them to `Documents/aot-probe.txt`; `run.sh` runs both, with the suites, on the machine it is on.

| probe, 26 checks | before | after |
|---|---:|---:|
| JIT, dynamic code on (macOS) | 26 | 26 |
| JIT, dynamic code off (macOS) | 1 | 26 |
| Native AOT, osx-arm64 | 1 | 26 |
| Native AOT, linux-x64 | 1 | 26 |
| iOS 26.5 simulator, Debug | 0 | 26 |
| iOS 26.5 simulator, Release | 0 | 26 |
| iOS 26.5 simulator, Release with the interpreter | 26 | 26 |

With the reflection change alone and not yet the two-parameter delegate, iOS Debug and Release passed 13: every graph check and no direct one. The linux-x64 row is from the sandbox; every other row is from an Apple Silicon Mac.

## Predicted and measured

The rule was set before the run: if `current` allocates what 7.0.4 does on every arm and no arm moves in time by 9% or more, the two-parameter delegate applies everywhere; otherwise only without dynamic code.

| per op | 7.0.4 | current | | allocated, both |
|---|---:|---:|---:|---:|
| building and disposing a filtered view | 305.85 μs | 298.96 μs | 0.98x | 876,800 B |
| changing every rank through a filtered view | 22.40 μs | 22.73 μs | 1.02x | 48,000 B |
| the same on the graph | 45.36 μs | 45.36 μs | 1.00x | 72,000 B |
| changing every rank unobserved | 8.86 μs | 8.99 μs | 1.02x | 48,000 B |
| one raise under a thousand observations | 12.31 μs | 12.32 μs | 1.00x | 48 B |

**The two-parameter delegate stays unconditional.** The allocation is identical on every arm, and the largest difference in time is 2.3%.

| without dynamic code | with | without | | predicted |
|---|---:|---:|---:|---|
| an evaluation on direct subscription | 12.3 ns, 0 B | 97.0 ns, 232 B | 7.9x | ~450 ns, ~232 B |
| a property change in a filtered view, direct subscription | 13.7 ns, 0 B | 103.9 ns, 232 B | 7.6x | several times, ~230 B more |
| the same on the graph | 36.4 ns, 24 B | 616.4 ns, 296 B | 16.9x | ~14x |
| building and disposing a filtered view of a thousand | 299.0 μs, 856.3 KB | 668.7 μs, 1,106.2 KB | 2.24x | a few times slower |

Each change is above the unobserved floor of its own job, which without dynamic code was 9.23 μs, 1.03x the floor with it. An evaluation is the raise arm divided by its thousand observations, less the 48 B the raise itself allocates.

The allocation predictions held, to the byte for an evaluation. **The evaluation's time did not: 97.0 ns against a predicted 450 ns.** The prediction came from the sandbox, which is not the reference machine. The graph's ratio came in at 16.9x against a predicted 14x. A filtered change at 7.6x is "several times". The build at 2.24x and 1.29x the allocation, 256 B more per element, is consistent with "a few times slower" only loosely. It also contradicts an earlier sandbox note, taken on the proof of concept, that building would be cheaper without compilation.

The interpreter's 232 B per evaluation is the main cost of observing without code generation. Whether direct subscription should skip `Expression.Compile` and evaluate some other way there is not part of this change.

## The readmes and the release notes

- The Expressions readme and the root readme's Expressions part gain "Ahead-of-Time Compilation": where the libraries were checked, that an app with Mono's interpreter on worked before, that the packages are marked compatible, and the table above without the predictions. The root readme's contents gain the entry.
- The Components readme and the root readme's Components part gain a sentence under Reflection: these methods and the comparers work through reflection where code cannot be generated, and the three `GetImplementation…` methods are annotated.
- Expressions' release notes gain a line with the costs above and the 7.0.4 comparison. Components' release notes already describe the reflected paths, the annotations and the `IsDisposed` fix.
