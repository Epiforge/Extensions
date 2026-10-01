# What a cast would do

*2026-10-01: the reflected invokers and comparers accept, convert and refuse what the generated ones do, and the generated ones stop failing where they cannot work*

## What disagreed

Components 4.4.0 runs without dynamic code by invoking and comparing through reflection, and its release notes promise the same results either way. I checked that promise in the sandbox at `735b631`. A console probe ran 138 invocation cases and 75 comparer and default cases, on net8.0, net9.0 and net10.0, built once with `DynamicCodeSupport` true and once false, and recorded each result or exception with its message. The outcomes were the same on all three frameworks. Most cases that differed were misuse, but they are what a caller sees:

- **`null` for a value-type parameter.** The generated invokers throw `NullReferenceException`. The reflected ones passed the type's default and called the member.
- **A value which would need widening**, such as a `short` or `char` for an `int`, or an `int` for a `long` or `double`. The generated invokers throw `InvalidCastException`. The reflected ones widened it and called the member.
- **An argument or instance of the wrong type.** The generated invokers throw `InvalidCastException`, or `NullReferenceException` for a `null` instance. The reflected ones threw `ArgumentException` or `TargetException`.
- **The comparers.**
  - `Compare` on the `int` comparer with `null` and `1` throws `NullReferenceException` generated and returned -1 reflected.
  - `Compare` on the `DayOfWeek` comparer with `Monday` and `1` returns 0 generated and threw reflected.
  - `GetHashCode(null)` on the `string` comparer returns 0 generated and threw `NullReferenceException` reflected.
  - Values of the wrong type threw `ArgumentException` or `TargetException` reflected where the generated comparers throw `InvalidCastException`.

The generated path had defects of its own, which are not a matter of agreement:

- **`ReflectionExtensions.FastInvoke` never checked the number of arguments.** Too few threw `IndexOutOfRangeException`, too many were silently ignored, and a `null` list threw `NullReferenceException` for a member with parameters. `FastInvoker` has always checked.
- **Members which cannot be invoked through objects:**
  - `ref` and `out` parameters, pointer parameters and returns, by-reference returns, and `Span<T>` parameters and returns threw `InvalidProgramException` or `NullReferenceException` when invoked, or returned `null` for a pointer.
  - Open generic methods and constructors of open generic types threw `InvalidProgramException`.
- **`FastDefault` of a type with no boxable default** threw `ArgumentException` for `void` and `Span<int>`, `BadImageFormatException` for `TypedReference`, and `InvalidProgramException` for `KeyValuePair<,>`.
- **`FastEqualityComparer` for `int?`** treated `null` as equal to `null` but threw `NullReferenceException` hashing it. The boxed comparer, which every value type without its own `IEquatable<T>` gets, `Nullable<T>` included, called `GetHashCode` on the argument without checking it.
- **On .NET 6 only, `FastGetValue` and `FastSetValue` went through `FastInvoke`**, with the generated path's semantics. On .NET 7 and later they call `PropertyInfo.GetValue` and `SetValue`, so setting `null` on an `int` property gave `NullReferenceException` on one framework and 0 on the others.
- **The Components readme said** "Use of these methods in .NET 7 or later will simply call the built-in methods". That is true of `FastGetValue` and `FastSetValue` only.

## What a cast does

The generated invokers cast every argument with `unbox.any` or `castclass`, as `(T)(object)value` does. A sandbox oracle cast 33 values to 49 types through a generic method and compared the outcomes, messages and result types included, with a rule written in C#. They agreed on all 1,617 pairs:

- **A non-nullable value type** takes a value of exactly its type, or of an enumeration or primitive type with the same underlying type: `DayOfWeek` for `int`, `int` for `DayOfWeek`, and one `int`-based enumeration for another. It does not take `uint` for `int`, or one enumeration for another with a different underlying type. It refuses `null` with `NullReferenceException`.
- **`Nullable<U>`** takes `null` or a value of exactly `U`, and nothing else, not even an enumeration over `U`.
- **Any other type** takes `null` or anything `Type.IsInstanceOfType` accepts. That includes array covariance and `int[]` for `uint[]`, which the runtime's cast allows.
- **The refusal** is `InvalidCastException` with the message `Unable to cast object of type '{value's type}' to type '{type}'.`

## What changed

