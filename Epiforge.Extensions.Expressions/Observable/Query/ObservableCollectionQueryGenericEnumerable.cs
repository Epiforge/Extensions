namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericEnumerable<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionQueryGenericEnumerable(CollectionObserver collectionObserver, IEnumerable<TElement> enumerable) :
        base(collectionObserver) =>
        Enumerable = enumerable;

    internal readonly IEnumerable<TElement> Enumerable;

    public override TElement this[int index] =>
        Enumerable.ElementAt(index);

    public override int Count =>
        Enumerable.Count();

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        true;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        Enumerable.GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
    }
}