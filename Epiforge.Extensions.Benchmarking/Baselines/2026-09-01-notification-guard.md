# 1 September 2026 — the notification guard, and the subscription it needed first

A guard was written, measured at no change, reverted, and then made to work by changing something else. This records both halves, because the failed half is the more instructive one.

## The parked item

The 31 August handoff records that `ObservableCollectionWhereQuery`, around line 126, constructs a `NotifyCollectionChangedEventArgs` **whether or not anything is subscribed**, and proposes the fix which reached zero allocation on five of nine dictionary mutation paths in Collections.

## The instrument

`QueryNotificationBenchmarks`, three arms, a thousand elements, `DefaultJob`. The predicate is `person => (person.Rank & 1) == 0` and each invocation toggles that bit on every element, so every element crosses the predicate. `FlipEveryRankWithNoQuery` flips the same ranks with no query attached, isolating what a `BenchmarkPerson` costs to notify on its own.

## Act one: the guard alone does nothing

| arm | before | after the guard |
|--- |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 257.81 KB | 257.81 KB |
| `FlipEveryMembershipWithNothingObserving` | 257.81 KB | 257.81 KB |
| `FlipEveryRankWithNoQuery` | 46.88 KB | 46.88 KB |

Byte for byte unchanged. Every query method on `ObservableCollectionQuery` ends `return whereQuery.AsScoped()` — twenty-eight of them — and `ScopedObservableCollectionQuery`'s constructor did:

```csharp
this.query.PropertyChanged += QueryPropertyChanged;
this.query.PropertyChanging += QueryPropertyChanging;
this.query.CollectionChanged += QueryCollectionChanged;
```

**A caller never holds an inner query. It holds a scope, and the scope subscribed eagerly.** So `CollectionChanged` was never null, `IsChangeObserved` was always true, and the guard was a branch always taken. The handoff's premise is false: it is not that nothing is subscribed, it is that something always is.

## Act two: the subscription the guard needed

`ScopedObservableCollectionQuery.CollectionChanged` is now a custom event over a backing field. It attaches to the inner query on the first handler and detaches on the last; the constructor attaches nothing.

`PropertyChanged` and `PropertyChanging` stay eager, deliberately. `SetCount` raises them with cached static arguments, so there is nothing to save and every reason not to put a count notification at risk.

The accessors take a lock. A field-like event gets thread-safe add and remove from the compiler; a hand-written one does not, and two threads subscribing at once would otherwise both find a null field and attach twice, delivering everything twice.

| arm | before | after both halves | |
|--- |---: |---: |---: |
| `FlipEveryMembershipWithNothingObserving` | 257.81 KB / 160.83 μs | **187.50 KB / 144.82 μs** | **−70.3 KB, −9.7%** |
| `FlipEveryMembershipWithASubscriber` | 257.81 KB / 159.05 μs | 257.81 KB / 160.37 μs | control |
| `FlipEveryRankWithNoQuery` | 46.88 KB / 8.43 μs | 46.88 KB / 8.31 μs | control |

**72 bytes per flip.** Both controls held to the byte, and the subscriber arm not moving is what establishes that the guard is on the construction rather than on the notification.

## The prediction, which held

Recorded before the run: *"around 80 bytes per flip, taking 257.81 KB to roughly 180 KB — not the whole 216, because with the collection event skipped there are still two deferred notifications from `SetCount`, and `DeferNotification` promotes to a `List<object>` on the second one regardless."*

72 bytes and 187.5 KB. The reasoning was right as well as the figure: 216 less 72 leaves **exactly 144 bytes per flip** still allocated, which is the deferral list and the expression's own re-evaluation.

That number is worth having on its own terms. A `NotifyCollectionChangedEventArgs` for a single item with an index costs **72 bytes**, measured. The handoff said 192.

## What remains, unattempted

144 bytes per flip survive. `ObservableQuery.DeferNotification` holds the first notification in a field and promotes to a `List<object>` on the second, so a flip which raises `PropertyChanging`, `PropertyChanged` and nothing else still builds a list and its array to hold two cached statics. A second field, or a small inline pair, would avoid that for the common case and would help every query rather than this one.

Not attempted here, and it should not ride along with anything. It is a change to a mechanism every notification in the library passes through.

## What made the difference

Six tests written **before** the change, in `QueryNotificationDelivery`, all passing on the eager scope and required to pass unchanged afterward: a subscriber arriving after the query has already run unobserved, a subscriber leaving and returning, a second subscriber outliving the first, a chained query with nothing subscribed to either end, an unobserved query still tracking its source by `Count` and by index, and the count property still being announced when no collection subscriber exists.

Every one of those is a way lazy attachment could have gone quietly wrong. None of them is a benchmark.
