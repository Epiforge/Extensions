namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAllQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, bool>> predicate) :
    ObservableCollectionScalarQuery<TElement, bool>(collectionObserver, observableCollectionQuery)
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TElement>? unmatched;

    internal readonly Expression<Func<TElement, bool>> Predicate = predicate;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                unmatched!.CollectionChanged -= UnmatchedCollectionChanged;
                unmatched.PropertyChanged -= UnmatchedPropertyChanged;
                unmatched.Dispose();
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = unmatched!.OperationFault is { } unmatchedFault ? (unmatchedFault, default) : (null, unmatched.Count == 0);

    protected override void OnInitialization()
    {
        unmatched = observableCollectionQuery.ObserveWhere(Expression.Lambda<Func<TElement, bool>>(Expression.Not(Predicate.Body), Predicate.Parameters));
        unmatched.CollectionChanged += UnmatchedCollectionChanged;
        unmatched.PropertyChanged += UnmatchedPropertyChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"all {observableCollectionQuery} matching {Predicate}";

    void UnmatchedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();

    void UnmatchedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }
}
