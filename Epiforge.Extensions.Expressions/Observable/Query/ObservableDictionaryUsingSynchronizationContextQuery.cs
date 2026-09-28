namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableDictionaryUsingSynchronizationContextQuery<TKey, TValue>(CollectionObserver collectionObserver, ObservableDictionaryQuery<TKey, TValue> source, SynchronizationContext synchronizationContext) :
    ObservableDictionaryQuery<TKey, TValue>(collectionObserver)
    where TKey : notnull
{
    ObservableDictionary<TKey, TValue>? dictionary;
    readonly Queue<(NotifyDictionaryChangedEventArgs<TKey, TValue> change, Dictionary<TKey, TValue>? reset)> pending = new();
    internal readonly SynchronizationContext SynchronizationContext = synchronizationContext;

    public override TValue this[TKey key] =>
        ReadOnContext(SynchronizationContext, () => dictionary![key]);

    internal override IEqualityComparer<TKey> KeyComparer =>
        source.KeyComparer;

    public override int Count =>
        ReadOnContext(SynchronizationContext, () => dictionary!.Count);

    public override bool IsSynchronized =>
        true;

    public override IEnumerable<TKey> Keys =>
        ReadOnContext(SynchronizationContext, () => dictionary!.Keys.ToList().AsReadOnly());

    public override IEnumerable<TValue> Values =>
        ReadOnContext(SynchronizationContext, () => dictionary!.Values.ToList().AsReadOnly());

    public override bool Contains(KeyValuePair<TKey, TValue> item) =>
        ReadOnContext(SynchronizationContext, () => dictionary!.Contains(item));

    public override bool ContainsKey(TKey key) =>
        ReadOnContext(SynchronizationContext, () => dictionary!.ContainsKey(key));

    public override void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        ReadOnContext(SynchronizationContext, () => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary!).CopyTo(array, arrayIndex));

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
                source.PropertyChanged -= SourcePropertyChanged;
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
        ReadOnContext(SynchronizationContext, () => (IEnumerator<KeyValuePair<TKey, TValue>>)dictionary!.ToList().GetEnumerator());

    public override IReadOnlyList<KeyValuePair<TKey, TValue>> GetRange(IEnumerable<TKey> keys) =>
        ReadOnContext(SynchronizationContext, () => dictionary!.GetRange(keys));

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
#pragma warning disable IDE0028 // Simplify collection initialization
        dictionary = new();
#pragma warning restore IDE0028 // Simplify collection initialization
        dictionary.AddRange(source);
        source.DictionaryChanged += SourceDictionaryChanged;
        source.PropertyChanged += SourcePropertyChanged;
        OperationFault = source.OperationFault;
        dictionary.CollectionChanged += DictionaryCollectionChanged;
        ((INotifyDictionaryChanged)dictionary).DictionaryChanged += DictionaryDictionaryChangedBoxed;
        dictionary.DictionaryChanged += DictionaryDictionaryChanged;
        dictionary.PropertyChanged += DictionaryPropertyChanged;
    }

    /// <summary>
    /// Applies, on the context's thread, every change of the source not yet applied, in the order the source announced them
    /// </summary>
    void ApplyPendingChanges()
    {
        using var changeHold = HoldOwnChanges();
        while (true)
        {
            NotifyDictionaryChangedEventArgs<TKey, TValue> e;
            Dictionary<TKey, TValue>? reset;
            lock (pending)
            {
                if (!pending.TryDequeue(out var next))
                    return;
                (e, reset) = next;
            }
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
                    dictionary!.Reset(reset!);
                    break;
            }
        }
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
    {
        lock (pending)
            pending.Enqueue((e, e.Action is NotifyDictionaryChangedAction.Reset ? source.ToDictionary(kv => kv.Key, kv => kv.Value) : null));
        if (SynchronizationContext == SynchronizationContext.Current)
            ApplyPendingChanges();
        else
            DeferUntilChangeLocksReleased(() => SynchronizationContext.Send(ApplyPendingChanges));
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OperationFault))
            return;
        void applyOperationFault()
        {
            using var changeHold = HoldOwnChanges();
            OperationFault = source.OperationFault;
        }
        if (SynchronizationContext == SynchronizationContext.Current)
            applyOperationFault();
        else
            DeferUntilChangeLocksReleased(() => SynchronizationContext.Send(applyOperationFault));
    }

    public override string ToString() =>
        $"synchronizing {source} using {SynchronizationContext}";

    public override bool TryGetValue(TKey key, out TValue value)
    {
        bool success;
        (success, value) = ReadOnContext(SynchronizationContext, () => (dictionary!.TryGetValue(key, out var value), value));
        return success;
    }
}
