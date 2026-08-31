# Benchmark comparison, 31 August 2026 — fields as fixed targets

`IsFixed` accepted a field only when a compiler-generated type declared it, which admitted a captured local and refused everything else. A field raises no change notification whatever declares it, so the graph reads one once and holds it — `ObservableMemberExpression` guards both its subscribe and unsubscribe paths entirely on `isFieldOfCompilerGeneratedType`, and its target is a constant, so nothing re-triggers evaluation. Both mechanisms already froze such a value. The restriction was conservative rather than necessary.

One line now admits a field on any fixed target. A second change was forced by it and is described below.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
```

Compared against `2026-08-30-queries-after-direct-subscription.md`, measured on the same machine at the same launch counts against the released 4.0.0.

## Reproducing

```
dotnet run --project Epiforge.Extensions.Benchmarking --configuration Release -- --filter *QueryFootprintBenchmarks* *QueryFanOutBenchmarks*
```

## One shape moved and four did not

`QueryFootprintBenchmarks` measures five predicate shapes at four element counts. Exactly one of them — `person => person.Rank > threshold.Rank`, where `threshold` is a field of the benchmark class — was made eligible by this change. The other four were eligible or ineligible before and remain so.

| shape | allocation before | after |
|--- |---: |---: |
| `person => true` | 8,775.88 KB | 8,775.96 KB |
| `person => person.Rank > 0` | 12,527.15 KB | 12,527.38 KB |
| `person => person.Rank % 2 == 0` | 12,528.00 KB | 12,527.88 KB |
| `person => person.Rank % 2 == 0 && person.Name.Length > 1` | 90,953.98 KB | 90,953.43 KB |
| **`person => person.Rank > threshold.Rank`** | **38,656.43 KB** | **13,310.17 KB** |

At ten thousand elements. Four shapes identical to within a kilobyte on figures up to ninety megabytes; the fifth down **2.90×**. Allocation in this suite is deterministic, which is what makes four unchanged rows a control rather than a coincidence.

The moved shape across all four sizes:

| elements | time before | after | | allocation before | after | |
|---: |---: |---: |---: |---: |---: |---: |
| 250 | 728.87 μs | **197.73 μs** | 3.69× | 978.43 KB | **348.38 KB** | 2.81× |
| 1,000 | 3,526.10 μs | **603.71 μs** | 5.84× | 3,890.14 KB | **1,356.92 KB** | 2.87× |
| 4,000 | 34,726.56 μs | **9,439.60 μs** | 3.68× | 15,161.49 KB | **5,132.29 KB** | 2.95× |
| 10,000 | 94,704.15 μs | **21,986.75 μs** | 4.31× | 38,656.43 KB | **13,310.17 KB** | 2.90× |

Its ratio against the suite's control fell from 10.49–13.92 to 2.31–3.13, which puts it level with the other eligible shapes: at ten thousand elements it is 21,987 μs against 20,943 for `ThreeNodes` and 21,352 for `FiveNodes`. It has stopped being the expensive outlier and joined the population.

## QueryFanOut: the penalty for a shared source is nearly gone

| | before | after | |
|--- |---: |---: |---: |
| `ConstructAndDisposeWithFanOut` | 2,730.38 μs / 3,890.13 KB | **583.26 μs / 1,356.91 KB** | **4.68× / 2.87×** |
| `ConstructAndDisposeWithoutFanOut` | 486.86 μs / 1,278.06 KB | 454.23 μs / 1,277.15 KB | control |
| `ChangeTheSharedValue` | 7.445 μs / 23.70 KB | 6.418 μs / 23.67 KB | 1.16× |

The fan-out arm was **5.61×** its no-fan-out control and is now **1.29×**. A thousand elements comparing against one shared threshold used to cost five and a half times what a thousand elements comparing against a literal cost; it now costs a third more.

`ChangeTheSharedValue` improved 14% on identical allocation. That arm is propagation, not construction — one shared value changing, a thousand expressions reacting — and it moved because the notification now travels the registry rather than the graph.

## What the remaining difference is made of

At ten thousand elements the moved shape allocates 13,310 KB against `ThreeNodes`'s 12,527 — 783 KB more, or about 78 bytes per element. Each observation attaches to the shared threshold as well as to its own element, and a `DirectSubscriptionAttachment` is a small class. Ten thousand of them at roughly fifty-six bytes accounts for most of the difference.

That is the correct cost of a second subscription rather than waste, and pooling attachments is a separate question with its own measurement.

## A fix this change forced, and what caught it

Admitting a field on any fixed target also admits a field on the **argument**, because a parameter was always fixed. `DirectObservableExpression.Resolve` walks a fixed chain to produce the value frozen at construction, and it had never needed to know a parameter's value, because until now every chain bottomed out at a display-class constant. It threw.

`Resolve` now takes the observation's argument and answers a parameter with it. That is correct for the same reason the rest of this change is: the graph's node for a parameter-rooted field has a constant target and no subscription, so it freezes the value too.

**The differential fuzzer did not catch this.** Its grammar had been extended, deliberately and before the rule changed, to generate field-through shapes — but only rooted at an ordinary object, because `Recorded` had no public field to root one at the argument. What caught it was `ObservableMemberExpression.FieldValue`, a unit test written long before any of this work, observing `p1 => p1.artist.Name` over a `ValueTuple` argument. Tuple elements are fields.

The lesson is not that the fuzzer is weak. It is that a generator covers the shapes its grammar knows, and a hand-written test covers a shape somebody once thought worth pinning, and the second set is not a subset of the first. `Recorded` has since gained a public field and the fuzzer generates and mutates it, including reassigning it, since reassignment is the mutation that distinguishes a frozen value from a live one.

## Next

Method-backed operators, which is what makes `person => person.Name == "Emily"` ineligible — string equality compiles to `op_Equality`. It is at least as common a predicate shape as the one this document measures, and it gets its own step and its own benchmark rather than riding along with this one.
