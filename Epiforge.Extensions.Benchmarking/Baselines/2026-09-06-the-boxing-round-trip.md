# 6 September 2026 — the boxing round trip

A struct result which does not implement `IEquatable<T>` costs **96 bytes more per evaluation** than a structurally identical struct which does. That is the dominant term in the 160 bytes `2026-09-05-the-hundred-and-sixty-bytes.md` spent four passes chasing, and it has nothing to do with the node that produces the value.

## What the shape instrument says

`ObservationShapeBenchmarks`, seven arms, a thousand observations each re-evaluated on a property change. Four arms replicate figures already on record and all four reproduced, which is what licenses the fifth.

| arm | result type | `IEquatable<T>` | per change | named | residue |
| --- | --- | --- | ---: | ---: | ---: |
| no observation | — | — | 48 B | 48 | 0 |
| `pair.Value.Rank` | `int` | yes | 72 B | 72 | 0 |
| `pair.Value.Rank.CompareTo(0)` | `int` | yes | 128 B | 128 | 0 |
| `ValueTuple.Create(pair.Value.Rank, 0)` | `ValueTuple<int, int>` | **yes** | **160 B** | 136 | **24** |
| `KeyValuePair.Create(pair.Value.Rank, 0)` | `KeyValuePair<int, int>` | **no** | **256 B** | 136 | **120** |
| `new KeyValuePair<int, int>(pair.Key, pair.Value.Rank)` | `KeyValuePair<int, int>` | **no** | **256 B** | 136 | **120** |
| `new KeyValuePair<int, int>(0, pair.Value.Rank)` | `KeyValuePair<int, int>` | **no** | **256 B** | 136 | **120** |

`ValueTuple<int, int>` and `KeyValuePair<int, int>` have the same size, the same two `int` fields and the same generic arity. The measured difference between them is **96 bytes per change**, and the documented difference between them is that one implements `IEquatable<T>` and overrides `Equals(object)` and the other does neither.

## The mechanism, which the measurement located rather than reading

`FastEqualityComparer` holds a shim per type. `TypedComparer<T>.AreEqual(object? x, object? y)` unboxes both arguments and calls `EqualityComparer<T>.Default.Equals(T, T)`.

The values arrive **already boxed** — `ObservableExpression.evaluation.Result` is `object?`. For a value type without `IEquatable<T>` the runtime's default comparer is `ObjectEqualityComparer<T>`, whose `Equals(T x, T y)` calls `x.Equals(y)`, which is `ValueType.Equals(object)` and therefore **boxes `y` again**.

So the sequence is: arrive boxed, unbox, re-box, compare. For a type that implements `IEquatable<T>` the runtime picks the generic comparer instead and the round trip does not happen, which is why `int` and `ValueTuple<int, int>` show none of it.

`2026-08-29-typed-comparers.md` removed the reflection from this path and took a comparison from 64 bytes to zero for the types it measured — all of which implement `IEquatable<T>`. The case that does not was never exercised.

## The change

`FastEqualityComparer` gains a second shim, `BoxedComparer`, selected in the constructor when the type is a value type that does not implement `IEquatable<T>`. It compares the boxed values as they arrive — `x.Equals(y)` — which reaches exactly the same `ValueType.Equals(object)` the default comparer reaches, with `y` already boxed.

**Semantics are unchanged by construction.** Both paths end in the same virtual call on the same value: a struct which overrides `Equals(object)` gets its override either way, and one which does not gets the runtime's structural comparison either way. `GetHashCode` likewise reaches `ValueType.GetHashCode` from both. The public surface does not move.

This is the shape `2026-08-29-typed-comparers.md` established — a shim chosen once per type, a virtual call at the site, no reflection and no IL emit — extended to the case it did not cover.

## The prediction, before the run

The three `KeyValuePair` arms fall from **256 B to 160 B**, matching the `ValueTuple` arm exactly, because the only difference between those types then costs nothing.

- If they fall to 160, the round trip was the whole 96 and the count works out to four boxed comparisons per change.
- **If they fall only to about 232**, one comparison per change was removed rather than four, the 96 is not all round trip, and the remainder is unlocated.
- The `ValueTuple`, `int` and floor arms **must not move at all**. Any of them moving means the shim selection caught a type it should not have.

**The 24 bytes by which the `ValueTuple` arm exceeds its named model are not addressed here and are not predicted to move.** An observation returning an eight-byte two-field struct costs 24 more than the named array-plus-box model, where one returning an `int` costs exactly the model. That is a separate residue, it is smaller than the one this document closes, and it is now the only unexplained figure left on this path.

## Run it with

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *ObservationShape*
```

## The after, and a prediction wrong by half

| arm | before | after | change |
| --- | ---: | ---: | ---: |
| `KeyValuePair.Create(…)` | 256 B | **208 B** | −48 |
| `new KeyValuePair<int, int>(pair.Key, …)` | 256 B | **208 B** | −48 |
| `new KeyValuePair<int, int>(0, …)` | 256 B | **208 B** | −48 |
| `ValueTuple.Create(…)` | 160 B | **160 B** | — |
| `pair.Value.Rank.CompareTo(0)` | 128 B | **128 B** | — |
| `pair.Value.Rank` | 72 B | **72 B** | — |
| no observation | 48 B | **48 B** | — |

**Predicted 160. Measured 208.** The four arms which had to hold, held exactly, so the shim selection caught only the types it was meant to and nothing else in the run moved.

The change is real and it is half of what was predicted. Forty-eight bytes per change is **two** boxed comparisons removed, not four. The prediction named 232 as the outcome that would mean "one comparison per change rather than four" — so the answer was not even one of the two possibilities offered. **Two comparisons of the result go through `FastEqualityComparer` on every change; the assumption that all four did was never checked and was wrong.**

**No time claim is made.** The three moved arms fell 5–6%, but the floor arm — which allocates identically and executes nothing that changed — fell **7.5%** in the same run. The control moved further than the arms did, so between-run drift for this pair swamps the effect and the time is unreadable. The allocation is not in doubt.

## What is left, and it is the same size as what was taken

| | per change |
| --- | ---: |
| a `KeyValuePair<int, int>` result, now | 208 B |
| a `ValueTuple<int, int>` result | 160 B |
| **still attributable to the missing `IEquatable<T>`** | **48 B** |
| the named model for either | 136 B |
| **unexplained for the tuple as well** | **24 B** |

So the pair result now carries 72 bytes above the named model: 48 which the tuple does not carry, and 24 which it does.

The 48 is two more boxed comparisons somewhere that does not route through `FastEqualityComparer`. `ScopedObservableExpression.ResultEquals` is a separate, abstract, per-scope comparison of the same values on the same change, and it is the obvious place to look — **but that is reading, and reading has been wrong five times on this path already.** The instrument needs no new arms to test it: the same three pair arms should fall from 208 toward 160 if that site is fixed the same way, and the tuple, `int` and floor arms must again not move.

## Judgment

Forty-eight bytes on every re-evaluation of an observation whose result is a value type without `IEquatable<T>` is worth keeping on its own. Every dictionary select query produces `KeyValuePair` results, so this is not an exotic case — it is the projection path of the whole dictionary side.

The remaining 48 is the same size, likely the same shape, and costs one contained change and one filtered run of an instrument that already exists.
