namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryEnumerable(CollectionObserver collectionObserver, IEnumerable enumerable) :
    ObservableCollectionQuery<object?>(collectionObserver)
{
    /// <summary>
    /// The handler subscribed to the collection, which is the one unsubscribed, since unsubscribing another made from the same method would allocate it
    /// </summary>
    NotifyCollectionChangedEventHandler? collectionChangedHandler;
    List<object?>? copy;
    internal readonly IEnumerable Enumerable = enumerable;

    public override object? this[int index] =>
        copy is { } kept ? kept[index] : Enumerable.Cast<object?>().ElementAt(index);

    private protected override bool AnnouncesCount =>
        copy is not null;

    private protected override int CurrentCount =>
        copy?.Count ?? Enumerable.Cast<object?>().Count();

    internal override bool HasEnumerationPenalty =>
        true;

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
                    collectionChangedNotifier.CollectionChanged -= collectionChangedHandler;
            }
            return removedFromCache;
        }
        return true;

    }

    public override IEnumerator<object?> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<object?>)kept).GetEnumerator() : Enumerable.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. Enumerable.Cast<object?>()];
            collectionChangedHandler = CollectionChangedNotifierCollectionChanged;
            collectionChangedNotifier.CollectionChanged += collectionChangedHandler;
        }
    }

    public override string ToString() =>
        $"enumerable of object elements (hash code {Enumerable.GetHashCode()})";

    private protected override bool TryCopyTo(object?[] array, int arrayIndex)
    {
        if (copy is not { } kept)
            return base.TryCopyTo(array, arrayIndex);
        if (array.Length - arrayIndex < kept.Count)
            return false;
        kept.CopyTo(array, arrayIndex);
        return true;
    }
}
