# Benchmark comparison, 6 September 2026 — the argument array

`ObservableExpression.EvaluationResults` built a fresh `object?[]` on every evaluation of every method call, indexer and constructor node, filled it from the operand nodes, and handed it to `FastInvoke` — which looked its delegate up in a `ConcurrentDictionary` and then read the array back out. `2026-09-06-the-guard-that-cost-five-times.md` measured that lookup at 3.44 ns for a constructor and 4.73 for a method, more than half of what invoking through the delegate saves. The array was the larger prize and the exact one.

This is unreleased work.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

`ObservationShapeBenchmarks`.

## What changed

`FastInvoker` is new public API in Components. It emits one `DynamicMethod` matching the member's own arity, so a member taking two arguments or fewer is invoked with its values passed directly and no array exists at any point; anything wider keeps the array form. One is cached per `ConstructorInfo` and per `MethodInfo`, reached through `constructor.GetFastInvoker()` or `method.GetFastInvoker()`, so the lookup happens once rather than per call. The IL is the shape `ReflectionExtensions` already emitted.

`ObservableMethodCallExpression`, `ObservableNewExpression` and `ObservableIndexExpression` each resolve one in `OnInitialization`, beside the `MethodInfo` or `ConstructorInfo` they already hold for the life of the node, and evaluate through a new `ObservableExpression.Invoke` which reads the operands straight into it. `EvaluationResults` survives for wider members and for the `Activator.CreateInstance` fallback.

**A cached array per node was never an option**, and not for the re-entrancy reason previously recorded. `ExpressionObserver.GetObservableExpression` returns cached nodes shared between observations, so one node serves many, and two threads propagating two different sources can evaluate it at once. A shared mutable buffer on a shared node is a data race. That is what forced the arity-specific delegates rather than a buffer.

## The measurement

Per underlying property change, over a thousand elements.

| arm | before | predicted | after |
|--- |---: |---: |---: |
| `ChangeEveryValueMethodCalled` | 128 B | **96 B** | **96 B** |
| `ChangeEveryValueMethodCalledForAPair` | 160 B | **120 B** | **120 B** |
| `ChangeEveryValueMethodCalledForATuple` | 160 B | **120 B** | **120 B** |
| `ChangeEveryValueProjected` | 160 B | **120 B** | **120 B** |
| `ChangeEveryValueProjectedOverAConstantKey` | 160 B | **120 B** | **120 B** |
| `ChangeEveryValueProjectedWithDirectSubscriptionAllowed` | 160 B | **120 B** | **120 B** |
| `ChangeEveryValueMemberRead` | 72 B | 72 B | 72 B |
| `ChangeEveryValueWithNoObservation` | 48 B | 48 B | 48 B |

**Six arms predicted to the byte and both controls unmoved.** The prediction was arithmetic on constants already on record — a one-element `object?[]` at 32 bytes, a two-element one at 40 — with the arity read off each expression rather than guessed, and it is the form which keeps working.

In time, against controls which moved 0.5% and 0.6% between the two runs:

| arm | before | after |
|--- |---: |---: |
| `ChangeEveryValueMethodCalled` | 51.040 μs | 44.815 μs |
| `ChangeEveryValueMethodCalledForAPair` | 80.954 μs | 71.698 μs |
| `ChangeEveryValueMethodCalledForATuple` | 75.593 μs | 67.170 μs |
| `ChangeEveryValueProjected` | 81.579 μs | 74.188 μs |
| `ChangeEveryValueProjectedWithDirectSubscriptionAllowed` | 83.390 μs | 71.799 μs |

Between 9% and 14%, on a pair of runs whose controls agree to well under 1%. No time figure was predicted, and the reason was written down beforehand: the lookup alone was worth 3.4 to 4.7 ns against a drift floor of 8.6%, so time could not be called in advance. It resolved because the array went with it.

## The model, which closes and keeps its one hole

For a method call returning an `int`, the recorded model was 48 floor + 24 boxed operand + 32 argument array + 24 boxed result = 128, and it measured 128. Remove the array and the model says 96. It measures 96.

For a two-field struct result the model was 48 + 24 + 40 + 24 = 136 against a measured 160, leaving **24 bytes unexplained** — the one open figure on that path. Remove the array and the model says 96 against a measured 120. **The residue is still exactly 24.** It did not move with the array, which is what an independent term should do, and that corroborates it as real rather than as slack in the arithmetic.

## One arm went the wrong way

`ChangeEveryValueProjectedOverAConstantKey` read **90.395 μs against 81.386 before — up 11.1%** — while its structural twin `ChangeEveryValueProjected` went down 9.1%. The two differ only in the first constructor argument, a constant `0` against `pair.Key`; their allocation is identical, moved identically, and both are 120 B.

Across the three runs it has taken today it reads 77.039, 81.386, 90.395 μs, climbing each time, on byte-identical allocation throughout. That is more than the calibrated drift and more than the two controls in this run did by an order of magnitude.

A shared path regression is ruled out: the twin arm uses the same constructor, the same arity and the same code, and improved. Whatever this is, it is specific to the constant-key shape, and nothing here explains it. **Flagged, not explained, and deliberately not re-run** — it has been drifting upward across runs that bracket two unrelated changes, so one more run of the same suite would add a fourth point to a series rather than a cause. It wants an instrument of its own, or it wants leaving alone.

## Where the projected observation has got to today

| | bytes per evaluation |
|--- |---: |
| start of day | 256 |
| after the boxing round trip was removed | 160 |
| after the argument array was removed | **120** |
