# Benchmark results, 6 September 2026 — what the refusals cost

`2026-09-06-the-shapes-that-qualify.md` found that eleven of the thirteen shapes this project passes to queries take the fast path, and that both refusals contain `&&`. It also put the two refused shapes at 4.49 MB and 6.45 MB against a 2.62 MB graph baseline, and concluded from that that `&&` was the largest thing on the board.

That conclusion was wrong, and this document is why. The 6.45 MB shape is `person.Rank % 2 == 0 && person.Name.Length > 1`, whose right operand reads a property through a property — refused on its own account, with nothing to do with `&&`. The two causes were never separated, and pricing a design against their sum would have priced it against a number that was not available.

`RefusalCauseBenchmarks` separates them. Ten arms, five pairs, one run. Three pairs are controls reproducing figures already on record.

This is not a release baseline. Nothing in the libraries changed to produce it.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## The arms

Constructing and disposing a filtered query over a thousand elements.

| Shape | Expression | Direct | Graph |
|--- |--- |---: |---: |
| `RankComparison` | `person.Rank > 0` | **1.17 MB** | 2.62 MB |
| `PropertyChain` | `person.Name.Length > 1` | 3.25 MB | 3.25 MB |
| `ShortCircuitOverOneProperty` | `person.Rank > 0 && person.Rank < 100` | 4.49 MB | 4.49 MB |
| `ShortCircuitOverTwoMembers` | `person.Rank > 0 && person.Name != null` | 5.05 MB | 5.05 MB |
| `DeferredBranchOverAPropertyChain` | `person.Rank % 2 == 0 && person.Name.Length > 1` | 6.45 MB | 6.45 MB |

Four of the five pairs are identical to the byte on both sides. Only `RankComparison` separates, and it is the only one of the five the analyzer accepts.

## The controls

`RankComparison` at 1.17 / 2.62 MB and `ShortCircuitOverOneProperty` at 4.49 MB reproduce yesterday's figures exactly. `DeferredBranchOverAPropertyChain` came in at 6.45 MB against a recorded 6.67; the gap is dated, not noise — the recorded figure is from `OperatorEligibilityBenchmarks` before the boxing round trip was removed, and `RankComparisonGraph` moved 2.69 → 2.62 over the same interval, in the same direction by a similar proportion.

## The property chain is refused by itself

`person.Name.Length > 1` costs **3.25 MB on both sides**, with no short-circuiting operator anywhere in it. Reading a property through a property is refused entirely on its own account.

So admitting `&&` would not have moved the 6.45 MB shape by one byte. Its right operand would still be refused. What admitting `&&` actually buys, in the measured corpus, is `ShortCircuitOverOneProperty` — 4.49 MB, one shape.

This is the first time the property-chain refusal has been priced. It is one of the four refused shapes the orientation has listed since the fast path was written, and it was the only one never measured. It had no eligibility arm.

## The arithmetic, which closes

The previous run left 0.59 MB unaccounted, and the named suspect was node sharing: `ShortCircuitOverOneProperty` reads `person.Rank` in *both* operands, so the graph's expression cache serves one member node to both, while `DeferredBranchOverAPropertyChain` reads `Rank` and `Name` and shares nothing. `ShortCircuitOverTwoMembers` was added to price exactly that.

**A shared member node is worth 0.56 MB**: 5.05 against 4.49, the same shape with the sharing removed.

The account for the combination then reads:

| term | source | MB |
|--- |--- |---: |
| `person.Rank > 0 && person.Name != null` | measured | 5.05 |
| one arithmetic node, for `% 2` | `GroupKeySelector` 2.71 − `OrderKeySelector` 1.97 | +0.74 |
| one member hop, for `.Length` | `PropertyChain` 3.25 − `RankComparison` 2.62 | +0.63 |
| **predicted** | | **6.42** |
| **measured** | | **6.45** |

Three hundredths of a megabyte over four terms each rounded to two decimals. That closes.

## A constant worth keeping

Every node in these expressions costs the graph between **0.56 and 0.74 MB per thousand elements** — 560 to 740 bytes per element per node — for construction and disposal of a filtered query. A bare member read is 1.97 MB, a comparison against a constant adds 0.65, an arithmetic operation 0.74, a member hop 0.63, a shared member node 0.56.

The fast path has no such term. Every eligible shape measured so far lands between 1.17 and 1.30 MB whatever its node count, because it builds one delegate and a handful of attachments rather than a tree.

## What this reprices

Yesterday's board put `&&` at the top on the strength of two shapes totalling 11 MB. One of those shapes was somebody else's.

| refusal | cost | fast path, predicted | difficulty |
|--- |---: |---: |--- |
| `&&` | 5.05 MB | ~1.3 MB | attach-on-first-reach, to mirror the graph's latch |
| property chain | 3.25 MB | ~1.2 MB | depends on whether the intermediate can notify |

`&&` saves more per occurrence. The property chain is the more ordinary thing to write — `person.Team.Rank`, `order.Customer.Name`, `person.Name.Length` — though how ordinary is exactly the thing this repository cannot observe, and the census limit still stands.

The difficulty column is where they part. The `&&` refusal needs the fast path to attach a deferred site the first time an evaluation reaches it, which two tests committed today show is precisely what the graph does: `person.Rank > 0 && other.Rank > 0` attaches once before the left is true and twice after, and stays at two when the left goes false again. A latch, not a re-decision.

The property chain splits into two populations which have not been separated:

- **an intermediate whose type cannot notify** — `person.Name.Length`, where `Name` is a `string`. Nothing can be subscribed to the intermediate because a `string` raises nothing, so subscribing to `person.PropertyChanged` for `Name` is the whole of it. If the graph attaches exactly one thing here, the analyzer could accept this shape with no change to the graph at all.
- **an intermediate which does notify** — `person.Team.Rank`. Here the subscription target genuinely changes as the intermediate is replaced, which is what `ChangeableMemberTarget` names and what a plan fixed at construction cannot follow.

Two tests were committed today to settle which of those the graph does, before any design rests on it: one asserting the graph attaches nothing beyond the object for `subject.Tag!.Length`, and one asserting it attaches to both links of `s.Next!.Rank`. If the first passes, the non-notifying case is a plan-time decision on the static type of the intermediate, the agreement harness can prove it, and Daniel's model is untouched. That would make it a smaller change than `&&` for two thirds of the saving, and it goes first.

## A note on method

The previous run's residue was 0.59 MB and was flagged rather than explained away. One arm priced it at 0.56 and the account closed. That is the third time this month that refusing to move on from arithmetic which did not add up has produced the missing term rather than a story about it.

The error being corrected here is subtler than a wrong prediction: two causes were measured together, their sum was read as one cause's price, and a design was about to be sized against it. **A refused shape is refused for a reason, and a shape can be refused twice.** Before pricing the removal of one refusal, check that nothing else in the same expression is also refused — an arm holding the other cause constant is one pair and settles it.
