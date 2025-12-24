namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericEnumerable<TElement>(CollectionObserver collectionObserver, IEnumerable<TElement> enumerable) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    internal readonly IEnumerable<TElement> Enumerable = enumerable;

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

    public override string ToString() =>
        $"enumerable of {typeof(TElement).FullName} elements (hash code {Enumerable.GetHashCode()})";
}