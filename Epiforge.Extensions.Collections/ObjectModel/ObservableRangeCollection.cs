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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(predicate);
#else
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
#endif
        var removed = new List<T>();
        for (var i = 0; i < Items.Count;)
            if (predicate(Items[i]))
                removed.Add(GetAndRemoveAt(i));
            else
                ++i;
        return removed.ToImmutableArray();
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
        return result.ToImmutableArray();
    }

    /// <inheritdoc/>
    public void InsertRange(int index, IEnumerable<T> items)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(items);
#else
        if (items is null)
            throw new ArgumentNullException(nameof(items));
#endif
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
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, originalIndex));
                NotifyCountChanged();
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
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, newStartIndex, oldStartIndex));
                NotifyCountChanged();
            }
        }
    }

    void NotifyCountChanged() =>
        OnPropertyChanged(CommonPropertyChangeNotificationEventArgs.CountChanged);

    /// <inheritdoc/>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(e);
#else
        if (e is null)
            throw new ArgumentNullException(nameof(e));
#endif
        var eventArgs = logger?.IsEnabled(LogLevel.Trace) ?? false ? e.ToStringForLogging() : null;
        logger?.LogTrace(EventIds.Epiforge_Extensions_Collections_RaisingCollectionChanged, "Raising CollectionChanged: {EventArgs}", eventArgs);
        base.OnCollectionChanged(e);
        logger?.LogTrace(EventIds.Epiforge_Extensions_Collections_RaisedCollectionChanged, "Raised CollectionChanged: {EventArgs}", eventArgs);
    }

    /// <inheritdoc/>
    public int RemoveAll(Func<T, bool> predicate) =>
        GetAndRemoveAll(predicate).Count;

    /// <inheritdoc/>
    public void RemoveRange(IEnumerable<T> items)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(items);
#else
        if (items is null)
            throw new ArgumentNullException(nameof(items));
#endif
        foreach (var item in items)
        {
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                Items.RemoveAt(index);
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
                NotifyCountChanged();
            }
        }
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
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, index));
                NotifyCountChanged();
            }
    }

    /// <inheritdoc/>
    public void ReplaceAll(IEnumerable<T> items)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(items);
#else
        if (items is null)
            throw new ArgumentNullException(nameof(items));
#endif
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
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, list, oldItems, 0));
            if (oldItems.Length != list.Count)
                NotifyCountChanged();
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
            if (list.Count > 0)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, list, oldItems, originalIndex));
            else
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems, originalIndex));
            if (oldItems.Length != list.Count)
                NotifyCountChanged();
            return oldItems.ToImmutableArray();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> ReplaceRange(int index, int count, IList<T> list) =>
        ReplaceRange(index, count, (IEnumerable<T>)list);

    /// <inheritdoc/>
    public void Reset(IEnumerable<T> newCollection)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(newCollection);
#else
        if (newCollection is null)
            throw new ArgumentNullException(nameof(newCollection));
#endif
        var previousCount = Items.Count;
        Items.Clear();
        foreach (var element in newCollection)
            Items.Add(element);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        if (previousCount != Items.Count)
            NotifyCountChanged();
    }
}
