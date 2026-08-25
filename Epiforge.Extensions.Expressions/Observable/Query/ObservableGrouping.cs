namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableGrouping<TKey, TElement>(CollectionObserver collectionObserver, TKey key, ObservableCollectionQuery<TElement> groupQuery) :
    ObservableCollectionQuery<TElement>(collectionObserver),
    IObservableGrouping<TKey, TElement>
{
    bool ownerDisposing;

    public override TElement this[int index] =>
        groupQuery[index];

    public override int Count =>
        groupQuery.Count;

    public TKey Key { get; } = key;

    protected override bool Dispose(bool disposing)
    {
        if (!ownerDisposing)
            return false;
        if (disposing)
        {
            groupQuery.CollectionChanged -= GroupQueryCollectionChanged;
            groupQuery.PropertyChanging -= GroupQueryPropertyChanging;
            groupQuery.PropertyChanged -= GroupQueryPropertyChanged;
            groupQuery.Dispose();
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        groupQuery.GetEnumerator();

    void GroupQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void GroupQueryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void GroupQueryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    internal void InternalDispose()
    {
        ownerDisposing = true;
        Dispose();
    }

    protected override void OnInitialization()
    {
        groupQuery.CollectionChanged += GroupQueryCollectionChanged;
        groupQuery.PropertyChanging += GroupQueryPropertyChanging;
        groupQuery.PropertyChanged += GroupQueryPropertyChanged;
    }
}
