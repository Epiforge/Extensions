This library provides a number of extension methods for collections and dictionaries:

* `EnumerableExtensions`, providing:
  * `FindIndex` - Finds the index of the first element in the source that satisfies the specified predicate
  * `FindLastIndex` - Finds the index of the last element in the source that satisfies the specified predicate
  * `FindIndicies` - Finds the indicies of the elements in the source that satisfy the specified predicate
  * `IndexOf` - Finds the first index of the specified item in the source
  * `LastIndexOf` - Finds the last index of the specified item in the source
  * `IndiciesOf` - Finds the indicies of the specified item in the source
* `DictionaryExtensions`, providing:
  * `GetOrAdd` - Adds a key/value pair to the specified `IDictionary` or `IDictionary<TKey, TValue>` by using the specified function if the key does not already exist (returns the new value, or the existing value if the key exists)
  * `TryRemove` - Attempts to remove and return the value that has the specified key from the specified `IDictionary` or `IDictionary<TKey, TValue>`

# Generic
* `ReadOnlyDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IReadOnlyDictionary<TKey, TValue>`
* `ReadOnlyRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IRangeDictionary<TKey, TValue>`
* `ReadOnlyConcurrentDictionary<TKey, TValue>` is a read-only wrapper for `ObservableConcurrentDictionary<TKey, TValue>`
* `ReversedComparer<T>` is a comparer that reverses the comparison of another comparer (this is useful when you want to sort a list in the opposite order of the default sort order)

# ObjectModel
* `ObservableDictionary<TKey, TValue>`, `ObservableSortedDictionary<TKey, TValue>`, `ObservableConcurrentDictionary<TKey, TValue>` are counterparts to the BCL's `Dictionary<TKey, TValue>`, `SortedDictionary<TKey, TValue>`, and `ConcurrentDictionary<TKey, TValue>`, respectively, that implement the also included `IRangeDictionary<TKey, TValue>` and `INotifyDictionaryChanged<TKey, TValue>`. Ever want to add multiple items to a dictionary at once... or keep an eye on what's being done to it? Now you can.
* `RangeObservableCollection<T>` is a counterpart to the BCL's `ObservableCollection<T>` which implements:
  * `AddRange` - Adds objects to the end of the collection
  * `GetAndRemoveAll` - Removes all object from the collection that satisfy a predicate
  * `GetAndRemoveAt` - Gets the element at the specified index and removes it from the collection
  * `GetRange` - Gets the elements in the range starting at the specified index and of the specified length
  * `InsertRange` - Inserts elements into the collection at the specified index
  * `MoveRange` - Moves the items at the specified index to a new location in the collection
  * `RemoveAll` - Removes all object from the collection that satisfy a predicate
  * `RemoveRange` - Removes the specified items from the collection *or* removes the specified range of items from the collection
  * `ReplaceAll` - Replace all items in the collection with the items in the specified collection
  * `ReplaceRange` - Replaces the specified range of items from the collection with the items in the specified collection
  * `Reset` - Resets the collection with the specified collection of items
* `ReadOnlyObservableRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IObservableRangeDictionary<TKey, TValue>`.

# Specialized
* `EquatableList<T>` is an immutable list of items which may be compared with other instances of the same type and produces a hash code based on the permutation of its contents.
* `NullableKeyDictionary<TKey, TValue>` and `NullableKeySortedDictionary<TKey, TValue>` are very slim implementations of `IDictionary<TKey, TValue>` that allow a single null key (useful for some edge cases in which a null key is simply going to happen and you need to be able to deal with it; otherwise, use other dictionary classes).
* `OrderedHashSet<T>` is a counterpart to the BCL's `HashSet<T>` that maintains the order of the elements in the set. All operations are still *O(1)*, just like the original, but if you enumerate over it you will get elements in the exact order they were added. There are also methods for manipulating the order.