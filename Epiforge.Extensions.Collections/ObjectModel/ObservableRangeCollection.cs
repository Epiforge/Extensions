namespace Epiforge.Extensions.Collections.ObjectModel;

/// <inheritdoc/>
public class ObservableRangeCollection<T> :
    ObservableCollection<T>,
    IObservableRangeCollection<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/>
    /// </summary>
    public ObservableRangeCollection() :
        base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    public ObservableRangeCollection(ILogger logger) :
        this() =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="raiseCollectionChangedEventsForIndividualElements">Whether to raise individual <see cref="INotifyCollectionChanged.CollectionChanged"/> events for each element operated upon by range methods</param>
    public ObservableRangeCollection(bool raiseCollectionChangedEventsForIndividualElements) :
        base() =>
        RaiseCollectionChangedEventsForIndividualElements = raiseCollectionChangedEventsForIndividualElements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="raiseCollectionChangedEventsForIndividualElements">Whether to raise individual <see cref="INotifyCollectionChanged.CollectionChanged"/> events for each element operated upon by range methods</param>
    public ObservableRangeCollection(ILogger logger, bool raiseCollectionChangedEventsForIndividualElements) :
        this(raiseCollectionChangedEventsForIndividualElements) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection from which the elements are copied</param>
    public ObservableRangeCollection(IEnumerable<T> collection) :
        base(collection)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="collection">The collection from which the elements are copied</param>
    public ObservableRangeCollection(ILogger logger, IEnumerable<T> collection) :
        this(collection) =>
        this.logger = logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection from which the elements are copied</param>
    /// <param name="raiseCollectionChangedEventsForIndividualElements">Whether to raise individual <see cref="INotifyCollectionChanged.CollectionChanged"/> events for each element operated upon by range methods</param>
    public ObservableRangeCollection(IEnumerable<T> collection, bool raiseCollectionChangedEventsForIndividualElements) :
        base(collection) =>
        RaiseCollectionChangedEventsForIndividualElements = raiseCollectionChangedEventsForIndividualElements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="logger">The logger with which to trace library logic</param>
    /// <param name="collection">The collection from which the elements are copied</param>
    /// <param name="raiseCollectionChangedEventsForIndividualElements">Whether to raise individual <see cref="INotifyCollectionChanged.CollectionChanged"/> events for each element operated upon by range methods</param>
    public ObservableRangeCollection(ILogger logger, IEnumerable<T> collection, bool raiseCollectionChangedEventsForIndividualElements) :
        this(collection, raiseCollectionChangedEventsForIndividualElements) =>
        this.logger = logger;

    readonly ILogger? logger;

    /// <summary>
    /// Gets whether this <see cref="ObservableRangeCollection{T}"/> will raise individual <see cref="INotifyCollectionChanged.CollectionChanged"/> events for each element operated upon by range methods
    /// </summary>
    public bool RaiseCollectionChangedEventsForIndividualElements { get; }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> items) =>
        InsertRange(Items.Count, items);

    /// <inheritdoc/>
    public void AddRange(IList<T> items) =>
        AddRange((IEnumerable<T>)items);

    /// <inheritdoc/>
    public IReadOnlyList<T> GetAndRemoveAll(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var removed = new List<T>();
        RemoveAllCore(predicate, removed);
        return [..removed];
    }

    /// <inheritdoc/>
    public virtual T GetAndRemoveAt(int index)
    {
        var item = Items[index];
        RemoveAt(index);
        return item;
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> GetRange(int index, int count)
    {
        var result = new List<T>();
        for (int i = index, ii = index + count; i < ii; ++i)
            result.Add(this[i]);
        return [..result];
    }

    /// <inheritdoc/>
    public void InsertRange(int index, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var originalIndex = index;
        --index;
        if (RaiseCollectionChangedEventsForIndividualElements)
            foreach (var item in items)
                InsertItem(++index, item);
        else
        {
            var list = new List<T>();
            foreach (var item in items)
            {
                Items.Insert(++index, item);
                list.Add(item);
            }
            if (list.Count > 0)
            {
                NotifyCountChanged();
                NotifyIndexerChanged();
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, originalIndex));
            }
        }
    }

    /// <inheritdoc/>
    public void InsertRange(int index, IList<T> items) =>
        InsertRange(index, (IEnumerable<T>)items);

    /// <inheritdoc/>
    public void MoveRange(int oldStartIndex, int newStartIndex, int count)
    {
        if (oldStartIndex != newStartIndex && count > 0)
        {
            if (RaiseCollectionChangedEventsForIndividualElements)
            {
                if (oldStartIndex < newStartIndex)
                {
                    var insertionIndex = newStartIndex + count - 1;
                    for (var i = 0; i < count; ++i)
                        Move(oldStartIndex, insertionIndex);
                }
                else
                {
                    for (var i = 0; i < count; ++i)
                        Move(oldStartIndex + i, newStartIndex + i);
                }
            }
            else
            {
                var insertionIndex = newStartIndex - 1;
                var movedItems = new List<T>();
                for (var i = 0; i < count; ++i)
                {
                    var item = Items[oldStartIndex];
                    Items.RemoveAt(oldStartIndex);
                    movedItems.Add(item);
                }
                foreach (var item in movedItems)
                    Items.Insert(++insertionIndex, item);
                NotifyIndexerChanged();
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, newStartIndex, oldStartIndex));
            }
        }
    }

    void NotifyCountChanged() =>
        OnPropertyChanged(CommonPropertyChangeNotificationEventArgs.CountChanged);

    void NotifyIndexerChanged() =>
        OnPropertyChanged(CommonPropertyChangeNotificationEventArgs.IndexerChanged);

    void NotifyReplacement(T[] oldItems, List<T> newItems, int index)
    {
        if (oldItems.Length is 0 && newItems.Count is 0)
            return;
        if (oldItems.Length != newItems.Count)
            NotifyCountChanged();
        NotifyIndexerChanged();
        if (oldItems.Length == newItems.Count)
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems, oldItems, index));
        else
        {
            if (oldItems.Length > 0)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems, index));
            if (newItems.Count > 0)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems, index));
        }
    }

    /// <inheritdoc/>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var eventArgs = logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        logger?.LogTrace(EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        base.OnCollectionChanged(e);
        logger?.LogTrace(EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    /// <inheritdoc/>
    public int RemoveAll(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return RemoveAllCore(predicate, null);
    }

    int RemoveAllCore(Func<T, bool> predicate, List<T>? removed)
    {
        var removedCount = 0;
        if (RaiseCollectionChangedEventsForIndividualElements)
        {
            for (var i = 0; i < Items.Count;)
                if (predicate(Items[i]))
                {
                    var item = GetAndRemoveAt(i);
                    removed?.Add(item);
                    ++removedCount;
                }
                else
                    ++i;
            return removedCount;
        }
        var count = Items.Count;
        var index = 0;
        while (index < count)
        {
            if (!predicate(Items[index]))
            {
                ++index;
                continue;
            }
            var runLength = 1;
            while (index + runLength < count && predicate(Items[index + runLength]))
                ++runLength;
            NotifyCollectionChangedEventArgs eventArgs;
            if (runLength == 1)
            {
                var item = Items[index];
                Items.RemoveAt(index);
                removed?.Add(item);
                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (object?)item, index);
            }
            else
            {
                var run = new T[runLength];
                for (var i = 0; i < runLength; ++i)
                {
                    run[i] = Items[index];
                    Items.RemoveAt(index);
                }
                removed?.AddRange(run);
                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, run, index);
            }
            count -= runLength;
            removedCount += runLength;
            NotifyCountChanged();
            NotifyIndexerChanged();
            OnCollectionChanged(eventArgs);
            ++index;
        }
        return removedCount;
    }

    /// <inheritdoc/>
    public void RemoveRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (RaiseCollectionChangedEventsForIndividualElements)
        {
            foreach (var item in items)
            {
                var index = Items.IndexOf(item);
                if (index >= 0)
                {
                    Items.RemoveAt(index);
                    NotifyCountChanged();
                    NotifyIndexerChanged();
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (object?)item, index));
                }
            }
            return;
        }
        var wanted = new NullableKeyDictionary<T, int>();
        var any = false;
        foreach (var item in items)
        {
            wanted[item] = wanted.TryGetValue(item, out var sought) ? sought + 1 : 1;
            any = true;
        }
        if (!any)
            return;
        RemoveAllCore(item =>
        {
            if (!wanted.TryGetValue(item, out var sought) || sought is 0)
                return false;
            wanted[item] = sought - 1;
            return true;
        }, null);
    }

    /// <inheritdoc/>
    public void RemoveRange(IList<T> items) =>
        RemoveRange((IEnumerable<T>)items);

    /// <inheritdoc/>
    public void RemoveRange(int index, int count)
    {
        if (count > 0)
            if (RaiseCollectionChangedEventsForIndividualElements)
                for (var i = 0; i < count; ++i)
                    RemoveAt(index);
            else
            {
                var removedItems = new T[count];
                for (var removalIndex = 0; removalIndex < count; ++removalIndex)
                {
                    removedItems[removalIndex] = Items[index];
                    Items.RemoveAt(index);
                }
                NotifyCountChanged();
                NotifyIndexerChanged();
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, index));
            }
    }

    /// <inheritdoc/>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (RaiseCollectionChangedEventsForIndividualElements)
        {
            Clear();
            AddRange(items);
        }
        else
        {
            var oldItems = new T[Items.Count];
            Items.CopyTo(oldItems, 0);
            Items.Clear();
            var list = new List<T>();
            foreach (var element in items)
            {
                Items.Add(element);
                list.Add(element);
            }
            NotifyReplacement(oldItems, list, 0);
        }
    }

    /// <inheritdoc/>
    public void ReplaceAll(IList<T> items) =>
        ReplaceAll((IEnumerable<T>)items);

    /// <inheritdoc/>
    public IReadOnlyList<T> ReplaceRange(int index, int count, IEnumerable<T>? collection = null)
    {
        if (RaiseCollectionChangedEventsForIndividualElements)
        {
            var oldItems = GetRange(index, count);
            RemoveRange(index, count);
            if (collection is not null)
                InsertRange(index, collection);
            return oldItems;
        }
        else
        {
            var originalIndex = index;
            var oldItems = new T[count];
            for (var i = 0; i < count; ++i)
            {
                oldItems[i] = Items[index];
                Items.RemoveAt(index);
            }
            var list = new List<T>();
            index -= 1;
            if (collection is not null)
                foreach (var element in collection)
                {
                    Items.Insert(++index, element);
                    list.Add(element);
                }
            NotifyReplacement(oldItems, list, originalIndex);
            return [..oldItems];
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> ReplaceRange(int index, int count, IList<T> list) =>
        ReplaceRange(index, count, (IEnumerable<T>)list);

    /// <inheritdoc/>
    public void Reset(IEnumerable<T> newCollection)
    {
        ArgumentNullException.ThrowIfNull(newCollection);
        var previousCount = Items.Count;
        Items.Clear();
        foreach (var element in newCollection)
            Items.Add(element);
        if (previousCount != Items.Count)
            NotifyCountChanged();
        NotifyIndexerChanged();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <inheritdoc/>
    public int ResetRemovingAll(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var count = Items.Count;
        var firstRemoved = -1;
        for (var i = 0; i < count; ++i)
            if (predicate(Items[i]))
            {
                firstRemoved = i;
                break;
            }
        if (firstRemoved < 0)
            return 0;
        var survivors = new List<T>(count - 1);
        for (var i = 0; i < firstRemoved; ++i)
            survivors.Add(Items[i]);
        for (var i = firstRemoved + 1; i < count; ++i)
        {
            var item = Items[i];
            if (!predicate(item))
                survivors.Add(item);
        }
        Items.Clear();
        for (var i = 0; i < survivors.Count; ++i)
            Items.Add(survivors[i]);
        NotifyCountChanged();
        NotifyIndexerChanged();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        return count - survivors.Count;
    }
}
