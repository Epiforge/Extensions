namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionIndexQuery<TElement> :
    ObservableCollectionScalarQuery<TElement, TElement>
{
    public ObservableCollectionIndexQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Index? index, bool outOfRangeIsDefault) :
        base(collectionObserver, observableCollectionQuery)
    {
        Index = index;
        OutOfRangeIsDefault = outOfRangeIsDefault;
    }

    internal readonly Index? Index;
    internal readonly bool OutOfRangeIsDefault;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        if (observableCollectionQuery.OperationFault is { } fault)
            Evaluation = (fault, default!);
        else
        {
            try
            {
                if (Index is { } index)
                {
                    if (observableCollectionQuery.HasIndexerPenalty && (!index.IsFromEnd || index.Value == 1))
                    {
                        if (index.IsFromEnd)
                            Evaluation = (null, OutOfRangeIsDefault ? observableCollectionQuery.LastOrDefault() : observableCollectionQuery.Last())!;
                        else if (index.Value == 1)
                            Evaluation = (null, OutOfRangeIsDefault ? observableCollectionQuery.FirstOrDefault() : observableCollectionQuery.First())!;
                        else
                            Evaluation = (null, OutOfRangeIsDefault ? observableCollectionQuery.ElementAtOrDefault(index.Value) : observableCollectionQuery.ElementAt(index.Value))!;
                    }
                    else
                    {
                        var offset = index.GetOffset(observableCollectionQuery.Count);
                        if (offset < 0 || offset >= observableCollectionQuery.Count)
                            Evaluation = OutOfRangeIsDefault ? (null, default!) : (new IndexOutOfRangeException(), default!);
                        else
                            Evaluation = (null, observableCollectionQuery[offset]);
                    }
                }
                else if (observableCollectionQuery.HasIndexerPenalty)
                    Evaluation = (null, OutOfRangeIsDefault ? observableCollectionQuery.SingleOrDefault() : observableCollectionQuery.Single())!;
                else
                    Evaluation = observableCollectionQuery.Count switch
                    {
                        0 when OutOfRangeIsDefault => (null, default!),
                        0 => (ExceptionHelper.SequenceContainsNoElements, default!),
                        1 => (null, observableCollectionQuery[0]),
                        _ => (ExceptionHelper.SequenceContainsMoreThanOneElement, default!)
                    };
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
}
