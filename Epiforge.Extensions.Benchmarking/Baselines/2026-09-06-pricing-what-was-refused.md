# Benchmark results, 6 September 2026 — pricing what was refused

Four refusals had no arm anywhere in `Baselines\`, so none could be ranked, and the standing rule forbids asserting a prize without one. Two other questions were open for the same reason. This is one window's worth of instruments, run together.

Nothing in the libraries changed to produce any of it.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## What the refusals cost

`RefusalPricingBenchmarks`, constructing and disposing a filtered query over a thousand elements. Every pair is refused — direct and graph agree — and `RankComparison` reproduced 1.17 and 2.62 MB exactly despite a different element type.

| shape | direct | graph |
|--- |---: |---: |
| `person.Rank > 0` | **1.17 MB** | 2.62 MB |
| `person.Partner!.Rank > 0` (notifying chain) | 3.39 MB | 3.39 MB |
| `person.Rank > 0 && other.Rank > 0` (two objects) | 3.95 MB | 3.95 MB |
| `(person.Rank > 0 ? person.Rank : person.Rank) > 0` | 5.05 MB | 5.06 MB |
| `table[person.Rank] > 0` (indexer read) | **15.24 MB** | 15.24 MB |

**The indexer read is the largest refusal in the library by a factor of three**, at 5.8x the graph's own baseline and 13x what the fast path costs a shape it accepts. It sat second from the bottom of the open list as a one-line note, ranked there by nothing but my impression.

That is the whole point of building the arms. The order I would have worked in was wrong, and no amount of reading would have corrected it — `2026-09-06-what-the-refusals-cost.md` made the same error in the same week and had to be corrected by measurement then too.

The remaining order is `?:` at 5.05, the two-object short circuit at 3.95, the notifying chain at 3.39. None of them is close to the indexer.

## The struct residue, which survives and is now bounded

`StructResultBenchmarks`, per property change.

| result | arguments | measured | model |
|--- |---: |---: |---: |
| `int` from `CompareTo` | 1 | **96 B** | 48 + 24 operand + 24 unexplained |
| `long` from `Convert.ToInt64` | 1 | **120 B** | 48 + 24 + 24 result + 24 unexplained |
| one-field struct | 1 | **120 B** | as above |
| two-field struct | 2 | **120 B** | as above |
| three-field struct | 3 | **176 B** | 48 + 24 + 48 array + 32 result + 24 unexplained |
| nothing observed | — | **48 B** | control |

The experiment was designed to separate struct-ness from payload size, and it did neither, because the framing was wrong. **`long` costs the same as a struct.** What is actually anomalous is the `int` arm, and the reason is in the predicate rather than the type: `Rank.CompareTo(0)` returns 1 for every element but one, and the evaluation setter does not re-box a result which has not changed. So that arm skips the result box on almost every evaluation, and its 96 is 120 minus a box it never allocates.

With that understood the whole table closes. The three-field arm is the confirmation: 48 floor, 24 for the boxed operand, 48 for the `object?[3]` which reappears exactly where `FastInvoker` stops taking arguments directly, 32 for a twelve-byte payload boxed, and the same 24 — **176 measured, 176 modelled**.

**So the unexplained 24 bytes is constant across result type, result size and arity.** It is not struct-ness, not payload, not the argument array, and not the result box. That is a much tighter description than "a two-field struct costs 24 more than the model", and it is what the next attempt should start from.

## The standing prediction

`ChangeEveryValueInASelectQuery` read **120,000 B — 120 per change — against a prediction of 120**, made when the projection observation fell from 160 to 120 and recorded to be checked whenever this suite next ran for any reason. It ran for another reason and the check was free.

Both identities hold to the byte: a filtered dictionary query costs 96 B per change and its observation alone costs 96; a projected one costs 120 and its observation costs 120. A query still costs exactly what observing its own predicate or selector costs, and nothing more.

## One arm to watch

`ReplaceOneKeyObservedByAnIndexer` read **360 bytes per replace against 352 on record**. Two changes since that figure should have pushed it down rather than up — the boxing round trip, and the argument array which `ObservableIndexExpression` no longer builds for a single-argument indexer.

Eight bytes, one arm, no allocation change anywhere near it to explain the direction. Flagged and not chased: today has already produced one three-point trend that turned out to be an excursion, and this is a single point.
