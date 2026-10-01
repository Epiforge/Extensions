# What the interpreter cost

*2026-10-01: without dynamic code, the observer evaluates the lambdas it would compile instead of interpreting them, and reads properties by the cheaper of the two reflected routes*

## What it cost

Without dynamic code, `Compile` gives the LINQ expression interpreter, and every lambda the observer compiled ran there. On the reference machine an evaluation on direct subscription cost 97.0 ns and 232 B, against 12.3 ns and nothing with dynamic code; a change on the graph cost 616.4 ns and 296 B against 36.4 ns and 24 B. The interpreter allocates a frame of 168 B for every call before it does any work, and reaches every member through reflection.

## What changed

- **`ClosureCompiler`** (new, internal, .NET 8 and later) turns an expression tree into a tree of nodes evaluated over boxed values, with one frame of slots per thread reused from call to call. It covers constants, parameters, fields, properties, calls, indexers, construction, member initialization, arrays, conditionals, blocks with variables, type tests, `try` with `catch` and `finally`, inlined invocation of a lambda, assignment, the short-circuiting and coalescing operators, conversions and every unary and binary operator, lifted or not. What it does not cover, such as by-reference parameters, pointers, by-reference-like types and `fault` blocks, makes it decline, and the lambda is compiled as before.
- **`ClosureOperations`** (new, generated) holds every primitive operator and conversion over the eleven primitive types, each written as the C# operator that compiles to the instruction a compiled lambda executes. So checked and unchecked arithmetic, NaN, negative zero, shifts and division by zero give what a compiled lambda gives. Casts follow the rule measured in [What a cast would do](2026-10-01-what-a-cast-would-do.md); lifted operators follow the expression compiler's: a comparison with `null` is false, or `null` when lifted to `null`; `null` equals `null`; `bool?` `And` and `Or` are three-valued; a lifted conversion to a type that is not nullable throws `InvalidOperationException`.
- **Where it is used**, only when `RuntimeFeature.IsDynamicCodeSupported` is false: direct subscription's evaluation, the graph's binary, unary, type-binary and coalescing nodes, and a scalar transform.
- **Properties are read through `PropertyInfo.GetValue`**, with a getter's exception unwrapped and rethrown as itself. Calls, indexers, setters and constructors go through `MethodInvoker`.

## Why properties are read through `GetValue`

The first run of `DynamicCodeBenchmarks` on this change allocated exactly as predicted and failed on time: without dynamic code the raising arm went from 101.0 to 154.7 μs and a change on direct subscription from 104.4 to 156.1 ns above the floor, while the graph's change, whose closures hold only operators, fell 7.5%. `EvaluationRouteBenchmarks` (new) split one evaluation of `person.Rank > 0` by the route that reaches the property, on the JIT, on the JIT with dynamic code off, and under Native AOT through BenchmarkDotNet's Native AOT toolchain (`NativeAotAttribute`, new). Its second run:

| per call | JIT | JIT, dynamic code off | Native AOT |
|---|---:|---:|---:|
| a getter through `MethodInvoker` | 4.9 ns | 117.5 ns | 7.6 ns |
| a getter through `PropertyInfo.GetValue` | 5.4 ns | 28.5 ns | 7.8 ns |
| a getter through `MethodBase.Invoke`, exceptions unwrapped | 5.2 ns | 29.7 ns | 8.7 ns |
| `string.IndexOf(char)` through `MethodInvoker` | 5.3 ns | 130.8 ns | 11.0 ns |
| `string.IndexOf(char)` through `MethodBase.Invoke` | 7.3 ns | 133.2 ns | 14.6 ns |
| the lambda, interpreted (184 B) | 28.5 ns | 60.8 ns | 43.1 ns |
| one evaluation by the observer, above a raise with no observer | 11.9 ns, 0 B | 47.1 ns, 24 B | 38.4 ns, 24 B |

With dynamic code off on the JIT, a getter with no arguments costs about 4x as much through `MethodInvoker` as through `GetValue` or `Invoke`; with one argument the two routes cost the same, and under Native AOT `MethodInvoker` is the cheaper. So getters moved and nothing else did. The JIT with dynamic code off is what an application published for Native AOT runs as under `dotnet run` and the debugger, and what the tests without dynamic code run as. How the runtime prices these routes was measured, not read.

## How it was checked

- **A differential in the sandbox**: 5,455 generated lambdas over 264,877 inputs, evaluated by closures and by lambdas compiled on the JIT, with no difference in result, result type or fault type.
- **`CompiledAgreement`** (new, three tests) observes every primitive operator and conversion over every primitive, enumeration and nullable type, on direct subscription and on the graph, and compares each result and fault with the compiled lambda's. It passes in both Expressions test projects, so without dynamic code it compares closures with the interpreter.
- **The suites** on the reference machine, after each change: Components 218, Components without dynamic code 174, Collections 653, Comparisons 48, Expressions 1,058, Expressions without dynamic code 1,057. All pass.
- **The Native AOT probe** in the sandbox passes 26 of 26 with a clean publish. Mono's ahead-of-time compilation on iOS has not run this change yet.

