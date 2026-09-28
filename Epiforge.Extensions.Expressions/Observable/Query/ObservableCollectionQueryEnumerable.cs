namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryEnumerable(CollectionObserver collectionObserver, IEnumerable enumerable) :
    ObservableCollectionQuery<object?>(collectionObserver)
{
    List<object?>? copy;
    internal readonly IEnumerable Enumerable = enumerable;

    public override object? this[int index] =>
        copy is { } kept ? kept[index] : Enumerable.Cast<object?>().ElementAt(index);

    public override int Count =>
        copy?.Count ?? Enumerable.Cast<object?>().Count();

    internal override bool HasEnumerationPenalty =>
        true;

    internal override bool HasIndexerPenalty =>
        true;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        FollowChange(ref copy, e, Enumerable.Cast<object?>());
    }

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
        copy is { } kept ? kept.GetEnumerator() : Enumerable.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. Enumerable.Cast<object?>()];
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
    }

    public override string ToString() =>
        $"enumerable of object elements (hash code {Enumerable.GetHashCode()})";
}
