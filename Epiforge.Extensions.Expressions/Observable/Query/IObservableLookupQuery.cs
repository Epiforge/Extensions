namespace Epiforge.Extensions.Expressions.Observable.Query;

public interface IObservableLookupQuery<TKey, TElement> :
    IObservableCollectionQuery<IObservableGrouping<TKey, TElement>>,
    IObservableRangeDictionary<TKey, IObservableGrouping<TKey, TElement>>
    where TKey : notnull
{
}
