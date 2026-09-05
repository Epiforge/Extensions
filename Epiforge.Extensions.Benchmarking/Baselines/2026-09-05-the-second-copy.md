# 5 September 2026 — the second copy

`2026-09-04-the-observed-indexer.md` closed with 456 bytes per dictionary change unexplained on the boxed notification path and an account that reached only about 240 of them. This pass removes the copy that was the leading suspect, and the design was chosen by a test rather than by argument.

## The before

From the after run of the observed-indexer pass, all six arms of `DictionaryPropagationBenchmarks` in one process.

| arm | allocated | per operation | mean |
| --- | ---: | ---: | ---: |
| `ReplaceOneKeyWithNoObservation` | 0 B | **0** | 4.752 μs |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | 0 B | **0** | 5.108 μs |
| `ReplaceOneKeyObservedByATypedSubscriber` | 104,000 B | **104 B** | 16.342 μs |
| `ReplaceOneKeyObservedByABoxedSubscriber` | 560,000 B | **560 B** | 54.296 μs |
| `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` | 560,000 B | **560 B** | 55.183 μs |
| `ReplaceOneKeyObservedByAnIndexer` | 560,000 B | **560 B** | 80.471 μs |

## What a test settled, and what it did not

The design turned on one fact: whether `NewItems` and `OldItems` currently hand a consumer something it can write through. If they already expose a bare array, the arguments object could simply take ownership of an array built for it and nothing would be lost. If they do not, that route quietly weakens a guarantee the type has been shipping.

Rather than decompile the lowering once, `ItemsOfManyAreNotWritableByTheConsumer` and `ItemsOfOneAreNotWritableByTheConsumer` assert the property behaviorally, so the answer survives a compiler upgrade. **They pass**, which settles it: the items are not a bare array, and taking ownership of one is off the table.

**The first version of that helper could have passed without asserting anything.** It only checked writability if the items implement `IList<KeyValuePair<TKey, TValue>>`, and a read-only wrapper which implements only `IReadOnlyList<T>` would have skipped the check entirely. The inference above survives that — a bare array *does* implement `IList<T>`, so a bare array could not have produced a pass either way — but a test which can pass vacuously is a poor instrument, and the helper now also asserts directly that the items are not an array.

## The change, and why not the faster one

`Box` now returns a `ReadOnlyCollection<KeyValuePair<object?, object?>>` wrapping the exact-size array it just filled, and a private constructor takes those lists as they are instead of copying them. Its `<summary>` says what it requires: only a caller that built the lists for that instance may use it.

The alternative was `ImmutableCollectionsMarshal.AsImmutableArray`, which wraps an array with no copy at all. It was rejected on cost, not on taste: **both routes cost one small object per side** — a `ReadOnlyCollection<T>` is a header and one reference, a boxed `ImmutableArray<T>` is a header and one reference — and the marshal is .NET 8 or later, so it would need a guarded fallback for `net6.0` and `net7.0`. The first entry under "habits that cost sessions real time" is a conditional-compilation guard that read as innocent in a search and was not. Identical allocation for a guard and a framework dependency is a bad trade.

## The prediction, revised before the run and not after

The earlier baseline predicted this change would take the boxed arm to **roughly 150 to 200 bytes**. That prediction is withdrawn and replaced here, before any measurement, because it omitted two things which the design cannot avoid.

What should remain per replacement once the second copy is gone:

| | |
| --- | ---: |
| the typed arguments every subscriber pays for | 104 B |
| the boxed arguments object | ~24 B |
| two exact-size arrays, one per side | ~80 B |
| two `ReadOnlyCollection` wrappers, one per side | ~48 B |
| two boxed `int` keys | 48 B |
| **predicted total** | **~304 B** |

The 150-to-200 figure counted neither the key boxes nor the wrapper the guarantee requires. Revising a prediction before the run is fair; revising it after the number arrives is not, which is why this is written down now.

So:

- **The boxed arm lands near 300.** The hypothesis is confirmed: the constructor's `[..items]` over an `IEnumerable<T>` parameter was re-enumerating and re-growing an array it had been handed at exactly the right size, and that second copy was worth roughly 256 bytes, about 128 per side.
- **It lands near 450.** The hypothesis is refuted, the second copy was cheap, and the residue is somewhere neither reading has looked. This is the more interesting outcome and the reason to run it rather than assume.
- **The typed arm stays at 104 B.** Nothing on that path changed. If it moves, something was changed that was not understood.
- **The indexer arm tracks the boxed arm exactly**, as it has since its own handler stopped allocating. Any gap between them is a new finding.
- The control and the property-changed arm stay at **0 B**.

