# Benchmark results, 6 September 2026 — the floor that was not one

Every observation measurement this week has treated 48 bytes per property change as the base cost: the `PropertyChangingEventArgs` and `PropertyChangedEventArgs` the changed object raises before the library sees anything. The orientation calls it the floor. It is not a floor, it is an allocation, and `PropertyChangeNotifier.OnPropertyChanged(string)` is `OnPropertyChanged(new PropertyChangedEventArgs(propertyName))`.

The class already has `SetBackedProperty` overloads which take pre-made arguments. Nothing in the suite uses them. This prices what they are worth, without changing the library.

## Environment

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## Reproducing

`EventArgumentsBenchmarks`. `BenchmarkPersonWithCachedEventArguments` is `BenchmarkPerson`'s `Rank` and nothing else, set through the overload which takes arguments rather than a name.

## The measurement

Per property change, over a thousand elements.

| arm | predicted | measured | mean |
|--- |---: |---: |---: |
| `ChangeEveryValueWithNoObservation` | 48 B (control) | **48 B** | 8.162 μs |
| `ChangeEveryValueWithNoObservationAndCachedEventArguments` | **0 B** | **0 B** | 4.456 μs |
| `ChangeEveryValueMemberRead` | 72 B (control) | **72 B** | 40.050 μs |
| `ChangeEveryValueMemberReadWithCachedEventArguments` | **24 B** | **24 B** | 34.063 μs |

Both controls reproduced and both predictions landed exactly.

**The additive model is confirmed end to end.** An observed member read costs 72 bytes; take the event arguments away and it costs 24, which is the boxed result and nothing else. 48 + 24 = 72, measured from both sides rather than assumed.

**Raising a property change costs 48 bytes and 3.7 nanoseconds.** The unobserved arms differ in nothing but the two allocations, and they differ by 3.706 μs over a thousand changes. On the observed pair the same removal is worth 5.99 μs, so the saving is not smaller when there is work around it.

## A rule that needs qualifying

The orientation says twenty-four bytes has no time price of its own, and that what it buys is a share of a collection. That was written to forbid attributing a time saving to one allocation site by differencing two arms which differ in other ways, and it stands for that. But this pair differs in nothing else, and **48 bytes of allocation is worth 3.7 ns here** — at these rates the collection share is measurable, and where a pair isolates the allocation exactly, the time it costs can be read off.

## What this opens, and what it does not

The prize is 48 bytes and 3.7 ns on **every property change of every type deriving from `PropertyChangeNotifier`**, which is the base class this suite is built on. That makes it the largest single item left.

The obvious fix is the wrong one. Caching inside `PropertyChangeNotifier.OnPropertyChanged(string)` needs a dictionary lookup, and a `ConcurrentDictionary` hit was measured today at 3.44 to 4.73 ns — twice, once for changing and once for changed. **That spends more time than it saves and only wins on allocation.**

The fix which wins on both columns is the one the class already offers and nothing uses: static per-property arguments at the call site, which is exactly what the cached arms here do at 0 bytes and 4.456 μs.

So the work is not a change to `PropertyChangeNotifier`. It is an audit of the notifying types **inside** Collections and Expressions — every `OnPropertyChanged(name)` or `SetBackedProperty(…, name)` on a path a consumer hits repeatedly — and giving each one static arguments. `ObservableDictionary`'s indexer notification already does this, so the pattern and its precedent are both in the codebase.

What it does not open is anything about a consumer's own types. A user's `Person` raising a change is the user's 48 bytes, and the only thing this library can do about it is document the overload.
