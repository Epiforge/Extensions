namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents an observable dictionary which can be used to generate observable queries
/// </summary>
/// <typeparam name="TKey">They type of keys in the dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary</typeparam>
public interface IObservableDictionaryQuery<TKey, TValue> :
    INotifyDictionaryChanged<TKey, TValue>,
    IObservableQuery,
    IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>
    /// Gets the exception encountered while performing the operation
    /// </summary>
    Exception? OperationFault { get; }

    /// <summary>
    /// Continuously applies an accumulator function to each key/value pair of the dictionary
    /// </summary>
    /// <typeparam name="TAccumulate">The type of the accumulator value</typeparam>
    /// <typeparam name="TResult">The type of the resulting value</typeparam>
    /// <param name="seedFactory">A method to produce the initial accumulator value when the dictionary changes</param>
    /// <param name="func">An accumulator method to be invoked on each key/value pair</param>
    /// <param name="resultSelector">A method to transform the final accumulator value into the result value</param>
    /// <returns>The transformed final accumulator value</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector);

    /// <summary>
    /// Continuously determines whether all key/value pairs of the dictionary satisfy a condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>true</c> when every key/value pair of the dictionary passes the test in the specified predicate, or if the sequence is empty; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously determines whether the dictionary contains any key/value pairs
    /// </summary>
    /// <returns>><c>true</c> if the dictionary contains any key/value pairs; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAny();

    /// <summary>
    /// Continuously determines whether any key/value pairs of the dictionary satisfies a condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>true</c> if any key/value pairs in the dictionary pass the test in the specified predicate; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously computes the average of the values in the dictionary
    /// </summary>
    /// <returns>The average value of the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveAverage();

    /// <summary>
    /// Continuously computes the average of the values that are obtained by invoking a transform function on each key/value pairs of the dictionary
    /// </summary>
    /// <typeparam name="TResult">The type of the values being averaged</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The average value of the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously casts the values of the dictionary to the specified type
    /// </summary>
    /// <typeparam name="TResult">The type to cast the values</typeparam>
    /// <returns>Each key/value pair of the dictionary with the value cast to the specified type</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TResult> ObserveCast<TResult>();

    /// <summary>
    /// Continuously observes a <see cref="IReadOnlyDictionary{TKey, TValue}"/> using a <see cref="ObservableConcurrentDictionary{TKey, TValue}"/> as the backing store
    /// </summary>
    /// <returns>A dictionary using a <see cref="ObservableConcurrentDictionary{TKey, TValue}"/> as the backing store that contains values from this dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently();

    /// <summary>
    /// Continuously determines the number of key/value pairs in the dictionary
    /// </summary>
    /// <returns>The number of key/value pairs in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<int> ObserveCount();

    /// <summary>
    /// Continuously determines the number of key/value pairs in the dictionary which satisfy a condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>The number of key/value pairs in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<int> ObserveCount(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary
    /// </summary>
    /// <returns>The first key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst();

    /// <summary>
    /// Continuously returns the first key/value pair in the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>The first key/value pair in the dictionary that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary
    /// </summary>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns>The first key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the first key/value pair in the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns>The first key/value pair in the dictionary that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the first key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault();

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pair passes the test specified by <paramref name="predicate"/>; otherwise, the first key/value pair in the dictionary that passes the test specified by <paramref name="predicate"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the first key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pair passes the test specified by <paramref name="predicate"/>; otherwise, the first key/value pair in the dictionary that passes the test specified by <paramref name="predicate"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary
    /// </summary>
    /// <returns>The value at the last position in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast();

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>The last key/value pair in the dictionary that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary
    /// </summary>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns>The value at the last position in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns>The last key/value pair in the dictionary that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the last key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault();

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pairs pass the test in the predicate function; otherwise, the last key/value pair that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the last key/value pair in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <param name="comparer">The comparer to use when comparing keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pairs pass the test in the predicate function; otherwise, the last key/value pair that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> comparer);

    /// <summary>
    /// Continuously returns the maximum value in the dictionary
    /// </summary>
    /// <returns>The maximum value in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveMax();

    /// <summary>
    /// Continuously invokes a transform function on each key/value pair of the dictionary and returns the maximum value
    /// </summary>
    /// <typeparam name="TResult">The type of the maximum value</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The maximum value in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously returns the minimum value in the dictionary
    /// </summary>
    /// <returns>The minimum value in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveMin();

    /// <summary>
    /// Continuously invokes a transform function on each key/value pair of the dictionary and returns the minimum value
    /// </summary>
    /// <typeparam name="TResult">The type of the minimum value</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The minimum value in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously filters the values of the dictionary based on a specified type
    /// </summary>
    /// <typeparam name="TResult">The type to filter the values of the dictionary on</typeparam>
    /// <returns>A dictionary that contains key/value pairs from this dictionary for which the values are of type <typeparamref name="TResult"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>();

    /// <summary>
    /// Generates a dictionary which actively projects each key/value pair of this dictionary into a key/value pair
    /// </summary>
    /// <typeparam name="TResultKey">The type of the keys in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <typeparam name="TResultValue">The type of the values in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each key/value pair in this dictionary to obtain a resulting key</param>
    /// <param name="valueSelector">A transform function to apply to each key/value pair in this dictionary to obtain a resulting value</param>
    /// <returns>An dictionary the key/value pairs of which are the result of invoking the transform function on each key/value pair in this dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector)
        where TResultKey : notnull;

    /// <summary>
    /// Generates a dictionary which actively projects each key/value pair of this dictionary into a key/value pair
    /// </summary>
    /// <typeparam name="TResultKey">The type of the keys in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <typeparam name="TResultValue">The type of the values in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each key/value pair in this dictionary to obtain a resulting key</param>
    /// <param name="valueSelector">A transform function to apply to each key/value pair in this dictionary to obtain a resulting value</param>
    /// <param name="equalityComparer">The equality comparer to use to compare keys in the returned dictionary</param>
    /// <returns>An dictionary the key/value pairs of which are the result of invoking the transform function on each key/value pair in this dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, TResultKey>> keySelector, Expression<Func<TKey, TValue, TResultValue>> valueSelector, IEqualityComparer<TResultKey> equalityComparer)
        where TResultKey : notnull;

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary, and becomes faulted if there is not exactly one key/value pair in the dictionary
    /// </summary>
    /// <returns>The single key/value pair of the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle();

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary that satisfies a specified condition, and becomes faulted if more than one such key/value pair exists
    /// </summary>
    /// <param name="predicate">A function to test an key/value pair for a condition</param>
    /// <returns>The single key/value pair of the dictionary that satisfies a condition</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary, or a default value if the dictionary is empty; becomes faulted if there is more than one key/value pair in the dictionary
    /// </summary>
    /// <returns>The single key/value pair of the dictionary, or <c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary contains no key/value pairs</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault();

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary that satisfies a specified condition or a default value if no such key/value pair exists; becomes faulted if more than one key/value pair satisfies the condition
    /// </summary>
    /// <param name="predicate">A function to test an key/value pair for a condition</param>
    /// <returns>The single key/value pair of the dictionary that satisfies the condition, or <c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if no such key/value pair is found</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously computes the sum of the values in the dictionary
    /// </summary>
    /// <returns>The sum of the values in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveSum();

    /// <summary>
    /// Continuously computes the sum of the values that are obtained by invoking a transform function on each key/value pair of the dictionary
    /// </summary>
    /// <typeparam name="TResult">The type of the values being summed</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The sum of the projected values</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Creates a collection which contains the value of each key/value pair
    /// </summary>
    /// <returns>A collection which contains the value of each key/value pair</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TValue> ObserveToCollection();

    /// <summary>
    /// Creates a collection which continuously projects each key/value pair of the dictionary into a new form
    /// </summary>
    /// <param name="selector">A transform function to apply to each key/value pair to produce an element</param>
    /// <returns>A collection the elements of which are the result of invoking the transform function on each key/value pair of the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Expression<Func<TKey, TValue, TElement>> selector);

    /// <summary>
    /// Creates a dictionary that is kept consistent on a specified <see cref="SynchronizationContext"/> with this dictionary (use <see cref="ObserveUsingSynchronizationContextEventually(SynchronizationContext)"/> instead when this method may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> on which to perform consistency operations</param>
    /// <returns>A dictionary that is kept consistent with this dictionary on <paramref name="synchronizationContext"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext);

    /// <summary>
    /// Creates dictionary that is eventually made consistent on a specified <see cref="SynchronizationContext"/> with this dictionary (use this method instead of <see cref="ObserveUsingSynchronizationContext(SynchronizationContext)"/> when the same may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> on which to perform consistency operations</param>
    /// <returns>A dictionary that is eventually made consistent with this dictionary on <paramref name="synchronizationContext"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext);

    /// <summary>
    /// Continuously returns the value for a specified key in the dictionary
    /// </summary>
    /// <param name="key">The key of the value to retrieve</param>
    /// <returns>The value for the specified key in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveValueFor(TKey key);

    /// <summary>
    /// Continuously returns the value for a specified key in the dictionary or a default value if the key is not in the dictionary
    /// </summary>
    /// <param name="key">The key of the value to retrieve</param>
    /// <returns><c>default</c>(<typeparamref name="TValue"/>) if the key is not in the dictionary; otherwise, the value for the specified key in the dictionary</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TValue> ObserveValueForOrDefault(TKey key);

    /// <summary>
    /// Continuously filters the dictionary based on a predicate
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>A dictionary that contains key/value pairs from this dictionary that satisfy the condition</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate);
}
