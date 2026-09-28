namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableGrouping<TKey, TElement>(CollectionObserver collectionObserver, TKey key, ObservableCollectionQuery<TElement> groupQuery, ObservableQuery owner) :
    ObservableCollectionQuery<TElement>(collectionObserver),
    IObservableGrouping<TKey, TElement>,
    IObservableQueryDependent
{
    ObservableQuerySubscription? groupQuerySubscription;
    bool ownerDisposing;

    public override TElement this[int index] =>
        groupQuery[index];

    public override int Count =>
        groupQuery.Count;

    internal override bool ChangesThroughOthers =>
        true;

    public TKey Key { get; } = key;

    private protected override ObservableQuery? ChangeLockHolder =>
        owner;

    internal override void CollectChangeLocks(List<ObservableQuery> queries) =>
        owner.CollectChangeLocks(queries);

    protected override bool Dispose(bool disposing)
    {
        if (!ownerDisposing)
            return false;
        if (disposing)
        {
            if (groupQuerySubscription is { } subscription)
                groupQuery.UnsubscribeDependent(subscription);
            groupQuery.Dispose();
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        groupQuery.GetEnumerator();

    void IObservableQueryDependent.OnDependencyCollectionChanged(ObservableQuerySubscription subscription, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void IObservableQueryDependent.OnDependencyPropertyChanged(ObservableQuerySubscription subscription, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void IObservableQueryDependent.OnDependencyPropertyChanging(ObservableQuerySubscription subscription, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    internal void InternalDispose()
    {
        ownerDisposing = true;
        Dispose();
    }

    protected override void OnInitialization()
    {
        groupQuerySubscription = groupQuery.SubscribeDependent(this);
    }
}
