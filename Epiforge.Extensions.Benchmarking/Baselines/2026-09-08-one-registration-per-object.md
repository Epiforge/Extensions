# One registration per object

*2026-09-08 — `PropertyKeyingBenchmarks` and `RefusalPricingBenchmarks`, DefaultJob, MemoryDiagnoser, 1000 elements*

## What changed

`DirectSubscriptionRegistry` keyed its sources by `(object, kind, propertyName)`, so an expression reading two different properties of one object registered two handlers with that object's `PropertyChanged`, each with a source object and a dictionary entry of its own. The expression graph has always registered once per object and event and decided relevance per node. The fast path now does the same: the registry keys by `(object, registration)` where a registration is one of `CollectionChanged`, `DictionaryChanged` or `PropertyChanged`, and the kind and property name moved onto the attachment, which is asked whether it wants a reported name before anything is evaluated.

The walk advances to the first attachment which wants the name before entering a `PropagationScope`, so a change to a property nobody named still begins no propagation — which is what a handler registered per property did by returning. The conventional indexed form of a name is compared without being composed, so a change to any other property of an indexed source allocates nothing.

## Both sides, priced

| arm | before | after |
|---|---|---|
| `OnePropertyConstructDirect` | 296.092 μs / 973.00 KB | 289.540 μs / **965.19 KB** |
| `TwoPropertiesConstructDirect` | 411.540 μs / 1324.56 KB | 338.167 μs / **1035.50 KB** |
| `ChangeEveryRankWithTwoPropertiesObservedDirect` | 21.822 μs / 46.88 KB | **18.599 μs** / 46.88 KB |
| `ChangeEveryRankUnobserved` (control) | 9.171 μs / 46.88 KB | 9.274 μs / 46.88 KB |
| `TwoPropertiesConstructGraph` (control) | 6,643.971 μs / 5844.66 KB | 6,841.217 μs / 5844.67 KB |
| `ChangeEveryRankWithTwoPropertiesObservedGraph` (control) | 40.439 μs / 70.31 KB | 42.648 μs / 70.31 KB |

**The second property of one object cost 360 bytes per element and now costs 72.** The gap between the one-property and two-property arms is 70.31 KB per thousand, which is 72.0 bytes per element to the tenth — one attachment, and nothing else. Both graph controls held their allocation exactly and the unobserved control held its.

**The feared time cost is a time saving.** A handler waking on a property it does not want was supposed to be the price of this. It is not: the two-property propagation arm went from 21.822 μs to 18.599 μs, **14.8% faster**. Two handlers were being invoked through a multicast list on every change, one of them returning immediately; now one is invoked and walks two attachments. Fewer delegate invocations beat one extra name comparison. The graph arms moved 3% to 5.5% in time with their allocation identical to the byte, which is machine drift and not this change.

## The prediction, half of which was wrong

Recorded before the run: the gap collapses to one attachment, 56 to 72 bytes; the two-property arm falls 288 to 304; the one-property arm **rises about 8**, because every attachment grew by a reference to its property name; the propagation arm moves less than 10% either way.

The first two were right — 72.0 and 296.0. **The third was inverted.** The one-property arm did not rise 8 bytes per element; it *fell* 8.0. The magnitude was right and the sign was wrong, because the prediction counted the attachment growing by a reference and forgot that the source shed three fields at the same time: its property name, its precomputed indexed form, and its kind, replaced by a registration in the same four bytes. That is 16 bytes off every source against 8 onto every attachment.

**The fourth was wrong in the same direction as the third** — a wash was predicted and a 14.8% improvement arrived. Two misses out of four, both of them favourable, which is not a defense: a model that cannot get the sign right is not measuring, it is guessing with arithmetic attached.

## The rule the whole refusal table obeys

`RefusalPricingBenchmarks` contains no arm which reads two properties of one object, so it prices only the cost side. Every arm on it moves by exactly **−16 bytes per element per source object distinct to the element, +8 bytes per element per attachment**:

| arm | before | after | per element |
|---|---|---|---|
| `RankComparisonDirect` | 973.00 KB | 965.19 KB | **−8.0** |
| `ConditionalDirect` | 973.00 | 965.19 | **−8.0** |
| `NotifyingChainDirect` | 1355.81 | 1340.19 | **−16.0** |
| `TwoSourceSumDirect` | 1066.94 | 1066.92 | 0 |
| `NonShortCircuitDirect` | 1066.94 | 1066.92 | 0 |
| `SharedSourceRepeatedDirect` | 1074.75 | 1074.73 | 0 |
| `TwoObjectConditionalDirect` | 1144.97 | 1144.95 | 0 |
| `TwoObjectShortCircuitDirect` | 1144.97 | 1144.95 | 0 |
| `IndexerReadDirect` | 1129.66 | 1137.41 | **+7.9** |
| `ConstantPredicateDirect` | 652.69 | 652.69 | 0 |
| `SourceQueryOnly` | 4.30 | 4.30 | 0 |

One subscription to an object of its own is −8. Two such objects is −16. One object of its own plus a source shared by every element is 0, the shrinking source paying for both attachments. **A subscription to a shared object with no per-element source of its own is the only case which costs anything: +7.9 bytes per element**, which is `IndexerReadDirect`, and it is the price of the whole change.

The arithmetic closes on the two-property arm as well. Its second property was a source, a delegate, a registration on an already-registered object, a dictionary entry and an attachment; it is now an attachment. That is −288, and the first property's own source shed 16 against its attachment's 8, for −296 against a measured 296.0.

**The seven graph arms could not be checked**, because the baseline which recorded this instrument's last run listed only its direct arms. Record every arm of a table next time, controls included, or there is no before for them.

## Where the floor is now

| term | at the start of 7 September | before this | now |
|---|---|---|---|
| the observation itself | 424.0 B | 400.0 B | 400.0 B |
| the `Where` query's per-element share | 391.9 B | 263.9 B | 263.9 B |
| one subscription to an object distinct per element | 376.0 B | 328.0 B | **320.0 B** |
| **the floor** | **1191.9 B** | **991.9 B** | **983.9 B** |

**17.5% off the cost of building a filtered query over a thousand elements since 7 September.**
