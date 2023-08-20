namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue> :
    ObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    public ObservableDictionaryUsingSynchronizationContextEventuallyQuery(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, SynchronizationContext synchronizationContext) :
        base(collectionObserver)
    {
        this.source = source;
        SynchronizationContext = synchronizationContext;
    }

    ObservableDictionary<TKey, TValue>? dictionary;
    readonly ObservableDictionaryQuery<TKey, TValue> source;

    internal readonly SynchronizationContext SynchronizationContext;

    public override TValue this[TKey key] =>
        SynchronizationContext.Send(() => dictionary![key]);

    public override int Count =>
        SynchronizationContext.Send(() => dictionary!.Count);

    public override bool IsSynchronized =>
        true;

    public override IEnumerable<TKey> Keys =>
        SynchronizationContext.Send(() => dictionary!.Keys.ToList().AsReadOnly());

    public override Exception? OperationFault
    {
        get => SynchronizationContext.Send(() => source.OperationFault);
        protected set => throw new NotImplementedException();
    }

    public override IEnumerable<TValue> Values =>
        SynchronizationContext.Send(() => dictionary!.Values.ToList().AsReadOnly());

    public override bool ContainsKey(TKey key) =>
        SynchronizationContext.Send(() => dictionary!.ContainsKey(key));

    void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void DictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.DictionaryChanged -= SourceDictionaryChanged;
                dictionary!.DictionaryChanged -= DictionaryDictionaryChanged;
                dictionary.PropertyChanged -= DictionaryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        SynchronizationContext.Send(() => dictionary!.ToList().AsReadOnly().GetEnumerator());

    protected override void OnInitialization()
    {
        dictionary = new();
        dictionary.AddRange(source);
        source.DictionaryChanged += SourceDictionaryChanged;
        dictionary.DictionaryChanged += DictionaryDictionaryChanged;
        dictionary.PropertyChanged += DictionaryPropertyChanged;
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        SynchronizationContext.Post(_ =>
        {
            switch (e.Action)
            {
                case NotifyDictionaryChangedAction.Add:
                    dictionary!.AddRange(e.NewItems);
                    break;
                case NotifyDictionaryChangedAction.Remove:
                    dictionary!.RemoveRange(e.OldItems.Select(oldKeyValuePair => oldKeyValuePair.Key));
                    break;
                case NotifyDictionaryChangedAction.Replace:
                    dictionary!.ReplaceRange(e.OldItems.Select(oldKeyValuePair => oldKeyValuePair.Key), e.NewItems);
                    break;
                case NotifyDictionaryChangedAction.Reset:
                    dictionary!.Reset(source.ToDictionary(kv => kv.Key, kv => kv.Value));
                    break;
            }
        }, null);

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext} eventually";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        bool success;
        (success, value) = SynchronizationContext.Send(() => (dictionary!.TryGetValue(key, out var value), value));
        return success;
    }
}
