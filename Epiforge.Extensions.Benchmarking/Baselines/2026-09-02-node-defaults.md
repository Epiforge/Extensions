# Benchmark comparison, 2 September 2026 — the default every node boxes

Every `ObservableExpression` records the default value of its type, which it holds as its result until it first evaluates and returns to whenever it faults. The constructor obtained it from `ReflectionExtensions.FastDefault`.

`FastDefault` invokes a cached delegate returning `default(T)` boxed. The delegate is cached; **the box is not**. Every call allocates a fresh one, so every node over a value type paid twenty-four bytes at construction — and the graph builds a node per element.

This was found by reading, while pricing something else. It is exactly the sort of claim this series has been wrong about when it was reasoned rather than read, so the mechanism was confirmed in the source before anything was written.

## The design, and the two things it deliberately does not do

**It does not change `FastDefault`.** That method is public in Components, its consumers are unknown, and a boxed struct is mutable in place through `Unsafe.Unbox` or `FieldInfo.SetValue`. Handing every caller the same box to save an allocation inside Expressions is not a trade worth making on somebody else's behalf. The sharing lives in `ObservableExpression`, where every consumer of the value is in this assembly and visible.

**It does not share a default for every value type.** A struct implementing `IDisposable` whose `Dispose` mutates its own fields would corrupt the shared box for every later node of that type — and this is reachable, not theoretical: `defaultResult` is precisely what a faulted node holds, and it becomes the `previousValue` handed to `DisposeIfNecessaryAndPossible` on the next change. `ObservableExpression.DefaultResult` therefore gates on `ExpressionObserverOptions.CannotBeDisposed`, which for a value type means sealed and implementing neither disposal interface. `int`, `bool`, `double` and `DateTime` qualify; a disposable struct falls through to a fresh box, as before.

The gate runs once per type, inside the cache's value factory, rather than on every node. A reference type short-circuits before the dictionary is touched at all, since its default is null and costs nothing. A null cache entry means "not shareable"; it is also what `Nullable<T>` stores, which costs a redundant but allocation-free call and yields the right answer on either reading.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *NodeDefaultBenchmarks*
```

## The suite

`NodeDefaultBenchmarks` constructs and disposes a query over a thousand elements, three arms. The change is construction-side and invisible to any propagation benchmark, so it needed its own instrument.

The control is `ConstructStringSelectorGraph`, observing `person => person.Name` through the graph. Its nodes are a constant of the person and a member access yielding a string — both reference-typed, both with a null default, neither allocating a box. It cannot move.

## The prediction

Written before the run, by counting the nodes whose type is a value type.

`person => person.Rank > 0` on the graph, once the parameter is replaced, is a constant of the person (reference), the `.Rank` member (`int`), the constant `0` (`int`) and the comparison (`bool`). The constant `0` is the same `ConstantExpression` instance in every element's tree and the observer caches nodes by structural equality, so it is **one node for the whole query**, not a thousand. That leaves two boxes per element: **48 bytes**, or about 47 KB at a thousand elements.

The fast path builds one node per observation, of type `bool`: **24 bytes** per element, about 23 KB.

## Result

| arm | before | after | |
|--- |---: |---: |--- |
| `ConstructStringSelectorGraph` | 1,865.1 μs / 1.95 MB | 1,831.6 μs / **1.95 MB** | control, unmoved |
| `ConstructComparisonGraph` | 2,202.9 μs / 2.67 MB | 2,175.1 μs / **2.62 MB** | predicted 2.62 |
| `ConstructComparisonDirect` | 349.8 μs / 1.19 MB | 351.9 μs / **1.17 MB** | predicted 1.17 |

Both moved arms landed on the predicted figure and the control did not move at all.

**The resolution deserves stating rather than glossing.** BenchmarkDotNet prints these totals to two decimal places of a mebibyte, so a displayed 2.67 is anywhere in a ten-kilobyte band. The most that can honestly be claimed is that the graph arm fell by between 42 and 63 KB against a prediction of 47, and the direct arm by between 10 and 31 KB against a prediction of 23. Both predictions sit inside their bands; neither is confirmed to the byte the way `GraphResultBoxingBenchmarks` confirmed its four. A finer instrument would report at a smaller element count where the units drop to KB, and if this figure ever needs to be exact, that is how to get it.

**No time claim is made.** The three arms moved −1.8%, −1.3% and +0.6%, each within about one standard deviation of its own run, and the standing lesson from `2026-09-02-graph-boolean-boxes.md` is that a within-run deviation understates between-run variance by several times. A dictionary lookup replacing a delegate invocation and an allocation ought to be faster, and this instrument cannot show it. Recorded as unmeasured rather than as neutral.

## Proportion, which is the honest framing

Forty-seven kilobytes against 2.67 megabytes is **1.8%**; twenty-three against 1.19 is **1.9%**. This is a small change and it should be described as one. It was taken because it is contained, costs nothing at runtime, has a safety rule that can be stated in one sentence and checked in one line, and applies to every observation the library will ever construct.

## What it does not reach

The two boxed integers per element that `GraphResultBoxingBenchmarks` measures are *evaluation* results, not defaults, and are untouched. The graph still boxes every non-boolean result on every evaluation; only typing its results end to end would change that, and that is a large change to the observer's model rather than a contained one.
