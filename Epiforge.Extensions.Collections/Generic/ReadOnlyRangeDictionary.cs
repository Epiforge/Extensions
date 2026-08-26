namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Read-only wrapper around an <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/>
/// </summary>
/// <typeparam name="TKey">The type of keys in the read-only dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the read-only dictionary</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ReadOnlyRangeDictionary{TKey, TValue}"/> class
/// </remarks>
/// <param name="readOnlyRangeDictionary">The <see cref="IReadOnlyRangeDictionary{TKey, TValue}"/> around which to wrap</param>
[SuppressMessage("Code Analysis", "CA1033: Interface methods should be callable by child types")]
public class ReadOnlyRangeDictionary<TKey, TValue>(IReadOnlyRangeDictionary<TKey, TValue> readOnlyRangeDictionary) :
    ReadOnlyDictionary<TKey, TValue>(readOnlyRangeDictionary),
    IReadOnlyRangeDictionary<TKey, TValue>
    where TKey : notnull
{
    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) =>
        throw new NotSupportedException();

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
        throw new NotSupportedException();

    void ICollection<KeyValuePair<TKey, TValue>>.Clear() =>
        throw new NotSupportedException();

    /// <summary>
    /// Determines whether the read-only range dictionary contains a key/value pair
    /// </summary>
    /// <param name="item">The key/value pair to locate in the read-only range dictionary</param>
    /// <returns><c>true</c> if the item is found in the read-only range dictionary; otherwise, <c>false</c></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        readOnlyRangeDictionary.Contains(item);

    /// <summary>
    /// Copies the elements of the read-only range dictionary to an <see cref="Array"/>, starting at a particular <see cref="Array"/> index
    /// </summary>
    /// <param name="array">The one-dimensional <see cref="Array"/> that is the destination of the elements copied from read-only range dictionary (the <see cref="Array"/> must have zero-based indexing)</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        readOnlyRangeDictionary.CopyTo(array, arrayIndex);

    /// <summary>
    /// Gets the elements with the specified keys
    /// </summary>
    /// <param name="keys">The keys of the elements to get</param>
    /// <returns>The elements with the specified keys</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys) =>
        readOnlyRangeDictionary.GetRange(keys);

    bool IDictionary<TKey, TValue>.Remove(TKey key) =>
        throw new NotSupportedException();

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) =>
        throw new NotSupportedException();

    ICollection<TKey> IDictionary<TKey, TValue>.Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ((IDictionary<TKey, TValue>)readOnlyRangeDictionary).Keys;
    }

    ICollection<TValue> IDictionary<TKey, TValue>.Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ((IDictionary<TKey, TValue>)readOnlyRangeDictionary).Values;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly =>
        true;

    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => readOnlyRangeDictionary[key];
        set => throw new NotSupportedException();
    }
}
