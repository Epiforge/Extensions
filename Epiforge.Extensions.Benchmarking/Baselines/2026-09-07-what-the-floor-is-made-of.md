# What the floor is made of

*2026-09-07 — `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## Why the numbers changed shape

Two control arms were added — `SourceQueryOnly`, which wraps the source collection and disposes it without observing any predicate, and `ConstantPredicateDirect`, which is `person => true`, an observation per element that subscribes to nothing. The first allocates 4.3 KB, and because BenchmarkDotNet picks a column's unit from its smallest value, **the whole table dropped from megabytes to kilobytes and from microseconds to nanoseconds**. Every figure in this class is now readable to about a tenth of a byte per element, which is the resolution upgrade that had been contemplated as a config change and arrived free.

## The decomposition

Per element, taking the 4.3 KB wrapper out first.

| what | figure |
|---|---|
| the collection wrapper, whole query | **4.3 KB**, fixed, 394.6 ns |
| an observation which subscribes to nothing | **815.9 B** per element |
| a subscription to an object distinct per element | **376.0 B** |
| a subscription to an object already registered | **64.2 B** |
| an extra slot in the fixed-value array | **8.0 B** |
| a deferred group | **79.9 B** |
| the chain's second source and its link state | **440.0 B** |

Read off as: `ConstantPredicateDirect` 801.05 KB; `RankComparisonDirect` 1168.24; `TwoSourceSumDirect` 1262.23; `IndexerReadDirect` 1325.00; `SharedSourceRepeatedDirect` 1270.04; `TwoObjectShortCircuitDirect` 1340.26; `NotifyingChainDirect` 1597.93.

## What holds exactly

**The 64-byte marginal subscription is confirmed twice.** `TwoSourceSumDirect` over `RankComparisonDirect` is 96.2 bytes, which is one subscription plus a 32-byte fixed-value array; `IndexerReadDirect` over the same floor is 160.5, which is two plus that array. Both close on 64.

**The 8 bytes predicted and unmeasurable last night are 8.0.** `SharedSourceRepeatedDirect` over `TwoSourceSumDirect` is 7.81 KB, the second slot its repeated source occupies in the fixed-value array. At megabyte resolution this sat inside one printing step and the prediction had to be given as a pair of digits; it is now a single number.

**Two pairs are identical to the byte.** `ConditionalDirect` equals `RankComparisonDirect` at 1168.24, and `TwoObjectConditionalDirect` equals `TwoObjectShortCircuitDirect` at 1340.26.

**The chain closes.** `NotifyingChainDirect` is 440.0 over the floor, which is one distinct source at 376 plus 64 for the link state — though the linking observation's four extra fields and its three single-element arrays would predict nearer 128 than 64, so that split is not settled.

## The correction

A constant recorded a few hours ago said a subscription costs 62 bytes per element. That figure was solved from a pair whose second subscription was to `other`, one object shared by all thousand observations. **It is the marginal cost of an extra subscription to an object already registered, not the cost of a subscription.** A subscription to an object which is distinct per element costs 376 — six times as much — because it also creates a `DirectSubscriptionSource`, its lock, its event registration and its dictionary entry. The corrected pair of constants is above.

## What this opens

**Nearly 69% of the fast path's construction cost has nothing to do with subscriptions.** An observation that subscribes to nothing at all still costs 815.9 bytes per element against a floor of 1191.9. The wrapper is not it, at 0.37% of the total. That number is now the largest unexplained figure on this path and has no account at all.

**The 376 bytes are the largest figure that does have one.** The graph's equivalent — a source subscription on `SourceNotificationRegistry` — was brought from 230 bytes to 160 on 7 September by two changes: dropping a redundant per-source lock, and letting the first attachment double as the owner. `DirectSubscriptionRegistry` has had neither. `DirectSubscriptionSource` still allocates its own `Lock` and takes it inside `Attach` and `Detach`, both of which the registry already calls while holding `sourcesAccess`, and it still allocates a separate `DirectSubscriptionAttachment` for its first subscriber.

The arms make a ladder for testing that: `ConstantPredicateDirect` has no distinct source per element, `RankComparisonDirect` has one, `NotifyingChainDirect` has two, and every remaining direct arm has one. Anything charged per distinct source must move in that 0, 1, 2 ratio, whatever its size turns out to be.
