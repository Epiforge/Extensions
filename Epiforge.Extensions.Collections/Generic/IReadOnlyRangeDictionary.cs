namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Represents a read-only generic collection of key/value pairs that supports bulk reads
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary</typeparam>
public interface IReadOnlyRangeDictionary<TKey, TValue> :
    IDictionary<TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>
    /// Determines whether the <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/> contains an element with the specified key
    /// </summary>
    /// <param name="key">The key to locate in the <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/></param>
    /// <returns><c>true</c> if the <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/> contains an element with the key; otherwise, <c>false</c></returns>
    new bool ContainsKey(TKey key);

    /// <summary>
    /// Gets the elements with the specified keys
    /// </summary>
    /// <param name="keys">The keys of the elements to get</param>
    /// <returns>The elements with the specified keys</returns>
    IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys);

    /// <summary>
    /// Gets the value associated with the specified key
    /// </summary>
    /// <param name="key">The key the value of which to get</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter</param>
    /// <returns><c>true</c> if the object that implements <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/> contains an element with the specified key; otherwise, <c>false</c></returns>
    new bool TryGetValue(TKey key, out TValue value);

    /// <summary>
    /// Gets the element with the specified key
    /// </summary>
    /// <param name="key">The key of the element to get</param>
    /// <returns>The element with the specified key</returns>
    new TValue this[TKey key] { get; }

    /// <summary>
    /// Gets an <see cref="ICollection{T}"/> containing the keys of the <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/>
    /// </summary>
    new IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets an <see cref="ICollection{T}"/> containing the values in the <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/>
    /// </summary>
    new IEnumerable<TValue> Values { get; }
}
