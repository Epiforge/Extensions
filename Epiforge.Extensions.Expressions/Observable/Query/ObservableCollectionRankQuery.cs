namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Counts the elements of an ordering which sort ahead of an element and those which sort level with it
/// </summary>
/// <remarks>
/// The counts are re-read whenever the ordering gains, loses or resets elements, and whenever a key changes. A move is ignored because the ordering moves an element only when its key changed, which the key change already reports; and a key change must be heard even when nothing moves, because a key which comes to equal a neighbor's changes how many are level with it without changing where anything sits
/// </remarks>
sealed class ObservableCollectionRankQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionOrderByQuery<TElement> orderByQuery, TElement element) :
    ObservableScalarQuery<(int Ahead, int Tied)>(collectionObserver)
{
    internal readonly TElement Element = element;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = orderByQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                orderByQuery.CollectionChanged -= OrderByQueryCollectionChanged;
                orderByQuery.KeysChanged -= OrderByQueryKeysChanged;
                orderByQuery.PropertyChanged -= OrderByQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = orderByQuery.RankOf(Element);

    protected override void OnInitialization()
    {
        orderByQuery.CollectionChanged += OrderByQueryCollectionChanged;
        orderByQuery.KeysChanged += OrderByQueryKeysChanged;
        orderByQuery.PropertyChanged += OrderByQueryPropertyChanged;
        Evaluate();
    }

    void OrderByQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Move)
            Evaluate();
    }

    void OrderByQueryKeysChanged(object? sender, EventArgs e) =>
        Evaluate();

    void OrderByQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }

    public override string ToString() =>
        $"rank of {Element} in {orderByQuery}";
}