- **`ObjectCast`** (new, internal) applies that rule. It returns a converted value where the cast converts: `Enum.ToObject` for an enumeration, `Convert.ChangeType` for a primitive.
- **`ReflectedInvocation`** casts the instance and every argument before invoking. A `null` instance for an instance method throws `NullReferenceException`, as calling one does. The argument array is copied only when a cast converts a value.
- **`FastComparer` and `FastEqualityComparer` without dynamic code** cast both values before comparing. The equality comparer now chooses the boxed comparer exactly where the generated one does: a value type without its own `IEquatable<T>`.
- **The boxed equality comparer hashes `null` as 0**, on both paths. Making `Nullable<T>` use the typed comparer instead would have cost 0.9 ns per `int?` comparison in the sandbox, so it keeps the boxed comparer and its no-cast semantics.
- **`FastInvoker` refuses what it cannot invoke**, on both paths and at invocation, as before:
  - `NotSupportedException` for a reference, pointer or by-reference-like parameter or return;
  - `InvalidOperationException` with the runtime's own messages for an open generic member and for an abstract class's constructor.
- **`ReflectionExtensions.FastInvoke`** goes through `FastInvoker`, so a wrong count throws `FastInvoker`'s `ArgumentException` and a `null` list means none. This deletes the second pair of IL emitters, 90 lines.
- **`FastDefault`** throws `ArgumentException` for `void`, by-reference-like and open generic value types.
- **`FastGetValue` and `FastSetValue`** call `PropertyInfo.GetValue` and `SetValue` on every framework.

Components stays 4.4.0, still unreleased. Package validation against 4.3.0 passes with no suppressions.

## How it was checked

- **New tests.** `DynamicCodeAgreement` gained eleven tests. They run on the generated path in `Epiforge.Extensions.Components.Tests` and on the reflected path in `Epiforge.Extensions.Components.WithoutDynamicCode.Tests`.
- **Befores.** The run before the change failed exactly the predicted tests: 4 on net7.0 to net10.0 and 5 on net6.0 with dynamic code, and 10 on net8.0 to net10.0 without. The reflected path failed every agreement test, and the generated path failed the four tests of its own defects. The fifth on net6.0 was the property test.
- **Afters.** After the change: Components 218 on all five frameworks; without dynamic code 174 on three; Collections 653; Comparisons 48; Expressions 1,055; Expressions without dynamic code 1,054. All pass.
- **Native AOT.** In the sandbox, the 138-case invocation probe published with Native AOT for linux-x64 gives the same results and messages as the JIT with dynamic code in every case. The AOT probe passes 26 of 26 with a clean publish.

## Predicted and measured

`ConstructorInvocationBenchmarks` and `DynamicCodeBenchmarks` ran on the change on the reference machine. The befores for the first are its run of 27 September, whose `FastInvoke` code is the code this change replaced. The befores for the second are this morning's run. Both runs drifted between dates, so each arm is also read against a control in its own run.

| `FastInvoke`, 1,000 calls | before | after | per call | against its control |
|---|---:|---:|---:|---:|
| `string.IndexOf(char)` | 7.803 μs | 7.980 μs | +0.18 ns | +0.56 ns |
| `KeyValuePair<int, int>(1, 2)` | 6.096 μs | 6.234 μs | +0.14 ns | +0.31 ns |
| control, compiled method delegate | 3.163 μs | 3.009 μs | | |
| control, compiled constructor delegate | 2.666 μs | 2.588 μs | | |

Allocation is 23.44 KB on every arm in both runs. I predicted +1 to +3 ns per call and set the rule to accept up to +3 ns. The cost came in below the predicted range, so routing through `FastInvoker` stays. The property arms moved with their direct controls.

| `DynamicCodeBenchmarks`, per op | this morning | after |
|---|---:|---:|
| current against 7.0.4, largest difference | 2.3% | 0.6% |
| no dynamic code, a change through a filtered view on the graph, above the floor | 616.4 ns | 623.1 ns |
| no dynamic code, a change through a filtered view on direct subscription, above the floor | 103.9 ns | 104.4 ns |
| no dynamic code, one raise under a thousand observations | 96.99 μs | 101.00 μs |
| no dynamic code, building and disposing a filtered view | 668.7 μs | 654.9 μs |

Allocation is unchanged on every arm. The graph's change is +1.1%, within the predicted 5%. The raise arm is +4.1% against a predicted 3%, a miss. Its path contains none of this change: direct subscription evaluates through an interpreted lambda and compares results with `EqualityComparer<TResult>.Default`, not `FastEqualityComparer` or `FastInvoker`. So I read it as the variance between runs of the interpreted path, whose standard deviation this morning was 2.57 μs. Both jobs with dynamic code ran 2% to 4% faster than this morning on their own.

## The readmes and the release notes

- Both readmes replace the false .NET 7 sentence with one saying that `FastGetValue` and `FastSetValue` call the built-in methods, which are optimized from .NET 7 on.
- Components' release notes gain three lines: what the reflected paths now refuse and convert; the argument count, the members that cannot be invoked through objects, and `FastDefault`; and the hash of `null` and the .NET 6 properties.
