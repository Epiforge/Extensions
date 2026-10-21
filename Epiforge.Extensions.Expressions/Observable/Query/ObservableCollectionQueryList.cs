namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryList(CollectionObserver collectionObserver, IList list) :
    ObservableCollectionQuery<object?>(collectionObserver)
{
    /// <summary>
    /// The handler subscribed to the collection, which is the one unsubscribed, since unsubscribing another made from the same method would allocate it
    /// </summary>
    NotifyCollectionChangedEventHandler? collectionChangedHandler;
    List<object?>? copy;
    internal readonly IList List = list;

    public override object? this[int index] =>
        copy is { } kept ? kept[index] : List[index];

    public override int Count =>
        copy?.Count ?? List.Count;

    internal override bool HasEnumerationPenalty =>
        true;

    internal override bool HasIndexerPenalty =>
        false;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var changeHold = HoldOwnChanges();
        FollowChange(ref copy, e, List);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (List is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= collectionChangedHandler;
                else
                {
                    if (List is INotifyPropertyChanging propertyChangingNotifier)
                        propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                    if (List is INotifyPropertyChanged propertyChangedNotifier)
                        propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                }
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<object?> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<object?>)kept).GetEnumerator() : List.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (List is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. List.Cast<object?>()];
            collectionChangedHandler = CollectionChangedNotifierCollectionChanged;
            collectionChangedNotifier.CollectionChanged += collectionChangedHandler;
        }
        else
        {
            if (List is INotifyPropertyChanging propertyChangingNotifier)
                propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
            if (List is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        }
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Count")
        {
            using var changeHold = HoldOwnChanges();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == "Count")
        {
            using var changeHold = HoldOwnChanges();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public override string ToString() =>
        $"list of object elements (hash code {List.GetHashCode()})";

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
