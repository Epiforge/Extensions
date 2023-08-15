namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericList<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionQueryGenericList(CollectionObserver collectionObserver, IList<TElement> list) :
        base(collectionObserver) =>
        List = list;

    internal readonly IList<TElement> List;

    public override TElement this[int index] =>
        List[index];

    public override int Count =>
        List.Count;

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        false;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (List is INotifyPropertyChanging propertyChangingNotifier)
                    propertyChangingNotifier.PropertyChanging -= PropertyChangingNotifierPropertyChanging;
                if (List is INotifyPropertyChanged propertyChangedNotifier)
                    propertyChangedNotifier.PropertyChanged -= PropertyChangedNotifierPropertyChanged;
                if (List is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        List.GetEnumerator();

    protected override void OnInitialization()
    {
        if (List is INotifyPropertyChanging propertyChangingNotifier)
            propertyChangingNotifier.PropertyChanging += PropertyChangingNotifierPropertyChanging;
        if (List is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += PropertyChangedNotifierPropertyChanged;
        if (List is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
    }

    void PropertyChangedNotifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IList<TElement>.Count))
            OnPropertyChanged(e);
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(IList<TElement>.Count))
            OnPropertyChanging(e);
    }

    public override string ToString() =>
        $"list of {typeof(TElement).FullName} elements (hash code {List.GetHashCode()})";
}
