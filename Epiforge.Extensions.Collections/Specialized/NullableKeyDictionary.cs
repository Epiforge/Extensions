namespace Epiforge.Extensions.Collections.Specialized;

#pragma warning disable CS8714

/// <summary>
/// Represents a collection of keys and values in which a key may be <c>null</c>
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
public sealed class NullableKeyDictionary<TKey, TValue> :
    IDictionary<TKey, TValue>,
    IHashKeys<TKey>,
    IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the default initial capacity, and uses the default equality comparer for the key type
    /// </summary>
    public NullableKeyDictionary() =>
        dict = new Dictionary<TKey, TValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the default initial capacity, and uses the default equality comparer for the key type
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    public NullableKeyDictionary(ILogger logger) :
        this() =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that contains elements copied from the specified <see cref="IDictionary{TKey, TValue}"/> and uses the default equality comparer for the key type
    /// </summary>
    /// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new <see cref="NullableKeyDictionary{TKey, TValue}"/></param>
    public NullableKeyDictionary(IDictionary<TKey, TValue> dictionary) :
        this()
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        AddRange(dictionary);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that contains elements copied from the specified <see cref="IDictionary{TKey, TValue}"/> and uses the default equality comparer for the key type
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new <see cref="NullableKeyDictionary{TKey, TValue}"/></param>
    public NullableKeyDictionary(ILogger logger, IDictionary<TKey, TValue> dictionary) :
        this(dictionary) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the default initial capacity, and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(IEqualityComparer<TKey> comparer) =>
        dict = new Dictionary<TKey, TValue>(comparer);

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the default initial capacity, and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(ILogger logger, IEqualityComparer<TKey> comparer) :
        this(comparer) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the specified initial capacity, and uses the default equality comparer for the key type
    /// </summary>
    /// <param name="capacity">The initial number of elements that the <see cref="NullableKeyDictionary{TKey, TValue}"/> can contain</param>
    public NullableKeyDictionary(int capacity) =>
        dict = new Dictionary<TKey, TValue>(capacity);

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the specified initial capacity, and uses the default equality comparer for the key type
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="capacity">The initial number of elements that the <see cref="NullableKeyDictionary{TKey, TValue}"/> can contain</param>
    public NullableKeyDictionary(ILogger logger, int capacity) :
        this(capacity) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that contains elements copied from the specified <see cref="IDictionary{TKey, TValue}"/> and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new <see cref="NullableKeyDictionary{TKey, TValue}"/></param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
        this(comparer)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        AddRange(dictionary);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that contains elements copied from the specified <see cref="IDictionary{TKey, TValue}"/> and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new <see cref="NullableKeyDictionary{TKey, TValue}"/></param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(ILogger logger, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
        this(dictionary, comparer) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the specified initial capacity, and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="capacity">The initial number of elements that the <see cref="NullableKeyDictionary{TKey, TValue}"/> can contain</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(int capacity, IEqualityComparer<TKey> comparer) =>
        dict = new Dictionary<TKey, TValue>(capacity, comparer);

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableKeyDictionary{TKey, TValue}"/> class that is empty, has the specified initial capacity, and uses the specified <see cref="IEqualityComparer{T}"/>
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="capacity">The initial number of elements that the <see cref="NullableKeyDictionary{TKey, TValue}"/> can contain</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default <see cref="EqualityComparer{T}"/> for the type of the key</param>
    public NullableKeyDictionary(ILogger logger, int capacity, IEqualityComparer<TKey> comparer) :
        this(capacity, comparer) =>
        this.logger = logger;

    readonly Dictionary<TKey, TValue> dict;
    bool hasNullKeyedValue = false;
    readonly ILogger? logger;
    TValue nullKeyedValue = default!;

    /// <summary>
    /// Gets the <see cref="IEqualityComparer{TKey}"/> that is used to determine equality of keys for the dictionary
    /// </summary>
    public IEqualityComparer<TKey> Comparer =>
        dict.Comparer;

    /// <summary>
    /// Gets the number of elements in the collection
    /// </summary>
    public int Count =>
        dict.Count + (hasNullKeyedValue ? 1 : 0);

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly =>
        ((IDictionary<TKey, TValue>)dict).IsReadOnly;

    /// <summary>
    /// Gets an <see cref="ICollection{T}"/> containing the keys of the <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    public ICollection<TKey> Keys =>
        (hasNullKeyedValue ? new TKey[] { default! } : Enumerable.Empty<TKey>()).Concat(((IDictionary<TKey, TValue>)dict).Keys).ToImmutableArray();

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys =>
        Keys;

    /// <summary>
    /// Gets an <see cref="ICollection{T}"/> containing the values in the <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    public ICollection<TValue> Values =>
        (hasNullKeyedValue ? new TValue[] { nullKeyedValue } : Enumerable.Empty<TValue>()).Concat(((IDictionary<TKey, TValue>)dict).Values).ToImmutableArray();

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values =>
        Values;

    /// <summary>
    /// Gets or sets the value associated with the specified key
    /// </summary>
    /// <param name="key">The key of the value to get or set</param>
    /// <returns>The value associated with the specified key</returns>
    public TValue this[TKey key]
    {
        get => key is null ? hasNullKeyedValue ? nullKeyedValue : throw new KeyNotFoundException() : dict[key];
        set
        {
            if (key is null)
                if (hasNullKeyedValue)
                    nullKeyedValue = value;
                else
                    throw new KeyNotFoundException();
            else
                dict[key] = value;
            logger?.LogTrace("NullableKeyDictionary changed: set [{Key}, {Value}]", key, value);
        }
    }

    /// <summary>
    /// Adds the specified key and value to the dictionary
    /// </summary>
    /// <param name="key">The key of the element to add</param>
    /// <param name="value">The value of the element to add</param>
    public void Add(TKey key, TValue value)
    {
        if (key is null)
        {
            if (hasNullKeyedValue)
                throw new ArgumentException("A value with a null key has already been added");
            nullKeyedValue = value;
            hasNullKeyedValue = true;
        }
        else
            dict.Add(key, value);
        logger?.LogTrace("NullableKeyDictionary changed: added [{Key}, {Value}]", key, value);
    }

    void AddRange(IDictionary<TKey, TValue> dictionary)
    {
        foreach (var kv in dictionary)
            Add(kv.Key, kv.Value);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
        Add(item.Key, item.Value);

    /// <summary>
    /// Removes all keys and values from the <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    public void Clear()
    {
        hasNullKeyedValue = false;
        nullKeyedValue = default!;
        dict.Clear();
        logger?.LogTrace("NullableKeyDictionary changed: cleared");
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
        item.Key is null ? hasNullKeyedValue && EqualityComparer<TValue>.Default.Equals(nullKeyedValue, item.Value) : ((IDictionary<TKey, TValue>)dict).Contains(item);

    /// <summary>
    /// Determines whether the <see cref="IDictionary{TKey, TValue}"/> contains an element with the specified key
    /// </summary>
    /// <param name="key">The key to locate in the <see cref="IDictionary{TKey, TValue}"/></param>
    /// <returns><c>true</c> if the <see cref="IDictionary{TKey, TValue}"/> contains an element with the key; otherwise, <c>false</c></returns>
    public bool ContainsKey(TKey key) =>
        key is null ? hasNullKeyedValue : dict.ContainsKey(key);

    /// <summary>
    /// Determines whether the <see cref="IDictionary{TKey, TValue}"/> contains a specific value
    /// </summary>
    /// <param name="value">The value to locate in the <see cref="IDictionary{TKey, TValue}"/></param>
    /// <returns><c>true</c> if the <see cref="IDictionary{TKey, TValue}"/> contains an element with the value; otherwise, <c>false</c></returns>
    public bool ContainsValue(TValue value) =>
        dict.ContainsValue(value) || hasNullKeyedValue && EqualityComparer<TValue>.Default.Equals(nullKeyedValue, value);

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (hasNullKeyedValue)
            array[0] = new KeyValuePair<TKey, TValue>(default!, nullKeyedValue);
        ((IDictionary<TKey, TValue>)dict).CopyTo(array, arrayIndex + (hasNullKeyedValue ? 1 : 0));
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
        (hasNullKeyedValue ? new KeyValuePair<TKey, TValue>[] { new KeyValuePair<TKey, TValue>(default!, nullKeyedValue) } : Enumerable.Empty<KeyValuePair<TKey, TValue>>()).Concat(dict).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();

    /// <summary>
    /// Ensures that the dictionary can hold up to a specified number of entries without any further expansion of its backing storage
    /// </summary>
    /// <param name="capacity">The number of entries</param>
    /// <returns>The current capacity of the <see cref="IDictionary{TKey, TValue}"/></returns>
    public int EnsureCapacity(int capacity)
    {
        var ensuredCapacity = dict.EnsureCapacity(capacity);
        logger?.LogTrace("NullableKeyDictionary ensured capacity {EnsuredCapacity} from request of {Capacity}", ensuredCapacity, capacity);
        return ensuredCapacity;
    }

    /// <summary>
    /// Removes the element with the specified key from the <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="key">The key of the element to remove</param>
    /// <returns><c>true</c> if the element is successfully removed; otherwise, <c>false</c> (this method also returns <c>false</c> if key was not found in the original <see cref="IDictionary{TKey, TValue}"/>)</returns>
    public bool Remove(TKey key)
    {
        if (key is null)
        {
            if (hasNullKeyedValue)
            {
                nullKeyedValue = default!;
                hasNullKeyedValue = false;
                logger?.LogTrace("NullableKeyDictionary changed: removed {Key}", key);
                return true;
            }
            return false;
        }
        var removed = dict.Remove(key);
        if (removed)
            logger?.LogTrace("NullableKeyDictionary changed: removed {Key}", key);
        return removed;
    }

    /// <summary>
    /// Removes the element with the specified key from the <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    /// <param name="key">The key of the element to remove</param>
    /// <param name="value">The value that was removed</param>
    /// <returns><c>true</c> if the element is successfully removed; otherwise, <c>false</c> (this method also returns <c>false</c> if key was not found in the original <see cref="IDictionary{TKey, TValue}"/>)</returns>
    [SuppressMessage("Code Analysis", "CA1021: Avoid out parameters")]
    public bool Remove(TKey key, out TValue value)
    {
        if (key is null)
        {
            if (hasNullKeyedValue)
            {
                value = nullKeyedValue;
                nullKeyedValue = default!;
                hasNullKeyedValue = false;
                logger?.LogTrace("NullableKeyDictionary changed: removed [{Key}, {Value}]", key, value);
                return true;
            }
            value = default!;
            return false;
        }
        var removed = dict.Remove(key, out value!);
        if (removed)
            logger?.LogTrace("NullableKeyDictionary changed: removed [{Key}, {Value}]", key, value);
        return removed;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        if (item.Key is null)
        {
            if (hasNullKeyedValue && EqualityComparer<TValue>.Default.Equals(nullKeyedValue, item.Value))
            {
                nullKeyedValue = default!;
                hasNullKeyedValue = false;
                logger?.LogTrace("NullableKeyDictionary changed: removed [{Key}, {Value}]", item.Key, item.Value);
                return true;
            }
            return false;
        }
        var removed = ((IDictionary<TKey, TValue>)dict).Remove(item);
        if (removed)
            logger?.LogTrace("NullableKeyDictionary changed: removed [{Key}, {Value}]", item.Key, item.Value);
        return removed;
    }

    /// <summary>
    /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries
    /// </summary>
    public void TrimExcess()
    {
        dict.TrimExcess();
        logger?.LogTrace("NullableKeyDictionary trimmed excess");
    }

    /// <summary>
    /// Sets the capacity of this dictionary to hold up a specified number of entries without any further expansion of its backing storage
    /// </summary>
    /// <param name="capacity">The new capacity</param>
    public void TrimExcess(int capacity)
    {
        dict.TrimExcess(capacity);
        logger?.LogTrace("NullableKeyDictionary trimmed excess to {Capacity}", capacity);
    }

    /// <summary>
    /// Attempts to add the specified key and value to the dictionary
    /// </summary>
    /// <param name="key">The key of the element to add</param>
    /// <param name="value">The value of the element to add</param>
    /// <returns><c>true</c> if the key/value pair was added to the dictionary successfully; otherwise, <c>false</c></returns>
    public bool TryAdd(TKey key, TValue value)
    {
        if (key is null)
        {
            if (!hasNullKeyedValue)
            {
                hasNullKeyedValue = true;
                nullKeyedValue = value;
                logger?.LogTrace("NullableKeyDictionary changed: added [{Key}, {Value}]", key, value);
                return true;
            }
            return false;
        }
        var added = dict.TryAdd(key, value);
        if (added)
            logger?.LogTrace("NullableKeyDictionary changed: added [{Key}, {Value}]", key, value);
        return added;
    }

    /// <summary>
    /// Gets the value associated with the specified key
    /// </summary>
    /// <param name="key">The key the value of which to get</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter</param>
    /// <returns><c>true</c> if the object that implements <see cref="IDictionary{TKey, TValue}"/> contains an element with the specified key; otherwise, <c>false</c></returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (key is null)
        {
            if (hasNullKeyedValue)
            {
                value = nullKeyedValue;
                return true;
            }
            value = default!;
            return false;
        }
        return dict.TryGetValue(key, out value!);
    }
}