## Predicted and measured

`DynamicCodeBenchmarks`, without dynamic code, per op. The befores are the run of [What a cast would do](2026-10-01-what-a-cast-would-do.md).

| | before | closures | closures, getters through `GetValue` | predicted for the last |
|---|---:|---:|---:|---|
| one raise under a thousand observations | 101.0 μs, 232,048 B | 154.7 μs, 24,048 B | 44.4 μs, 24,048 B | 50 to 60 μs |
| a change through a filtered view on direct subscription, above the floor | 104.4 ns, 232 B | 156.1 ns, 24 B | 49.8 ns, 24 B | 50 to 60 ns |
| the same on the graph | 623.1 ns, 296 B | 576.1 ns, 72 B | 569.1 ns, 72 B | within 3% |
| building and disposing a filtered view | 654.9 μs, 1,106.2 KB | 687.0 μs, 903.1 KB | 583.3 μs, 903.1 KB | within 3% |

Every allocation came in as predicted. Times did not, in both runs:

- **The first run** failed its rule, as above.
- **The second** was faster than predicted on the raising arm, and on direct subscription by 0.2 ns. Building was 15% faster where I predicted no change: building a view evaluates the filter once for each of its thousand people, and 1,000 times the 106 ns an evaluation saved in `EvaluationRouteBenchmarks` is close to the 104 μs it gained.
- **`EvaluationRouteBenchmarks`**: every prediction for its first run held. In the second, `IndexOf` through `Invoke` came in at 133.2 ns where I predicted 25 to 45 ns, and the getter through `MethodInvoker` without dynamic code at 117.5 ns where I predicted within 5% of 130.4 ns; its deviation in that run was 23.3 ns.

With dynamic code nothing in this change runs. The current job in the last run is within 2.4% of 7.0.4 on every arm but the raising one, 4.7% over on a deviation of 0.60 μs, and its unobserved floor moved 3.3% the other way.

## What Apple Silicon showed

Before this release, `DynamicCodeBenchmarks` ran on an M3 MacBook Air with dynamic code, against 7.0.4, to see whether anything done for ahead-of-time compilation cost a JIT. Allocation was identical on every arm, and time was within 3% on every arm but the raising one. That arm, a thousand cheap evaluations, came in 5.7% slower, then 6.2% and 6.1% in two runs of it alone: about 0.49 ns an evaluation. On the reference machine the same arm had read +0.0%, +0.6%, +1.1% and +4.7% across four runs, within the variance between runs there.

With dynamic code, the only thing an evaluation does that 7.0.4's did not was the change of the morning's round which made direct subscription's delegate take two parameters and read the observation's values, reached groups, links and held slots from the observation itself. A runtime without dynamic code needs that, because it can make a delegate of at most two parameters from an interpreted lambda without a generated thunk, but a JIT does not, and closures do not either. So:

- **With dynamic code**, direct subscription compiles 7.0.4's five-parameter delegate again; `DirectObservableExpression` is 7.0.4's to the byte, but for line endings.
- **Without it**, closures take the five as parameters through a C# lambda, which also spares them the two virtual reads they made through the observation.
- **Only a lambda closures do not cover** keeps a two-parameter interpreted delegate, and `DirectEvaluationState` (new) carries the four arrays to it: one carrier per thread, read into locals before anything else and cleared after each call, so it allocates nothing and keeps nothing alive.

| `RaiseUnderAThousandObservations`, with dynamic code, against 7.0.4 | two parameters | five |
|---|---:|---:|
| M3 MacBook Air | +5.7%, +6.2%, +6.1% | +0.1%, −1.2% |
| reference machine | +4.7% (last of four runs) | +1.8% |

Allocation is 48 B in every run on both. Without dynamic code the arm measured 44.3 μs and 24,048 B on the reference machine, against 44.4 μs before, and 57.1 and 56.3 μs on the Mac. The reference machine's run had two misses of the "unchanged" I predicted, both in arms this does not reach: the graph's change with dynamic code at +6.4% against 7.0.4, on a deviation of 2.39 μs against 0.84, and building without dynamic code at +3.7%, in a run whose builds with dynamic code were also 3% to 4% slower than the last. Every suite passed on both machines at the counts above, and on the Mac the probe passed 26 of 26 on the JIT with and without dynamic code, under Native AOT for osx-arm64, and in the iOS simulator in Debug, Release, and Release with the interpreter.

## The readmes and the release notes

- **Both readmes:** the Ahead-of-Time Compilation section gives the last run's figures, says why an observation costs more there, and gives the cost of an evaluation under Native AOT itself.
- **Expressions' release notes** gain a line on the closures and replace the cost line with the same figures.
- **Expressions' release notes** also say that the two-parameter delegate is only for what closures do not cover without dynamic code, and that with dynamic code direct subscription compiles 7.0.4's delegate; the cost line claims only allocation equal to 7.0.4's with dynamic code.
