namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericEnumerable<TElement>(CollectionObserver collectionObserver, IEnumerable<TElement> enumerable) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    List<TElement>? copy;
    internal readonly IEnumerable<TElement> Enumerable = enumerable;

    public override TElement this[int index] =>
        copy is { } kept ? kept[index] : Enumerable.ElementAt(index);

    public override int Count =>
        copy?.Count ?? Enumerable.Count();

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        true;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        FollowChange(ref copy, e, Enumerable);
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

    public override IEnumerator<TElement> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<TElement>)kept).GetEnumerator() : Enumerable.GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. Enumerable];
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
    }

    public override string ToString() =>
        $"enumerable of {typeof(TElement).FullName} elements (hash code {Enumerable.GetHashCode()})";
}