# 31 August 2026 — the dictionaries announce their indexer, and stop building their events twice

Two changes to `ObservableDictionary`, `ObservableSortedDictionary` and `ObservableConcurrentDictionary`, one of which was the question and the other of which was found while measuring it.

## The question: does `Item[]` cost anything?

The three dictionaries announced `Count` and nothing else. Anything bound to a particular key had no way to know it had changed, and **replacing the value under an existing key announced nothing at all** — the indexer setter raises a `Replace` and no count notification, because the count did not change.

The collection in this package was fixed the same day, but that fix could be derived from the base class it inherits. A dictionary has no base class to compare against, so this is a convention argument rather than a measured divergence, and the only thing worth measuring was the cost: an extra `OnPropertyChanged` on every mutation of three types, one of them concurrent.

`DictionaryMutationBenchmarks` was built to answer that. Ten thousand adds, removals and indexer assignments against each type, nothing subscribed. The first version of it was worthless — a single `[IterationSetup]` built all three dictionaries every iteration, thirty thousand mutations of garbage before each measurement, and `AddToSorted` came back as 1,042 μs with a standard deviation of 1,070 μs. Three targeted setups fixed it.

## What it found instead

Per single-element mutation, with **no subscriber attached at all**: 176 to 408 bytes. Dictionary growth accounts for perhaps 70 of that on an add and none on a removal.

The cause was in the event arguments, not the dictionaries. A single-item change went:

```
(action, key, value) → (action, KeyValuePair) → (action, [changedItem]) → NewItems = [..changedItems]
```

The one pair was placed in a collection, and `InitializeAdd`/`InitializeRemove` then spread that collection into a second one — through a builder, since the spread source is an `IEnumerable` whose length is not statically known. Two collections and a builder to carry one key and one value, on every add, every removal and every replacement, subscribed or not.

The two single-item constructors now assign directly. Nothing about `OnChanged` changed, so no subclass overriding it stops being called.

## What it recovered

| operation | before | after | saved |
|---|---:|---:|---:|
| add, hashed | 248.5 B | 136.7 B | **111.8 B** |
| add, sorted | 221.2 B | 112.0 B | **109.2 B** |
| add, concurrent | 313.5 B | 201.3 B | **112.2 B** |
| remove, any of the three | 176.2 B | 64.0 B | **112.2 B** |
| set, hashed | 312.5 B | 88.0 B | **224.5 B** |
| set, sorted | 312.5 B | 88.0 B | **224.5 B** |
| set, concurrent | 407.9 B | 184.0 B | **223.9 B** |

**Every redundant materialization cost 112 bytes, and the assignment path did two of them** — one for the new item and one for the old — which is why its saving is exactly twice. Seven measurements agree on the constant to within three bytes. That is the mechanism confirmed rather than asserted.

A removal now allocates exactly 64 bytes: the arguments object and one single-element array. There is nothing left to remove without changing what the event carries.

## And the time, which pays for the notification several times over

Arms whose standard deviation is under a tenth of the mean, so quotable:

| operation | before | after | ratio |
|---|---:|---:|---:|
| set, hashed | 394.2 μs | 213.8 μs | 0.54 |
| remove, hashed | 330.0 μs | 254.9 μs | 0.77 |
| set, concurrent | 945.1 μs | 756.9 μs | 0.80 |
| add, hashed | 385.4 μs | 333.0 μs | 0.86 |
| remove, concurrent | 454.9 μs | 389.9 μs | 0.86 |
| add, concurrent | 602.3 μs | 556.3 μs | 0.92 |

**Every one of those is net of the `Item[]` notification being added**, so the answer to the original question is that its cost is smaller than a saving that reaches 46%. I cannot isolate it from this run and will not pretend to: what can be said is that it allocates nothing, since the arguments are a cached static, and that the mutation paths got faster with it in place.

The sorted dictionary's add and remove arms remain too noisy to quote — 1,584 μs of deviation on a 1,719 μs mean for the add, with a median of 594. Its medians moved the right way. Its means are not evidence.

## The notification went in one place

`Item[]` is announced from `OnChanged`, not from each mutation. Every dictionary change already funnels through it, so add, remove, indexer assignment, the range operations, `TryAdd`, `TryRemove`, `GetOrAdd`, `AddOrUpdate`, `Reset` and `Clear` are all covered by construction. Three call sites across three forty-five-kilobyte files, rather than several dozen with a chance of missing one.

The `Count` notification still comes *after* the change event in these types, where the collection now raises its properties first. That is left alone deliberately: there is no base class here to derive the right order from, and changing it would be a convention argument with no measurement behind it.
