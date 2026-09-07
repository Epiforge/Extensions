# The indexer that was refused outright

*2026-09-07 — `IndexerPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was wrong

Every C# indexer read was refused as `UnsupportedExpressionKind`. The compiler emits `p[i]` as a call to the indexer's get method, and the analyzer's method-call arm did not recognise that shape — so `AnalyzeIndex`, which already existed and already admitted `IndexExpression`, was never reached from ordinary source. `ExpressionObserver.GetObservableExpression` had always rewritten that call into the index access it stands for. The analyzer now routes through the same rewrite.

Admitting it exposed a second fault, in the graph, which had been there far longer.

## The readings

Before is the run taken while dictionary indexers were still refused; after is the same class, unchanged, after both fixes.

| arm | before | after |
|---|---|---|
| `DictionaryReadDirect` | 10,830.1 μs / 15.24 MB | **515.6 μs / 1.35 MB** |
| `DictionaryReadGraph` | 10,640.5 μs / 15.24 MB | 10,648.4 μs / 15.24 MB |
| `ObservableListReadDirect` | 505.2 μs / 1.35 MB | 515.1 μs / 1.35 MB |
| `ObservableListReadGraph` | 10,641.6 μs / 15.24 MB | 10,660.0 μs / 15.24 MB |
| `PlainListReadDirect` | 389.8 μs / 1.22 MB | 389.3 μs / 1.22 MB |
| `PlainListReadGraph` | 4,505.2 μs / 4.39 MB | 4,494.4 μs / 4.39 MB |
| `RankComparisonDirect` | 361.6 μs / 1.18 MB | 361.5 μs / 1.18 MB |
| `RankComparisonGraph` (baseline) | 2,352.1 μs / 2.62 MB | 2,358.6 μs / 2.62 MB |

Every control holds to within 0.3% except `ObservableListReadDirect` at 2.0%, and no control moves in allocation at all. The pair of runs is trustworthy.

## What they say

**A dictionary indexer read went from 15.24 MB to 1.35 — 11.3x less memory and 21x less time.** It was the most expensive shape in the library, at 5.8x the graph's own baseline.

**A notifying-list indexer read is the same 1.35 MB, to the byte and within 0.5 μs.** The two land on the same number because they are the same work: an index over a notifying source, with a contents subscription and an `Item[]` subscription. The collection's kind never mattered, on either mechanism — the graph charged 15.24 MB for both as well.

**Against its own floor, the fast path pays 0.17 MB for the indexer and the graph pays 12.62.** `RankComparisonDirect` is 1.18 MB and `RankComparisonGraph` is 2.62. A factor of 74 for one node, the widest recorded.

**A plain `List<int>` indexer went from 4.39 MB to 1.22 — 3.6x memory, 11.6x time.** It is cheaper on the graph than the notifying list because a `List<int>` raises nothing, so the graph subscribes to nothing. The whole 3.17 MB saved is node structure, not subscriptions.

## The graph fault

`ObjectValueDictionaryChanged` did not evaluate the expression. It took its answer from the `DictionaryChanged` payload:

```csharp
if (key.Equals(keyValuePair.Key))
{
    Evaluation = (null, keyValuePair.Value);   // add and replace
    return;
}
...
Evaluation = (new KeyNotFoundException($"Key '{key}' was removed"), defaultResult);   // removal
```

Both branches assume that reading the dictionary's contents is the same as evaluating the expression. That holds only if the indexer is a plain lookup, and `ObservableDictionary`'s indexer is `public virtual TValue this[TKey key]`.

The removal branch failed loudly — a fault message the getter would never produce, which by `FaultEquals` (type and message) read as a change and raised a second notification per removal. That was the whole difference between the graph's ten values and the fast path's seven in `ObservableIndexExpression.DictionaryChanges`.

The add and replace branch failed silently, and was the worse of the two. `DictionaryIndexerValues` proved it against a subclass overriding the virtual indexer to return ten times the stored value:

```
AnAddedKeyIsReportedAsTheIndexerReadsIt      Expected:<40>. Actual:<4>.
AReplacedKeyIsReportedAsTheIndexerReadsIt    Expected:<70>. Actual:<7>.
```

The graph reported 4 for an expression whose value was 40. No fault, no signal, nothing a consumer could detect.

Both branches now use the payload for the only question it can answer — is my key in this event — and call `Evaluate()` for the rest. The handler is 178 bytes shorter and has the same shape as `ObjectValueCollectionChanged`, which has always worked this way. With the graph reading through the indexer, the two mechanisms agree on values, fault messages and notification counts, which is what made the refusal unnecessary.

The tell was there before the tests: within one file, the dictionary handler was the only place that substituted payload for evaluation. Every branch of its sibling evaluates.

## Contract changes

`DictionaryChanges` goes from ten expected values to seven. A consumer counting notifications on a dictionary indexer sees one per removal instead of two. Sanctioned deliberately; the second notification only ever carried a different message for the same result and exception type.

## What guards it

- `DictionaryIndexerValues` — an observation of `p1[5]` must equal `p1[5]`, against an overridden indexer, on adds and replaces. Mechanism-independent: it compares the observation to the indexer, not to the other mechanism.
- `DictionaryIndexerFaults.ARemovedKeyIsReportedTheSameWayByBothMechanisms` — one removal through both mechanisms, comparing the fault messages in order. Written as a probe while dictionaries were refused, when it compared the graph against itself; now a real cross-mechanism guard.
- `DirectSubscriptionPlanning.IndexOnAParameterHoldingADictionaryPlansBothContentsSubscriptionsAndTheIndexer`.
- `DirectSubscriptionAnalyzer.PropertyGetMethodCallIsEligible` — asserts the body really is a `MethodCallExpression`, so it fails if the rewrite stops being the path taken.
- `SubscriptionAgreement.ClosureFieldHoldingSomethingIndexed`, against `RecordedList`.
- `DifferentialFuzz` — all 450 programs reshaped by a new integer leaf indexing a field-held list and a new mutation writing to it.
- `ObservableIndexExpression.DictionaryChanges` and `ObjectValueChangesNotifiedByTheIndexerConvention`, both standing.

## Also fixed on the way

`DirectSubscriptionSource` compared an `IndexerPropertyChanged` subscription against its stored property name alone, so a collection notifying only by the `"Item[]"` convention was invisible to the fast path. The graph accepts `Item` or `Item[]`; the fast path now does too, building the conventional name once per subscription source rather than per event. The hole was unreachable before, because no C# indexer ever got far enough to plan such a subscription.

## Left open

`ObjectValueCollectionChanged` does not fabricate values, but it decides *relevance* by index arithmetic — `Add`, `Move`, ranged `Remove`, and a three-clause `Replace`, under a `CA1502` complexity suppression. Getting that arithmetic wrong yields no notification rather than a wrong one, which is harder to find. The fast path models nothing: it evaluates on any collection change and lets equality suppression discard the no-ops. Whether the arithmetic is cheaper than evaluating is unmeasured, and whether it is correct for every `Move` and ranged `Replace` against a held index is untested — `DifferentialFuzz` does not currently generate those.
