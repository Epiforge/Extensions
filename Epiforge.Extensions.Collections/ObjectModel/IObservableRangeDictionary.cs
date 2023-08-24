namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Represents a generic collection of key/value pairs that supports bulk operations and notifies listeners of dynamic changes, such as when an item is added and removed or the whole list is cleared
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary</typeparam>
public interface IObservableRangeDictionary<TKey, TValue> :
    INotifyCollectionChanged,
    INotifyDictionaryChanged,
    INotifyDictionaryChanged<TKey, TValue>,
    IRangeDictionary<TKey, TValue>
{
    /// <summary>
    /// Gets the value for the specified key
    /// </summary>
    /// <param name="key">The key</param>
    new TValue this[TKey key] { get; }

    /// <summary>
    /// Gets the number of key/value pairs contained in the <see cref="IObservableRangeDictionary{TKey, TValue}"/>
    /// </summary>
    new int Count { get; }
}
