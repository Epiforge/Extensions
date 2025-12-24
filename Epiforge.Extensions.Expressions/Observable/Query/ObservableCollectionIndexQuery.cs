namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionIndexQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Index? index, bool outOfRangeIsDefault) :
    ObservableCollectionScalarQuery<TElement, TElement>(collectionObserver, observableCollectionQuery)
{
    internal readonly Index? Index = index;
    internal readonly bool OutOfRangeIsDefault = outOfRangeIsDefault;

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

    public override string ToString() =>
        $"{Index switch
        {
            { Value: 0, IsFromEnd: false } => "first",
            { IsFromEnd: true } => "last",
            null => "single",
            { Value: var indexOffset } => $"position {indexOffset}",
        }} in {observableCollectionQuery} or {(OutOfRangeIsDefault ? "default" : "fault")} if out of range";
}
