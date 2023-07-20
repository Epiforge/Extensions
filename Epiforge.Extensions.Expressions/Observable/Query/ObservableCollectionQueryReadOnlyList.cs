namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryReadOnlyList<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionQueryReadOnlyList(CollectionObserver collectionObserver, IReadOnlyList<TElement> readOnlyList) :
        base(collectionObserver) =>
        ReadOnlyList = readOnlyList;

    internal readonly IReadOnlyList<TElement> ReadOnlyList;

    public override TElement this[int index] =>
        ReadOnlyList[index];

    public override int Count =>
        ReadOnlyList.Count;

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        false;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    public override IEnumerator<TElement> GetEnumerator() =>
        ReadOnlyList.GetEnumerator();

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (ReadOnlyList is INotifyPropertyChanging propertyChangingNotifier)
                    propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                if (ReadOnlyList is INotifyPropertyChanged propertyChangedNotifier)
                    propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                if (ReadOnlyList is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void OnInitialization()
    {
        if (ReadOnlyList is INotifyPropertyChanging propertyChangingNotifier)
            propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
        if (ReadOnlyList is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        if (ReadOnlyList is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyList<TElement>.Count))
            OnPropertyChanged(e);
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyList<TElement>.Count))
            OnPropertyChanging(e);
    }
}
