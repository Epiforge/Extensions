namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Represents a read-only generic collection of key/value pairs that notifies listeners of dynamic changes, such as when an item is added and removed or the whole list is cleared
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary</typeparam>
public interface IReadOnlyObservableRangeDictionary<TKey, TValue> :
    INotifyCollectionChanged,
    INotifyDictionaryChanged,
    INotifyDictionaryChanged<TKey, TValue>,
    IReadOnlyRangeDictionary<TKey, TValue>
{
    /// <summary>
    /// Gets the number of key/value pairs contained in the <see cref="IReadOnlyObservableRangeDictionary{TKey, TValue}"/>
    /// </summary>
    new int Count { get; }
}
