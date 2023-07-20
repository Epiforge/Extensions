namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents an observer of collections
/// </summary>
public interface ICollectionObserver
{
    /// <summary>
    /// Gets the number of cached observable queries
    /// </summary>
    int CachedObservableQueries { get; }

    /// <summary>
    /// Gets the expression observer used to observe the elements of collections
    /// </summary>
    IExpressionObserver ExpressionObserver { get; }

    /// <summary>
    /// Observes the specified enumerable
    /// </summary>
    /// <param name="collection">The collection to observe</param>
    IObservableCollectionQuery<object?> ObserveEnumerable(IEnumerable collection);

    /// <summary>
    /// Observes the specified list
    /// </summary>
    /// <param name="collection">The list to observe</param>
    IObservableCollectionQuery<object?> ObserveList(IList collection);

    /// <summary>
    /// Observes the specified generic enumerable
    /// </summary>
    /// <typeparam name="TElement">The type of elements being enumerated</typeparam>
    /// <param name="enumerable">The enumerable to observe</param>
    IObservableCollectionQuery<TElement> ObserveEnumerable<TElement>(IEnumerable<TElement> enumerable);

    /// <summary>
    /// Observes the specified generic list
    /// </summary>
    /// <typeparam name="TElement">The type of elements in the list</typeparam>
    /// <param name="list">The list to observe</param>
    IObservableCollectionQuery<TElement> ObserveList<TElement>(IList<TElement> list);

    /// <summary>
    /// Observes the specified read-only list
    /// </summary>
    /// <typeparam name="TElement">The type of elements in the read-only list</typeparam>
    /// <param name="readOnlyList">The read-only list to observe</param>
    IObservableCollectionQuery<TElement> ObserveReadOnlyList<TElement>(IReadOnlyList<TElement> readOnlyList);

    /// <summary>
    /// Observes the specified read-only dictionary
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary</typeparam>
    /// <param name="dictionary">The read-only dictionary to observe</param>
    IObservableDictionaryQuery<TKey, TValue> Observe<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary);
}