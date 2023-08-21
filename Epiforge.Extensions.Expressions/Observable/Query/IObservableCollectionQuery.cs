namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents an observable collection which can be used to generate observable queries
/// </summary>
/// <typeparam name="TElement">The type of elements in the collection</typeparam>
public interface IObservableCollectionQuery<TElement> :
    ICollection,
    INotifyCollectionChanged,
    IObservableQuery,
    IReadOnlyList<TElement>
{
    /// <summary>
    /// Gets the number of elements contained in the <see cref="IObservableCollectionQuery{TElement}"/>
    /// </summary>
    new int Count { get; }

    /// <summary>
    /// Gets the exception encountered while performing the operation
    /// </summary>
    Exception? OperationFault { get; }

    /// <summary>
    /// Continuously applies an accumulator function to each element of the collection
    /// </summary>
    /// <typeparam name="TAccumulate">The type of the accumulator value</typeparam>
    /// <typeparam name="TResult">The type of the resulting value</typeparam>
    /// <param name="seedFactory">A method to produce the initial accumulator value when the collection changes</param>
    /// <param name="func">An accumulator method to be invoked on each element</param>
    /// <param name="resultSelector">A method to transform the final accumulator value into the result value</param>
    /// <returns>The transformed final accumulator value</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TElement, TAccumulate> func, Func<TAccumulate, TResult> resultSelector);

    /// <summary>
    /// Continuously determines whether all elements of the collection satisfy a condition
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>true</c> when every element of the collection passes the test in the specified predicate, or if the collection is empty; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAll(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously determines whether the collection contains any elements
    /// </summary>
    /// <returns>><c>true</c> if the collection contains any elements; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAny();

    /// <summary>
    /// Continuously determines whether any element of the collection satisfies a condition
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>true</c> if any elements in the collection pass the test in the specified predicate; otherwise, <c>false</c></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<bool> ObserveAny(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously computes the average of the values in the collection
    /// </summary>
    /// <returns>The average value of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveAverage();

    /// <summary>
    /// Continuously computes the average of the values that are obtained by invoking a transform function on each element of the collection
    /// </summary>
    /// <typeparam name="TResult">The type of the values being averaged</typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>The average value of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TElement, TResult>> selector);

    /// <summary>
    /// Continuously casts the elements of the collection to the specified type
    /// </summary>
    /// <typeparam name="TResult">The type to cast the elements</typeparam>
    /// <returns>Each element of the collection cast to the specified type</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TResult> ObserveCast<TResult>();

    /// <summary>
    /// Continuously concatenates a second collection to the collection
    /// </summary>
    /// <param name="second">The collection to concatenate to this collection</param>
    /// <returns>The concatenated elements of this and the specified collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveConcat(IObservableCollectionQuery<TElement> second);

    /// <summary>
    /// Continuously determines the number of elements in the collection
    /// </summary>
    /// <returns>The number of elements in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<int> ObserveCount();

    /// <summary>
    /// Continuously determines the number of elements in the collection which satisfy a condition
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns>The number of elements in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<int> ObserveCount(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously returns distinct elements from the collection by using the default equality comparer to compare values
    /// </summary>
    /// <returns>The distinct elements from the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveDistinct();

    /// <summary>
    /// Continuously returns distinct elements from the collection by using a specified <see cref="IEqualityComparer{T}"/> to compare values
    /// </summary>
    /// <param name="comparer">An <see cref="IEqualityComparer{T}"/> to compare values</param>
    /// <returns>The distinct elements from the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveDistinct(IEqualityComparer<TElement> comparer);

    /// <summary>
    /// Continuously returns the element at a specified index in the collection
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve</param>
    /// <returns>>The element at the specified position in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveElementAt(int index);

    /// <summary>
    /// Continuously returns the element at a specified index in the collection or a default value if the index is out of range
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve</param>
    /// <returns><c>default</c>(<typeparamref name="TElement"/>) if the index is outside the bounds of the collection; otherwise, the element at the specified position in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveElementAtOrDefault(int index);

    /// <summary>
    /// Continuously returns the first element of the collection
    /// </summary>
    /// <returns>The first element in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveFirst();

    /// <summary>
    /// Continuously returns the first element in the collection that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns>The first element in the collection that passes the test in the spredicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveFirst(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously returns the first element of the collection, or a default value if the collection contains no elements
    /// </summary>
    /// <returns><c>default</c>(<typeparamref name="TElement"/>) if the collection is empty; otherwise, the first element in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveFirstOrDefault();

    /// <summary>
    /// Continuously returns the first element of the collection that satisfies a condition or a default value if no such element is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>default</c>(<typeparamref name="TElement"/>) if the collection is empty or if no element passes the test specified by <paramref name="predicate"/>; otherwise, the first element in the collection that passes the test specified by <paramref name="predicate"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveFirstOrDefault(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Creates a collection that is kept consistent with this collection, translating batch collection change notifications into individual element change notifications
    /// </summary>
    /// <returns>A collection kept consistent with this collection which raises collection change notifications only for individual element changes</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveIndividualChanges();

    /// <summary>
    /// Continuously groups the elements of the collection according to a specified key selector function
    /// </summary>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/></typeparam>
    /// <param name="keySelector">A function to extract the key for each element</param>
    /// <returns>A collection where each element is an <see cref="IObservableGrouping{TKey, TElement}"/> object which contains a sequence of objects and a key</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector);

    /// <summary>
    /// Continuously groups the elements of the collection according to a specified key selector function
    /// </summary>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/></typeparam>
    /// <param name="keySelector">A function to extract the key for each element</param>
    /// <param name="keyEqualityComparer">An <see cref="IEqualityComparer{T}"/> to compare keys</param>
    /// <returns>A collection where each element is an <see cref="IObservableGrouping{TKey, TElement}"/> object which contains a sequence of objects and a key</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<IObservableGrouping<TKey, TElement>> ObserveGroupBy<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> keyEqualityComparer);

    /// <summary>
    /// Continuously returns the last element of the collection
    /// </summary>
    /// <returns>The value at the last position in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveLast();

    /// <summary>
    /// Continuously returns the last element of the collection that satisfies a specified condition
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns>The last element in the collection that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveLast(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously returns the last element of the collection, or a default value if the collection contains no elements
    /// </summary>
    /// <returns><c>default</c>(<typeparamref name="TElement"/>) if the collection is empty; otherwise, the last element in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveLastOrDefault();

    /// <summary>
    /// Continuously returns the last element of the collection that satisfies a condition or a default value if no such element is found
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns><c>default</c>(<typeparamref name="TElement"/>) if the collection is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveLastOrDefault(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously returns the maximum value in the collection
    /// </summary>
    /// <returns>The maximum value in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveMax();

    /// <summary>
    /// Continuously invokes a transform function on each element of the collection and returns the maximum value
    /// </summary>
    /// <typeparam name="TResult">The type of the maximum value</typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>The maximum value in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TElement, TResult>> selector);

    /// <summary>
    /// Continuously returns the minimum value in the collection
    /// </summary>
    /// <returns>The minimum value in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveMin();

    /// <summary>
    /// Continuously invokes a transform function on each element of the collection and returns the minimum value
    /// </summary>
    /// <typeparam name="TResult">The type of the minimum value</typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>The minimum value in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TElement, TResult>> selector);

    /// <summary>
    /// Continuously filters the elements of the collection based on a specified type
    /// </summary>
    /// <typeparam name="TResult">The type to filter the elements of the collection on</typeparam>
    /// <returns>A collection that contains elements from this collection of type <typeparamref name="TResult"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TResult> ObserveOfType<TResult>();

    /// <summary>
    /// Continuously sorts the elements of the collection in ascending order according to a key
    /// </summary>
    /// <param name="selector">An expression to extract a key from an element</param>
    /// <returns>A collection whose elements are sorted according to the key selector</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector);

    /// <summary>
    /// Continuously sorts the elements of the collection in the specified order according to a key
    /// </summary>
    /// <param name="selector">An expression to extract a key from an element</param>
    /// <param name="isDescending"><c>true</c> to sort in descending order; otherwise, sort in ascending order</param>
    /// <returns>A collection whose elements are sorted according to the key selector in descending order</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveOrderBy(Expression<Func<TElement, IComparable>> selector, bool isDescending);

    /// <summary>
    /// Continuously sorts the elements of the collection according to a series of key selectors and directions
    /// </summary>
    /// <param name="selectorsAndDirections">A series of key selectors and directions, the position of each determining its ordering priority</param>
    /// <returns>A collection whose elements are sorted according to key selectors and directions</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveOrderBy(params (Expression<Func<TElement, IComparable>> selector, bool isDescending)[] selectorsAndDirections);

    /// <summary>
    /// Continuously projects each element of the collection into a new form
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector"/></typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>A collection the elements of which are the result of invoking the transform function on each element of this collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TResult> ObserveSelect<TResult>(Expression<Func<TElement, TResult>> selector);

    /// <summary>
    /// Continuously projects each element of the collection to an <see cref="IEnumerable{TResult}"/> and flattens the resulting sequences into one collection
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector"/></typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>An colelction the elements of which are the result of invoking the one-to-many transform function on each element of this collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TResult> ObserveSelectMany<TResult>(Expression<Func<TElement, IEnumerable<TResult>>> selector);

    /// <summary>
    /// Continuously returns the only element of the collection, and becomes faulted if there is not exactly one element in the collection
    /// </summary>
    /// <returns>The single element of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveSingle();

    /// <summary>
    /// Continuously returns the only element of the collection that satisfies a specified condition, and becomes faulted if more than one such element exists
    /// </summary>
    /// <param name="predicate">A function to test an element for a condition</param>
    /// <returns>The single element of the collection that satisfies a condition</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveSingle(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously returns the only element of the collection, or a default value if the collection is empty; becomes faulted if there is more than one element in the collection
    /// </summary>
    /// <returns>The single element of the collection, or <c>default</c>(<typeparamref name="TElement"/>) if the collection contains no elements</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveSingleOrDefault();

    /// <summary>
    /// Continuously returns the only element of the collection that satisfies a specified condition or a default value if no such element exists; becomes faulted if more than one element satisfies the condition
    /// </summary>
    /// <param name="predicate">A function to test an element for a condition</param>
    /// <returns>The single element of the collection that satisfies the condition, or <c>default</c>(<typeparamref name="TElement"/>) if no such element is found</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveSingleOrDefault(Expression<Func<TElement, bool>> predicate);

    /// <summary>
    /// Continuously computes the sum of the values in the collection
    /// </summary>
    /// <returns>The sum of the values in the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TElement> ObserveSum();

    /// <summary>
    /// Continuously computes the sum of the values that are obtained by invoking a transform function on each element of the collection
    /// </summary>
    /// <typeparam name="TResult">The type of the values being summed</typeparam>
    /// <param name="selector">A transform function to apply to each element</param>
    /// <returns>The sum of the projected values</returns>
    [return: DisposeWhenDiscarded]
    IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TElement, TResult>> selector);

    /// <summary>
    /// Creates a dictionary which continuously projects each element of the collection into a key/value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the <see cref="IObservableDictionaryQuery{TKey, TElement}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each element to produce a key for it</param>
    /// <returns>A dictionary the key/value pairs of which are the result of invoking the transform function on each element of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector)
        where TKey : notnull;

    /// <summary>
    /// Creates a dictionary which continuously projects each element of the collection into a key/value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the <see cref="IObservableDictionaryQuery{TKey, TElement}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each element to produce a key for it</param>
    /// <param name="equalityComparer">The equality comparer to use to compare keys in the dictionary</param>
    /// <returns>A dictionary the key/value pairs of which are the result of invoking the transform function on each element of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TElement> ObserveToDictionary<TKey>(Expression<Func<TElement, TKey>> keySelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull;

    /// <summary>
    /// Creates a dictionary which continuously projects each element of the collection into a key/value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the <see cref="IObservableDictionaryQuery{TKey, TValue}"/></typeparam>
    /// <typeparam name="TValue">The type of the values in the <see cref="IObservableDictionaryQuery{TKey, TValue}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each element to produce a key for it</param>
    /// <param name="valueSelector">A transform function to apply to each element to produce a value for it</param>
    /// <returns>A dictionary the key/value pairs of which are the result of invoking the transform functions on each element of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector)
        where TKey : notnull;

    /// <summary>
    /// Creates a dictionary which continuously projects each element of the collection into a key/value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the <see cref="IObservableDictionaryQuery{TKey, TValue}"/></typeparam>
    /// <typeparam name="TValue">The type of the values in the <see cref="IObservableDictionaryQuery{TKey, TValue}"/></typeparam>
    /// <param name="keySelector">A transform function to apply to each element to produce a key for it</param>
    /// <param name="valueSelector">A transform function to apply to each element to produce a value for it</param>
    /// <param name="equalityComparer">The equality comparer to use to compare keys in the dictionary</param>
    /// <returns>A dictionary the key/value pairs of which are the result of invoking the transform functions on each element of the collection</returns>
    [return: DisposeWhenDiscarded]
    IObservableDictionaryQuery<TKey, TValue> ObserveToDictionary<TKey, TValue>(Expression<Func<TElement, TKey>> keySelector, Expression<Func<TElement, TValue>> valueSelector, IEqualityComparer<TKey> equalityComparer)
        where TKey : notnull;

    /// <summary>
    /// Creates a collection that is kept consistent with this collection via a mechanism other than a simple lock (use <see cref="ObserveUsingSynchronizationCallbackEventually(object, CollectionSynchronizationCallback)"/> instead when this method may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="context">An object that is passed to the callback</param>
    /// <param name="synchronizationCallback">The callback that is invoked whenever access to the collection is required; you can use it to ensure that the collection is accessed by one thread at a time</param>
    /// <returns>A collection that is kept consistent with this collection via a mechanism other than a simple lock</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallback(object context, CollectionSynchronizationCallback synchronizationCallback);

    /// <summary>
    /// Creates a collection that is eventually kept consistent with this collection via a mechanism other than a simple lock (use this method instead of <see cref="ObserveUsingSynchronizationCallback(object, CollectionSynchronizationCallback)"/> when the same may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="context">An object that is passed to the callback</param>
    /// <param name="synchronizationCallback">The callback that is invoked whenever access to the collection is required; you can use it to ensure that the collection is accessed by one thread at a time</param>
    /// <returns>A collection that is kept consistent with this collection via a mechanism other than a simple lock</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSynchronizationCallbackEventually(object context, CollectionSynchronizationCallback synchronizationCallback);

    /// <summary>
    /// Creates a collection that is kept consistent on a specified <see cref="SynchronizationContext"/> with this collection (use <see cref="ObserveUsingSynchronizationContextEventually(SynchronizationContext)"/> instead when this method may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> on which to perform consistency operations</param>
    /// <returns>A collection that is kept consistent with this collection on <paramref name="synchronizationContext"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContext(SynchronizationContext synchronizationContext);

    /// <summary>
    /// Creates collection that is eventually made consistent on a specified <see cref="SynchronizationContext"/> with this collection (use this method instead of <see cref="ObserveUsingSynchronizationContext(SynchronizationContext)"/> when the same may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> on which to perform consistency operations</param>
    /// <returns>A collection that is eventually made consistent with this collection on <paramref name="synchronizationContext"/></returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSynchronizationContextEventually(SynchronizationContext synchronizationContext);

    /// <summary>
    /// Creates a collection that is kept consistent with this collection via monitored access to the specified lock object (use <see cref="ObserveUsingSyncRootEventually(object)"/> instead when this method may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="lockObject">The lock object</param>
    /// <returns>A collection that is kept consistent with this collection via monitored access to the specified lock object</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSyncRoot(object lockObject);

    /// <summary>
    /// Creates a collection that is eventually kept consistent with this collection via monitored access to the specified lock object (use this method instead of <see cref="ObserveUsingSyncRoot(object)"/> when the same may produce a deadlock and/or only eventual consistency is required)
    /// </summary>
    /// <param name="lockObject">The lock object</param>
    /// <returns>A collection that is kept consistent with this collection via monitored access to the specified lock object</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveUsingSyncRootEventually(object lockObject);

    /// <summary>
    /// Continuously filters the collection based on a predicate
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition</param>
    /// <returns>A collection that contains elements from this collection that satisfy the condition</returns>
    [return: DisposeWhenDiscarded]
    IObservableCollectionQuery<TElement> ObserveWhere(Expression<Func<TElement, bool>> predicate);
}
