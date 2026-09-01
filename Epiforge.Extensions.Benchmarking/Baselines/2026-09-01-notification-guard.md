# 1 September 2026 — the notification guard, which cannot work

A guard was written, measured, and reverted the same hour. This records why, so it is not attempted a third time.

## The parked item

The 31 August handoff records that `ObservableCollectionWhereQuery`, around line 126, constructs a `NotifyCollectionChangedEventArgs` **whether or not anything is subscribed**, and proposes the fix which reached zero allocation on five of nine dictionary mutation paths in Collections: an `IsChangeObserved` property tested at each call site before the arguments are built.

## The measurement

`QueryNotificationBenchmarks`, three arms, a thousand elements. The predicate is `person => (person.Rank & 1) == 0` and each invocation toggles that bit on every element, so every element crosses the predicate and reaches the site. `FlipEveryRankWithNoQuery` flips the same ranks with no query attached, isolating what a `BenchmarkPerson` costs to notify on its own.

| arm | mean | allocated |
|--- |---: |---: |
| `FlipEveryMembershipWithASubscriber` | 159.05 μs | 257.81 KB |
| `FlipEveryMembershipWithNothingObserving` | 160.83 μs | 257.81 KB |
| `FlipEveryRankWithNoQuery` | 8.43 μs | 46.88 KB |

216 bytes per flip above the control. The two query arms allocate **identically**, which is the premise of the guard.

After the guard was added: **257.81 KB and 257.81 KB.** Byte for byte unchanged, in both arms.

## Why it cannot work

Every query method on `ObservableCollectionQuery` ends the same way — `return whereQuery.AsScoped()`, twenty-eight of them — and `ScopedObservableCollectionQuery`'s constructor does:

```csharp
this.query.PropertyChanged += QueryPropertyChanged;
this.query.PropertyChanging += QueryPropertyChanging;
this.query.CollectionChanged += QueryCollectionChanged;
```

**A caller never holds an inner query. It holds a scope, and the scope subscribes eagerly.** So `ObservableCollectionWhereQuery.CollectionChanged` is non-null from the moment the query is handed out, `IsChangeObserved` is always true, and the guard is a branch that is always taken.

The handoff's premise is false. It is not that nothing is subscribed; something always is.

The guard has been reverted. A condition that can never be false, named as though it discriminates, is worse than no guard at all.

## What would be needed instead

The scope would have to subscribe **lazily** — attaching to the inner query's `CollectionChanged` only when its own gains a first handler, and detaching on the last removal. Then `IsChangeObserved` would mean what it says, and the 216 bytes per flip would be recoverable for a query which is held and read without being subscribed to.

That is a change to subscription lifetime, not a guard. It interacts with disposal and with the outstanding-subscription invariants the tests assert. It needs its own before-tests and its own evidence, and it should not be attempted as a variation on this one.

## The dead benchmark run, and why it was worth having

The first version of this benchmark used `person => person.Rank > 0` against ranks 0 to 999 with `Rank ^= 1`. That toggle maps 0 to 1, 2 to 3, 4 to 5: **exactly two of a thousand elements ever cross that predicate.** The other 998 re-evaluated to the same boolean and returned at `if (node.Weight == newWeight) return;` without reaching the site at all.

Its 47.3 KB was not the notification path. It was 48 bytes per element of `BenchmarkPerson`'s own `PropertyChangingEventArgs` and `PropertyChangedEventArgs`, which `SetBackedProperty`'s caller-member-name overload constructs fresh on every set — which is why the corrected control arm lands at 46.88 KB.

This is the trap already recorded in this directory: *"A benchmark arm that never exercises the branch you optimized proves nothing."* It was written after `ResetRemovingAll` was measured with a branch that never ran, and it was read the same morning it was repeated.

What caught it was arithmetic. 47.3 KB over a thousand flips is 48 bytes, and no account of the notification path adds up to 48. Predicting a figure and checking whether the measurement can be made of the parts you claim is what turned a plausible number into a discovered defect.
