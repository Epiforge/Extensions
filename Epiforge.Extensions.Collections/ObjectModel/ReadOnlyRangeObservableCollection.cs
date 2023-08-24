namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Read-only wrapper around an <see cref="IRangeObservableCollection{T}"/>
/// </summary>
/// <typeparam name="T">The type of elements in the read-only collection</typeparam>
public sealed class ReadOnlyRangeObservableCollection<T> :
    IRangeObservableCollection<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ReadOnlyRangeObservableCollection{T}"/>
    /// </summary>
    /// <param name="rangeObservableCollection">The <see cref="IRangeObservableCollection{T}"/> around which to wrap</param>
    public ReadOnlyRangeObservableCollection(IRangeObservableCollection<T> rangeObservableCollection)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(rangeObservableCollection);
#else
        if (rangeObservableCollection is null)
            throw new ArgumentNullException(nameof(rangeObservableCollection));
#endif
        this.rangeObservableCollection = rangeObservableCollection;
        this.rangeObservableCollection.CollectionChanged += RangeObservableCollectionCollectionChanged;
        this.rangeObservableCollection.PropertyChanged += RangeObservableCollectionPropertyChanged;
    }

    readonly IRangeObservableCollection<T> rangeObservableCollection;

    /// <inheritdoc/>
    public T this[int index] => ((IReadOnlyList<T>)rangeObservableCollection)[index];

    T IList<T>.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public int Count =>
        ((ICollection)rangeObservableCollection).Count;

    /// <inheritdoc/>
    public bool IsFixedSize =>
        rangeObservableCollection.IsFixedSize;

    /// <inheritdoc/>
    public bool IsReadOnly =>
        true;

    /// <inheritdoc/>
    public bool IsSynchronized =>
        rangeObservableCollection.IsSynchronized;

    /// <inheritdoc/>
    public object SyncRoot =>
        rangeObservableCollection.SyncRoot;

    int IList.Add(object? value) =>
        throw new NotSupportedException();

    void ICollection<T>.Add(T item) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.AddRange(IEnumerable<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.AddRange(IList<T> items) =>
        throw new NotSupportedException();

    void IList.Clear() =>
        throw new NotSupportedException();

    void ICollection<T>.Clear() =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Contains(T item) =>
        rangeObservableCollection.Contains(item);

    bool IList.Contains(object? value) =>
        rangeObservableCollection.Contains(value);

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) =>
        rangeObservableCollection.CopyTo(array, arrayIndex);

    void ICollection.CopyTo(Array array, int index) =>
        rangeObservableCollection.CopyTo(array, index);

    IReadOnlyList<T> IRangeObservableCollection<T>.GetAndRemoveAll(Func<T, bool> predicate) =>
        throw new NotSupportedException();

    T IRangeObservableCollection<T>.GetAndRemoveAt(int index) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() =>
        rangeObservableCollection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)rangeObservableCollection).GetEnumerator();

    /// <inheritdoc/>
    public IReadOnlyList<T> GetRange(int index, int count) =>
        rangeObservableCollection.GetRange(index, count);

    /// <inheritdoc/>
    public int IndexOf(T item) =>
        rangeObservableCollection.IndexOf(item);

    int IList.IndexOf(object? value) =>
        rangeObservableCollection.IndexOf(value);

    void IList.Insert(int index, object? value) =>
        throw new NotSupportedException();

    void IList<T>.Insert(int index, T item) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.InsertRange(int index, IEnumerable<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.InsertRange(int index, IList<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.MoveRange(int oldStartIndex, int newStartIndex, int count) =>
        throw new NotSupportedException();

    void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    void OnPropertyChanged(PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    void RangeObservableCollectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void RangeObservableCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void IList.Remove(object? value) =>
        throw new NotSupportedException();

    bool ICollection<T>.Remove(T item) =>
        throw new NotSupportedException();

    int IRangeObservableCollection<T>.RemoveAll(Func<T, bool> predicate) =>
        throw new NotSupportedException();

    void IList.RemoveAt(int index) =>
        throw new NotSupportedException();

    void IList<T>.RemoveAt(int index) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.RemoveRange(IEnumerable<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.RemoveRange(IList<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.RemoveRange(int index, int count) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.ReplaceAll(IEnumerable<T> items) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.ReplaceAll(IList<T> items) =>
        throw new NotSupportedException();

#pragma warning disable CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
    IReadOnlyList<T> IRangeObservableCollection<T>.ReplaceRange(int index, int count, IEnumerable<T>? collection = null) =>
        throw new NotSupportedException();
#pragma warning restore CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments

    IReadOnlyList<T> IRangeObservableCollection<T>.ReplaceRange(int index, int count, IList<T> list) =>
        throw new NotSupportedException();

    void IRangeObservableCollection<T>.Reset(IEnumerable<T> newCollection) =>
        throw new NotSupportedException();
}
