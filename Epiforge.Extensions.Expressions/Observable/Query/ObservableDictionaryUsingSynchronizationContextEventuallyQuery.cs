namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryUsingSynchronizationContextEventuallyQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, SynchronizationContext synchronizationContext) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    ObservableDictionary<TKey, TValue>? dictionary;
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

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

    public override bool Contains(KeyValuePair<TKey, TValue> item) =>
        SynchronizationContext.Send(() => dictionary!.Contains(item));

    public override bool ContainsKey(TKey key) =>
        SynchronizationContext.Send(() => dictionary!.ContainsKey(key));

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        SynchronizationContext.Send(() => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary!).CopyTo(array, arrayIndex));

    void DictionaryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void DictionaryDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    void DictionaryDictionaryChangedBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChangedBoxed(e);

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
                dictionary!.CollectionChanged -= DictionaryCollectionChanged;
                ((INotifyDictionaryChanged)dictionary).DictionaryChanged -= DictionaryDictionaryChangedBoxed;
                dictionary.DictionaryChanged -= DictionaryDictionaryChanged;
                dictionary.PropertyChanged -= DictionaryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        SynchronizationContext.Send(() => dictionary!.ToList().AsReadOnly().GetEnumerator());

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys) =>
        SynchronizationContext.Send(() => dictionary!.GetRange(keys));

    protected override void OnInitialization()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
        dictionary = new();
#pragma warning restore IDE0028 // Simplify collection initialization
        dictionary.AddRange(source);
        source.DictionaryChanged += SourceDictionaryChanged;
        dictionary.CollectionChanged += DictionaryCollectionChanged;
        ((INotifyDictionaryChanged)dictionary).DictionaryChanged += DictionaryDictionaryChangedBoxed;
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
