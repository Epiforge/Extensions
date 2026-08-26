namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents a collection of keys each continuously mapped to one or more values
/// </summary>
/// <typeparam name="TKey">The type of the keys</typeparam>
/// <typeparam name="TElement">The type of the elements grouped under each key</typeparam>
public interface IObservableLookupQuery<TKey, TElement> :
    IObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IReadOnlyObservableRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>
    where TKey : notnull
{
}
