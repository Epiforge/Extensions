namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryReadOnlyList<TElement>(CollectionObserver collectionObserver, IReadOnlyList<TElement> readOnlyList, ObservableQuery? changeLockHolder) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    List<TElement>? copy;
    internal readonly IReadOnlyList<TElement> ReadOnlyList = readOnlyList;

    public override TElement this[int index] =>
        copy is { } kept ? kept[index] : ReadOnlyList[index];

    public override int Count =>
        copy?.Count ?? ReadOnlyList.Count;

    private protected override ObservableQuery? ChangeLockHolder =>
        changeLockHolder;

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        false;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (changeLockHolder is not null)
        {
            OnCollectionChanged(e);
            return;
        }
        using var changeHold = HoldOwnChanges();
        if (copy is null)
            OnCollectionChanged(e);
        else
            FollowChange(ref copy, e, ReadOnlyList);
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<TElement>)kept).GetEnumerator() : ReadOnlyList.GetEnumerator();

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (ReadOnlyList is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                if (copy is null)
                {
                    if (ReadOnlyList is INotifyPropertyChanging propertyChangingNotifier)
                        propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                    if (ReadOnlyList is INotifyPropertyChanged propertyChangedNotifier)
                        propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                }
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void OnInitialization()
    {
        if (ReadOnlyList is INotifyCollectionChanged collectionChangedNotifier)
        {
            if (changeLockHolder is null)
                copy = [.. ReadOnlyList];
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        }
        if (copy is null)
        {
            if (ReadOnlyList is INotifyPropertyChanging propertyChangingNotifier)
                propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
            if (ReadOnlyList is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        }
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyList<>.Count))
        {
            if (changeLockHolder is not null)
            {
                OnPropertyChanged(e);
                return;
            }
            using var changeHold = HoldOwnChanges();
            OnPropertyChanged(e);
        }
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyList<>.Count))
        {
            if (changeLockHolder is not null)
            {
                OnPropertyChanging(e);
                return;
            }
            using var changeHold = HoldOwnChanges();
            OnPropertyChanging(e);
        }
    }

    public override string ToString() =>
        $"read-only list of {typeof(TElement).FullName} elements (hash code {ReadOnlyList.GetHashCode()})";
}
