# 31 August 2026 — what batching stopped announcing

`ObservableRangeCollection<T>` takes a constructor flag, `RaiseCollectionChangedEventsForIndividualElements`. With it set, a range operation delegates to `InsertItem`, `RemoveItem`, `MoveItem` and `ClearItems` on the base class. Without it, the collection mutates `Items` directly and raises one event itself.

Those two paths were not raising the same property notifications. The batch path raised `Count` and nothing else. The individual path — which is to say the base class — raises `Count` **and** `Item[]`.

So anything bound to an indexed position stopped refreshing, decided by a constructor argument.

## How it was found, and the instrument that found it

By reading the file, and then by not trusting the reading. The claim "the base class also raises `Item[]`" was recollection, and a claim about somebody else's code is the category that has been wrong most often in this series.

Documentation would not have settled it either, because what matters is what the runtime this library targets actually does, across five target frameworks, on the machine that builds it.

`ObservableRangeCollectionModeAgreement` settles it without asserting anything about the base class at all. For each range operation it runs the same call on two collections differing only in the flag, records every `PropertyChanged` name, and compares the distinct sets. In individual mode those notifications *come from* the base class, so whatever it does becomes the reference automatically.

The comparison is of distinct names rather than of counts. Batch mode raising fewer events is the entire purpose of the class; batch mode dropping a whole category of notification is not.

Six operations, six failures:

```
Assert.AreEqual failed. Expected:<Count, Item[]>. Actual:<Count>.
the batch mode did not notify what the individual mode notified
```

The first version of that assertion used `CollectionAssert.AreEquivalent` and reported `Expected:<2>. Actual:<1>.` — a difference without a subject. A test written to discover something has to report what it discovered.

## The fix

One static `PropertyChangedEventArgs("Item[]")` on `CommonPropertyChangeNotificationEventArgs`, a `NotifyIndexerChanged` helper beside the existing `NotifyCountChanged`, and seven call sites.

`MoveRange` had raised no property notification whatsoever in batch mode; it now raises `Item[]` and still not `Count`, matching `MoveItem`. `ReplaceAll` and `ReplaceRange` raise `Item[]` unconditionally while `Count` stays behind the existing length comparison, since a same-size replacement does not change the count. `Reset` got it too, although it has no per-element mode and so is not covered by the agreement test; leaving one path inconsistent to match a test's reach would be the wrong reason.

## Found and not fixed

**The two modes still order their notifications differently.** The base class raises `PropertyChanged` before `CollectionChanged`; every batch path here raises it after. The agreement test compares which properties are notified, not when, so it does not force this. Recorded as its own decision rather than folded into a fix.

**`RemoveRange(IEnumerable<T>)` is quadratic.** It calls `Items.IndexOf(item)` for each item, a linear scan each time. Removing a thousand items from a ten-thousand-element collection is ten million comparisons. The fix is not a one-liner: removal is by value, duplicates in the argument are meant to remove that many occurrences, and indices shift as removal proceeds, so a set of keys is not a drop-in replacement. It wants its own change and its own measurement.

**`ReplaceAll` and `ReplaceRange` can raise a `Replace` whose new and old item counts differ.** The code knows they can — `if (oldItems.Length != list.Count)` decides whether to announce a count change — and a consumer may reasonably read `Replace` as a same-size swap.

**`RemoveRange(IEnumerable<T>)` ignores the flag entirely** where every sibling honors it. This was nearly reported as a defect and is not one: removing items at scattered positions cannot be expressed as a single event, because `NotifyCollectionChangedEventArgs` carries one starting index. It is undocumented, though, and the flag's own summary implies otherwise.

## Why 24 KB of tests did not catch it

`ObservableRangeCollection` has a healthy test-to-source ratio — about 1.8, among the best in the package. Those tests assert what the collection *contains* after each operation, and by that standard both modes were always correct.

Nothing compared the two modes to each other. That is where every finding above lives.
