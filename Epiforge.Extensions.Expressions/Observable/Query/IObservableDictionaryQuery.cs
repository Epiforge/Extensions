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
    IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector);

    /// <summary>
    /// Continuously determines whether all key/value pairs of the dictionary satisfy a condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>true</c> when every key/value pair of the dictionary passes the test in the specified predicate, or if the sequence is empty; otherwise, <c>false</c></returns>
    IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously determines whether the dictionary contains any key/value pairs
    /// </summary>
    /// <returns>><c>true</c> if the dictionary contains any key/value pairs; otherwise, <c>false</c></returns>
    IObservableScalarQuery<bool> ObserveAny();

    /// <summary>
    /// Continuously determines whether any key/value pairs of the dictionary satisfies a condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>true</c> if any key/value pairs in the dictionary pass the test in the specified predicate; otherwise, <c>false</c></returns>
    IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously computes the average of the values in the dictionary
    /// </summary>
    /// <returns>The average value of the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveAverage();

    /// <summary>
    /// Continuously computes the average of the values that are obtained by invoking a transform function on each key/value pairs of the dictionary
    /// </summary>
    /// <typeparam name="TResult">The type of the values being averaged</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The average value of the dictionary</returns>
    IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously observes a <see cref="IReadOnlyDictionary{TKey, TValue}"/> using a <see cref="ObservableConcurrentDictionary{TKey, TValue}"/> as the backing store
    /// </summary>
    /// <returns>A dictionary using a <see cref="ObservableConcurrentDictionary{TKey, TValue}"/> as the backing store that contains values from this dictionary</returns>
    IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently();

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary
    /// </summary>
    /// <returns>The first key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst();

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary
    /// </summary>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns>The first key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the first key/value pair in the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>The first key/value pair in the dictionary that passes the test in the spredicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the first key/value pair in the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns>The first key/value pair in the dictionary that passes the test in the spredicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the first key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault();

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the first key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pair passes the test specified by <paramref name="predicate"/>; otherwise, the first key/value pair in the dictionary that passes the test specified by <paramref name="predicate"/></returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the first key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pair passes the test specified by <paramref name="predicate"/>; otherwise, the first key/value pair in the dictionary that passes the test specified by <paramref name="predicate"/></returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary
    /// </summary>
    /// <returns>The value at the last position in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast();

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary
    /// </summary>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns>The value at the last position in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>The last key/value pair in the dictionary that passes the test in the predicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns>The last key/value pair in the dictionary that passes the test in the predicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the last key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault();

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary, or a default value if the dictionary contains no key/value pairs
    /// </summary>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty; otherwise, the last key/value pair in the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pairs pass the test in the predicate function; otherwise, the last key/value pair that passes the test in the predicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the last key/value pair of the dictionary that satisfies a condition or a default value if no such key/value pair is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <param name="keyComparer">A comparer to compare keys in the dictionary</param>
    /// <returns><c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary is empty or if no key/value pairs pass the test in the predicate function; otherwise, the last key/value pair that passes the test in the predicate function</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer);

    /// <summary>
    /// Continuously returns the maximum value in the dictionary
    /// </summary>
    /// <returns>The maximum value in the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveMax();

    /// <summary>
    /// Continuously invokes a transform function on each key/value pair of the dictionary and returns the maximum value
    /// </summary>
    /// <typeparam name="TResult">The type of the maximum value</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The maximum value in the dictionary</returns>
    IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously returns the minimum value in the dictionary
    /// </summary>
    /// <returns>The minimum value in the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveMin();

    /// <summary>
    /// Continuously invokes a transform function on each key/value pair of the dictionary and returns the minimum value
    /// </summary>
    /// <typeparam name="TResult">The type of the minimum value</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The minimum value in the dictionary</returns>
    IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Continuously filters the values of the dictionary based on a specified type
    /// </summary>
    /// <typeparam name="TResult">The type to filter the values of the dictionary on</typeparam>
    /// <returns>A dictionary that contains key/value pairs from this dictionary for which the values are of type <typeparamref name="TResult"/></returns>
    IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>();

    /// <summary>
    /// Generates a dictionary which actively projects each key/value pair of this dictionary into a key/value pair
    /// </summary>
    /// <typeparam name="TResultKey">The type of the keys in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <typeparam name="TResultValue">The type of the values in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair in this dictionary</param>
    /// <returns>An dictionary the key/value pairs of which are the result of invoking the transform function on each key/value pair in this dictionary</returns>
    IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, KeyValuePair<TResultKey, TResultValue>>> selector);

    /// <summary>
    /// Generates a dictionary which actively projects each key/value pair of this dictionary into a key/value pair
    /// </summary>
    /// <typeparam name="TResultKey">The type of the keys in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <typeparam name="TResultValue">The type of the values in the resulting <see cref="IObservableDictionaryQuery{TResultKey, TResultValue}"/></typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair in this dictionary</param>
    /// <param name="equalityComparer">The equality comparer to use to compare keys in the returned dictionary</param>
    /// <returns>An dictionary the key/value pairs of which are the result of invoking the transform function on each key/value pair in this dictionary</returns>
    IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, KeyValuePair<TResultKey, TResultValue>>> selector, IEqualityComparer<TResultKey> equalityComparer);

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary, and becomes faulted if there is not exactly one key/value pair in the dictionary
    /// </summary>
    /// <returns>The single key/value pair of the dictionary</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle();

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary that satisfies a specified condition, and becomes faulted if more than one such key/value pair exists
    /// </summary>
    /// <param name="predicate">A function to test an key/value pair for a condition</param>
    /// <returns>The single key/value pair of the dictionary that satisfies a condition</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary, or a default value if the dictionary is empty; becomes faulted if there is more than one key/value pair in the dictionary
    /// </summary>
    /// <returns>The single key/value pair of the dictionary, or <c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if the dictionary contains no key/value pairs</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault();

    /// <summary>
    /// Continuously returns the only key/value pair of the dictionary that satisfies a specified condition or a default value if no such key/value pair exists; becomes faulted if more than one key/value pair satisfies the condition
    /// </summary>
    /// <param name="predicate">A function to test an key/value pair for a condition</param>
    /// <returns>The single key/value pair of the dictionary that satisfies the condition, or <c>default</c>(<see cref="KeyValuePair{TKey, TValue}"/>) if no such key/value pair is found</returns>
    IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate);

    /// <summary>
    /// Continuously computes the sum of the values in the dictionary
    /// </summary>
    /// <returns>The sum of the values in the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveSum();

    /// <summary>
    /// Continuously computes the sum of the values that are obtained by invoking a transform function on each key/value pair of the dictionary
    /// </summary>
    /// <typeparam name="TResult">The type of the values being summed</typeparam>
    /// <param name="selector">A transform function to apply to each key/value pair</param>
    /// <returns>The sum of the projected values</returns>
    IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector);

    /// <summary>
    /// Creates a collection which contains the value of each key/value pair
    /// </summary>
    /// <returns>A collection which contains the value of each key/value pair</returns>
    IObservableCollectionQuery<TValue> ObserveToCollection();

    /// <summary>
    /// Creates a collection which continuously projects each key/value pair of the dictionary into a new form
    /// </summary>
    /// <param name="selector">A transform function to apply to each key/value pair to produce an element</param>
    /// <returns>A collection the elements of which are the result of invoking the transform function on each key/value pair of the dictionary</returns>
    IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Func<TKey, TValue, TElement> selector);

    /// <summary>
    /// Continuously returns the value for a specified key in the dictionary
    /// </summary>
    /// <param name="key">The key of the value to retrieve</param>
    /// <returns>The value for the specified key in the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveValueFor(TKey key);

    /// <summary>
    /// Continuously returns the value for a specified key in the dictionary or a default value if the key is not in the dictionary
    /// </summary>
    /// <param name="key">The key of the value to retrieve</param>
    /// <returns><c>default</c>(<typeparamref name="TValue"/>) if the key is not in the dictionary; otherwise, the value for the specified key in the dictionary</returns>
    IObservableScalarQuery<TValue> ObserveValueForOrDefault(TKey key);

    /// <summary>
    /// Continuously filters the dictionary based on a predicate
    /// </summary>
    /// <param name="predicate">A function to test each key/value pair for a condition</param>
    /// <returns>A dictionary that contains key/value pairs from this dictionary that satisfy the condition</returns>
    IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate);
}
