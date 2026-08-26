namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Represents a generic collection of key/value pairs that supports bulk operations
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary</typeparam>
public interface IRangeDictionary<TKey, TValue> :
    IReadOnlyRangeDictionary<TKey, TValue>
{
    /// <summary>
    /// Adds elements with the provided keys and values to the <see cref="IRangeDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="keyValuePairs">The key-value pairs to add</param>
    void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs);

    /// <summary>
    /// Adds elements with the provided keys and values to the <see cref="IRangeDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="keyValuePairs">The key-value pairs to add</param>
    void AddRange(IReadOnlyList<KeyValuePair<TKey, TValue>> keyValuePairs);

    /// <summary>
    /// Removes any elements that satisfy the specified predicate from the <see cref="IRangeDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="predicate">A predicate that returns <c>true</c> when passed the key and value of an element to be removed</param>
    /// <returns>The key-value pairs of the elements that were removed</returns>
    IReadOnlyList<KeyValuePair<TKey, TValue>> RemoveAll(Func<TKey, TValue, bool> predicate);

    /// <summary>
    /// Removes the elements with any of the specified keys from the <see cref="IRangeDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="keys">The keys of the elements to remove</param>
    /// <returns>The keys of the elements that were found and removed</returns>
    IReadOnlyList<TKey> RemoveRange(IEnumerable<TKey> keys);

    /// <summary>
    /// Replaces elements in the <see cref="IRangeDictionary{TKey, TValue}"/> with specified elements
    /// </summary>
    /// <param name="keyValuePairs">The replacement key-value pairs</param>
    void ReplaceRange(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs);

    /// <summary>
    /// Removes the elements with any of the specified keys from and then adds elements with the provided keys and values to the <see cref="IRangeDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="removeKeys">The keys of the elements to remove</param>
    /// <param name="newKeyValuePairs">The key-value pairs to add</param>
    /// <returns>The keys of the elements that were found and removed</returns>
    IReadOnlyList<TKey> ReplaceRange(IEnumerable<TKey> removeKeys, IEnumerable<KeyValuePair<TKey, TValue>> newKeyValuePairs);

    /// <summary>
    /// Reinitializes the hash table or binary search tree used internally by the <see cref="IRangeDictionary{TKey, TValue}"/>, removing all elements
    /// </summary>
    void Reset();

    /// <summary>
    /// Reinitializes the hash table or binary search tree used internally by the <see cref="IRangeDictionary{TKey, TValue}"/> with the elements from the specified dictionary
    /// </summary>
    /// <param name="dictionary">The dictionary from which to retrieve the initial elements</param>
    void Reset(IDictionary<TKey, TValue> dictionary);

    /// <summary>
    /// Gets or sets the element with the specified key
    /// </summary>
    /// <param name="key">The key of the element to get or set</param>
    /// <returns>The element with the specified key</returns>
    new TValue this[TKey key] { get; set; }
}