Time is not predicted, and no time figure here may be differenced against another arm.

## The after

| arm | before | after | change |
| --- | ---: | ---: | ---: |
| `ReplaceOneKeyWithNoObservation` | 0 B | **0 B** | — |
| `ReplaceOneKeyObservedByAPropertyChangedSubscriber` | 0 B | **0 B** | — |
| `ReplaceOneKeyObservedByATypedSubscriber` | 104 B | **104 B** | — |
| `ReplaceOneKeyObservedByABoxedSubscriber` | 560 B | **320 B** | −240 |
| `ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers` | 560 B | **320 B** | −240 |
| `ReplaceOneKeyObservedByAnIndexer` | 560 B | **320 B** | −240 |

Means: the indexer arm 80.471 → 60.522 μs, the boxed arm 54.296 → 35.207 μs. The four arms whose allocation is unchanged moved −0.1%, +0.5%, −0.6% and −1.3%, so drift for this pair of runs is again inside ±1.5% and neither fall is in it.

## The prediction, judged

**304 predicted, 320 measured.** The first prediction this arc that landed, and it landed because it was built the way the method says predictions have to be built — a count of objects each of a known unit cost, not a story about which code is hot.

The hypothesis is confirmed. The second copy was worth **240 bytes, about 120 per side**, against 128 per side predicted: `[..items]` over a parameter typed `IEnumerable<T>` was taking an enumerator through the interface and growing a buffer to hold an array it had been handed at exactly the right length. **The trap is now measured rather than reasoned, and belongs in the orientation as a fact.**

**The account now closes.** The boxed adaptation costs 320 − 104 = **216 bytes**, against 200 nameable: an arguments object, two exact-size arrays, two `ReadOnlyCollection` wrappers, two boxed `int` keys. Sixteen bytes outstanding is one small object's worth of estimate error across six of them, not a missing mechanism. The 456-byte hole opened in `2026-09-04-the-observed-indexer.md` is closed.

The observed indexer has gone **808 → 560 → 320 bytes** per dictionary change across the two passes, and **152 → 80 → 61 μs**. It costs exactly what any boxed subscriber to the same dictionary costs, and has since its own handler stopped allocating.

## One arm moved which should not have

`ChangeEveryValueInAWhereQueryWithASubscriber` went from 137.746 μs to 146.813 μs, **+6.6%**, on byte-identical allocation of 256,000 B. It read 138.663 in the run before that, so the two earlier runs agree and this one does not.

**No mechanism connects it to this change.** That arm has no boxed subscriber, so `ToBoxed` is never called on its path; `Box` and the private constructor are reachable from nothing else; and the typed construction path was not touched, which the unmoved 104-byte arm independently confirms. Every other arm in the run sits inside ±1.5%.

This is the shape the baselines already record for between-run variance — on 2 September an arm executing no changed code produced an 8.9% single-launch excursion which turned out to be a tail sample of a much wider distribution than its within-run deviation suggested. That is the likeliest explanation and it is **not** an established one. It is recorded here so that the next run of this suite is checked against it rather than the excursion being forgotten. If it reads near 138 again, this was a tail sample. If it stays near 147, something real happened that this pass does not explain.

## The arm which moved, settled

`DictionaryPropagationBenchmarks` was run a fourth time, with nothing changed, for the sole purpose of reading that one arm again.

| run | `ChangeEveryValueInAWhereQueryWithASubscriber` |
| --- | ---: |
| before the observed-indexer pass | 138.663 μs |
| after it | 137.746 μs |
| after the second-copy pass | **146.813 μs** |
| unchanged, re-run | **137.835 μs** |

It came back. The excursion was a tail sample, the arm is not regressed, and nothing about the second copy reached it — which is what the byte-identical allocation said at the time and what the absence of any mechanism said before that.

Every other figure reproduced. All six `ReplaceOneKey` arms and all six `ChangeEveryValue` arms allocate exactly what they allocated in the run before, and the means moved by less than 2% everywhere except the arm this run was for.

**What is worth keeping is not the reassurance but the number.** `ChangeEveryValueInAWhereQueryWithASubscriber` has the widest between-run variance in this suite — four readings of 138.7, 137.7, 146.8 and 137.8 for code that was identical in three of them — while its within-run standard deviation never exceeded 1.1 μs, under 0.8%. That is a **six-to-one gap between what the instrument reports about its own precision and how far it actually moves between runs**, on the same arm, and it is the sharpest example of the rule the baselines already carry. A single-run time movement on that arm decides nothing.

The cost of learning that was one benchmark run of a suite that takes about ninety seconds, which is the right price for not carrying an unexplained 6.6% regression forward in the record.
