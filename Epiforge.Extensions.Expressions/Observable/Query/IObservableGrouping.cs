namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents a collection of objects that have a common key
/// </summary>
/// <typeparam name="TKey">The type of the key of the <see cref="IObservableGrouping{TKey, TElement}"/></typeparam>
/// <typeparam name="TElement">The type of the values of the <see cref="IObservableGrouping{TKey, TElement}"/></typeparam>
public interface IObservableGrouping<TKey, TElement> :
    IObservableCollectionQuery<TElement>
{
    /// <summary>
    /// The key of the <see cref="IObservableGrouping{TKey, TElement}"/>
    /// </summary>
    TKey Key { get; }
}
