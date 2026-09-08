# What a call costs, and what the fast helpers do not buy

*2026-09-08 — `ObservationShapeBenchmarks`, `ConstructorInvocationBenchmarks`, DefaultJob, MemoryDiagnoser*

## The twenty-four bytes a method call costs over a member read

Three arms were added to vary one thing each against the instance call taking one argument which was already there. Per change, over a thousand changes, with the 48 bytes of the changed object's own event arguments shown separately because they are the consumer's and not the library's.

| shape | per change | the library's share |
|---|---|---|
| nothing observing | 48.0 B | — |
| a member read | 72.0 B | 24 |
| an instance call, one argument | 96.0 B | 48 |
| **an instance call, no arguments** | **96.0 B** | **48** |
| **two instance calls in a row** | **96.05 B** | **48** |
| **a static call, one argument** | **120.0 B** | **72** |
| a projection | 120.0 B | 72 |

**It is not arity.** No arguments costs exactly what one argument costs.

**It is not the number of calls.** Two calls in a row cost exactly what one call costs, to within a twentieth of a byte. Whatever the 24 is, an expression pays it once however many calls it contains — which kills the obvious reading that each call boxes its own result, since a second call plainly produces a second result.

**A static call costs 24 more than an instance call**, and that is the one new clue. The same shape, the same arity, the same `int` result, and the only difference is that there is no instance. `ObservableMethodCallExpression` invokes through `FastInvoker` with the instance and the operands; the static path is worth reading before anything else is guessed.

So the finding stands, sharper than before: **the term is per observation and not per call, and having no instance costs more than having one.** Recorded, not chased.

## The fast reflection helpers, above .NET 7

`FastGetValue` and `FastSetValue` still guard on `IS_NET_7_0_OR_GREATER` and call `PropertyInfo.GetValue` / `SetValue` above it. Six arms, a thousand iterations each.

| what | mean | allocated |
|---|---|---|
| read `string.Length` directly | 1.635 μs | 23.44 KB |
| read it through `FastGetValue` | **5.545 μs** | 23.44 KB |
| read it through `PropertyInfo.GetValue` | **5.415 μs** | 23.44 KB |
| write `BenchmarkPerson.Rank` directly | 8.721 μs | 46.88 KB |
| write it through `FastSetValue` | **17.674 μs** | **101.56 KB** |
| write it through `PropertyInfo.SetValue` | **13.285 μs** | **70.31 KB** |

**Neither helper is faster than the reflection it wraps, and the setter is worse in both columns.** Reading through `FastGetValue` costs 2.4% more time than `GetValue` and allocates the same. Writing through `FastSetValue` costs **33% more time and 32 bytes more per call** than `SetValue` — the extra being an argument array the `params object?[] index` parameter forces where `SetValue(object, object)` needs none.

The allocation figures decompose cleanly otherwise: the direct write is 48 bytes per call of the changed object's event arguments, the reflected write adds 24 for boxing the value, and the fast write adds 32 more for the array.

**This is the same conclusion `FastInvoke`'s guards got, and it is now measured rather than assumed.** Above .NET 7 the guard buys nothing on the getter and costs on the setter. The open item asked for exactly these three arms per accessor; they have been run.
