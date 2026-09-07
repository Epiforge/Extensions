# The indexer that was refused outright

*2026-09-07 — `IndexerPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What was measured

Every C# indexer read was refused by the analyzer as `UnsupportedExpressionKind`, because the C# compiler emits `p[i]` as a call to the indexer's get method and the analyzer's method-call arm did not recognise that shape. `ExpressionObserver.GetObservableExpression` had always rewritten that call into the index access it stands for; the analyzer simply did not. Routing the call through the same rewrite lets `AnalyzeIndex` — which already existed and already admitted `IndexExpression` — plan the contents and `IndexerPropertyChanged` subscriptions.

Indexers whose target notifies as a dictionary are refused as `DictionaryIndexTarget`. See "What the refusal is for" below.

## The readings

| arm | time | allocated |
|---|---|---|
| `DictionaryReadDirect` | 10,830.1 μs | 15.24 MB |
| `DictionaryReadGraph` | 10,640.5 μs | 15.24 MB |
| `ObservableListReadDirect` | **505.2 μs** | **1.35 MB** |
| `ObservableListReadGraph` | 10,641.6 μs | 15.24 MB |
| `PlainListReadDirect` | **389.8 μs** | **1.22 MB** |
| `PlainListReadGraph` | 4,505.2 μs | 4.39 MB |
| `RankComparisonDirect` | 361.6 μs | 1.18 MB |
| `RankComparisonGraph` (baseline) | 2,352.1 μs | 2.62 MB |

## What they say

**A notifying-list indexer went from 15.24 MB to 1.35 — 11.3x less memory and 21x less time.** Against the fast path's own floor of 1.18 MB, the indexer costs 0.17 MB; against the graph's own floor of 2.62 MB, it costs 12.62. That ratio — 0.17 against 12.62, a factor of 74 — is the widest gap between the two mechanisms recorded so far for a single node.

**A plain-`List<int>` indexer went from 4.39 MB to 1.22 — 3.6x less memory, 11.6x less time.** It costs the fast path 0.04 MB over its floor. It is cheaper on the graph than the notifying list because a `List<int>` raises nothing, so the graph subscribes to nothing; the whole 3.17 MB saved is node structure, not subscriptions.

**The graph charges the same 15.24 MB for a dictionary read and a notifying-list read**, to four significant figures and within 1.1 μs of the same time. The cost is the index node over a 1000-element notifying source; the collection's kind does not enter into it.

**The dictionary pair allocates identically on both arms**, which is the instrument for eligibility: where a `Direct` and `Graph` arm allocate the same, the fast path declined and both arms measure the graph. The refusal is in force.

## What the refusal is for

`ObservableDictionary.TryRemove` raises `Item[]` and then `DictionaryChanged`. The graph notifies twice for one removal, and the two notifications carry different faults:

```
graph        The given key '5' was not present in the dictionary. | Key '5' was removed
fast path    The given key '5' was not present in the dictionary.
```

The first is the dictionary's own message, from re-reading. The second the graph builds from the `DictionaryChanged` payload — it reports that the key *was removed*, which the getter cannot say. `ScopedObservableExpression.FaultEquals` compares faults by type and message, so a changed message is a change worth announcing, and the graph announces it.

A direct observation has no payload. It re-reads, gets the same fault it already reported, and stays silent. Producing the graph's second notification would require pairing the event payload with knowledge of which key this expression indexes — the per-node machinery the fast path exists not to have. So dictionary-targeted indexers are refused rather than approximated.

This divergence was not introduced by admitting the C# form. `AnalyzeIndex` has always accepted `IndexExpression`, so a dictionary index built directly — as the Query internals build them — has been taking the fast path and losing the second notification since direct subscription shipped. Admitting the C# form is what made it reachable from ordinary source, and what made a test catch it.

The refusal is decided on the target's **static** type. A value whose static type is not `INotifyDictionaryChanged` but whose runtime type is would still slip through. Every shape in the corpus is exact; the edge is open.

## What guards it

- `DictionaryIndexerFaults.ARemovedKeyIsReportedTheSameWayByBothMechanisms` — runs one removal through both mechanisms and compares the fault messages they report, in order. Both now run the graph, so it passes; if eligibility is ever widened back over dictionaries it fails and prints both sequences.
- `DirectSubscriptionPlanning.IndexOnATargetWhichNotifiesOfDictionaryChangesIsIneligible` — asserts the refusal and its reason.
- `DirectSubscriptionAnalyzer.PropertyGetMethodCallIsEligible` — asserts the C# form is admitted, and that the body really is a `MethodCallExpression`, so it fails if the rewrite stops being the path taken.
- `SubscriptionAgreement.ClosureFieldHoldingSomethingIndexed` — plan-versus-graph subscription equality for an indexed read through a closure field, against `RecordedList`.
- `DifferentialFuzz` — all 450 programs reshaped by a new integer leaf that indexes a field-held list and a new mutation that writes to it.
- `ObservableIndexExpression.DictionaryChanges` and `ObjectValueChangesNotifiedByTheIndexerConvention` — both standing, both green.

## Also fixed on the way

`DirectSubscriptionSource` compared an `IndexerPropertyChanged` subscription against its stored property name alone, so a collection notifying only by the `"Item[]"` convention was invisible to the fast path. The graph accepts `Item` or `Item[]`; the fast path now does too, building the conventional name once per subscription source rather than per event. That hole was unreachable before, because no C# indexer ever got far enough to plan such a subscription.
