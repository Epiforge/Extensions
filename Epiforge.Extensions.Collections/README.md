This library provides a number of utilities surrounding collections:

* `EquatableList<T>` is an immutable list of items which may be compared with other instances of the same type and produces a hash code based on the permutation of its contents.
* `NullableKeyDictionary<TKey, TValue>` and `NullableKeySortedDictionary<TKey, TValue>` are very slim implementations of `IDictionary<TKey, TValue>` that allow a single null key (useful for some edge cases in which a null key is simply going to happen and you need to be able to deal with it; otherwise, use other dictionary classes).
* `ObservableDictionary<TKey, TValue>` and `ObservableSortedDictionary<TKey, TValue>` are counterparts to the BCL's `Dictionary<TKey, TValue>` and `SortedDictionary<TKey, TValue>`, respectively, that implement the also included `IRangeDictionary<TKey, TValue>` and `INotifyDictionaryChanged<TKey, TValue>`. Ever want to add multiple items to a dictionary at once... or keep an eye on what's being done to it? Now you can.
* `OrderedHashSet<T>` is a counterpart to the BCL's `HashSet<T>` that maintains the order of the elements in the set. All operations are still *O(1)*, just like the original, but if you enumerate over it you will get elements in the exact order they were added. There are also methods for manipulating the order.
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