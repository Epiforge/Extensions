namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryList :
    ObservableCollectionQuery<object?>
{
    public ObservableCollectionQueryList(CollectionObserver collectionObserver, IList list) :
        base(collectionObserver) =>
        List = list;

    internal readonly IList List;

    public override object? this[int index] =>
        List[index];

    public override int Count =>
        List.Count;

    internal override bool HasEnumerationPenalty =>
        true;

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

    public override IEnumerator<object?> GetEnumerator() =>
        List.Cast<object?>().GetEnumerator();

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
        if (e.PropertyName == "Count")
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    void PropertyChangingNotifierPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == "Count")
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
