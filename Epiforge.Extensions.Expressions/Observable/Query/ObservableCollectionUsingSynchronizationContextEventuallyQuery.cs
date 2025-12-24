namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionUsingSynchronizationContextEventuallyQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, SynchronizationContext synchronizationContext) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    static T MarshalForFunc<T>(SynchronizationContext synchronizationContext, Func<T> func)
    {
        if (synchronizationContext == SynchronizationContext.Current)
            return func();
        var tcs = new TaskCompletionSource<T>();
        synchronizationContext.Post(_ =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task.Result;
    }

    ObservableRangeCollection<TElement>? elements;
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

    public override TElement this[int index] =>
        MarshalForFunc(SynchronizationContext, () => elements![index]);

    public override int Count =>
        MarshalForFunc(SynchronizationContext, () => elements!.Count);

    public override bool IsSynchronized =>
        true;

    public override Exception? OperationFault
    {
        get => MarshalForFunc(SynchronizationContext, () => source.OperationFault);
        protected set => throw new NotImplementedException();
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.CollectionChanged -= SourceCollectionChanged;
                elements!.CollectionChanged += ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements!).PropertyChanged -= ElementsPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        MarshalForFunc(SynchronizationContext, () => elements!.ToList().AsReadOnly().GetEnumerator());

    protected override void OnInitialization()
    {
        elements = new(source);
        source.CollectionChanged += SourceCollectionChanged;
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SynchronizationContext.Post(_ =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    elements!.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<TElement>());
                    break;
                case NotifyCollectionChangedAction.Move:
                    elements!.MoveRange(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    elements!.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    elements!.ReplaceRange(e.OldStartingIndex, e.OldItems!.Count, e.NewItems!.Cast<TElement>());
                    break;
                case NotifyCollectionChangedAction.Reset:
                    elements!.Reset(source);
                    break;
            }
        }, null);

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext} eventually";
}
