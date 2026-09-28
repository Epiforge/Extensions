namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryList(CollectionObserver collectionObserver, IList list) :
    ObservableCollectionQuery<object?>(collectionObserver)
{
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
        FollowChange(ref copy, e, List.Cast<object?>());
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (List is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
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
        copy is { } kept ? kept.GetEnumerator() : List.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (List is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. List.Cast<object?>()];
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
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
}
