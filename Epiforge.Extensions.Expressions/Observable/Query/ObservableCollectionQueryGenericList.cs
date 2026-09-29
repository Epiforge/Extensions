namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericList<TElement>(CollectionObserver collectionObserver, IList<TElement> list) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    List<TElement>? copy;
    internal readonly IList<TElement> List = list;

    public override TElement this[int index] =>
        copy is { } kept ? kept[index] : List[index];

    public override int Count =>
        copy?.Count ?? List.Count;

    internal override bool HasEnumerationPenalty =>
        false;

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

    public override IEnumerator<TElement> GetEnumerator() =>
        copy is { } kept ? ((IEnumerable<TElement>)kept).GetEnumerator() : List.GetEnumerator();

    protected override void OnInitialization()
    {
        if (List is INotifyCollectionChanged collectionChangedNotifier)
        {
            copy = [.. List];
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
        if (e.PropertyName == nameof(IList<>.Count))
        {
            using var changeHold = HoldOwnChanges();
            OnPropertyChanged(e);
        }
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IList<>.Count))
        {
            using var changeHold = HoldOwnChanges();
            OnPropertyChanging(e);
        }
    }

    public override string ToString() =>
        $"list of {typeof(TElement).FullName} elements (hash code {List.GetHashCode()})";
}
