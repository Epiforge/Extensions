namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryEnumerable(CollectionObserver collectionObserver, IEnumerable enumerable) :
    ObservableCollectionQuery<object?>(collectionObserver)
{
    internal readonly IEnumerable Enumerable = enumerable;

    public override object? this[int index] =>
        Enumerable.Cast<object?>().ElementAt(index);

    public override int Count =>
        Enumerable.Cast<object?>().Count();

    internal override bool HasEnumerationPenalty =>
        true;

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

    public override IEnumerator<object?> GetEnumerator() =>
        Enumerable.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
    }

    public override string ToString() =>
        $"enumerable of object elements (hash code {Enumerable.GetHashCode()})";
}
