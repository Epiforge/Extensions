namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionIndexForCountQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Func<int, int> indexForCount) :
    ObservableCollectionScalarQuery<TElement, TElement>(collectionObserver, observableCollectionQuery)
{
    internal readonly Func<int, int> IndexForCount = indexForCount;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    [SuppressMessage("Performance", "CA1826: Do not use Enumerable methods on indexable collections")]
    void Evaluate()
    {
        if (observableCollectionQuery.OperationFault is { } fault)
            Evaluation = (fault, default!);
        else
        {
            try
            {
                var count = observableCollectionQuery.Count;
                if (count == 0)
                    Evaluation = (ExceptionHelper.SequenceContainsNoElements, default!);
                else
                {
                    var index = IndexForCount(count);
                    if (index < 0 || index >= count)
                        Evaluation = (new IndexOutOfRangeException(), default!);
                    else
                        Evaluation = (null, observableCollectionQuery.HasIndexerPenalty ? observableCollectionQuery.ElementAt(index) : observableCollectionQuery[index]);
                }
            }
            catch (Exception ex)
            {
                Evaluation = (ex, default!);
            }
        }
    }

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        Evaluate();
    }

    public override string ToString() =>
        $"element at a position computed from the count of {observableCollectionQuery}";
}
