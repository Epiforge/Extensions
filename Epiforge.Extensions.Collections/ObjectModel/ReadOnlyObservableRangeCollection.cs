namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Read-only wrapper around an <see cref="IReadOnlyObservableRangeCollection{T}"/>
/// </summary>
/// <typeparam name="T">The type of elements in the read-only collection</typeparam>
public sealed class ReadOnlyObservableRangeCollection<T> :
    SyncDisposable,
    IReadOnlyObservableRangeCollection<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ReadOnlyObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="rangeObservableCollection">The <see cref="IReadOnlyObservableRangeCollection{T}"/> around which to wrap</param>
    public ReadOnlyObservableRangeCollection(IReadOnlyObservableRangeCollection<T> rangeObservableCollection)
    {
        ArgumentNullException.ThrowIfNull(rangeObservableCollection);
        this.rangeObservableCollection = rangeObservableCollection;
        this.rangeObservableCollection.CollectionChanged += RangeObservableCollectionCollectionChanged;
        this.rangeObservableCollection.PropertyChanged += RangeObservableCollectionPropertyChanged;
    }

    readonly IReadOnlyObservableRangeCollection<T> rangeObservableCollection;

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

    /// <inheritdoc/>
    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            rangeObservableCollection.CollectionChanged -= RangeObservableCollectionCollectionChanged;
            rangeObservableCollection.PropertyChanged -= RangeObservableCollectionPropertyChanged;
        }
        return true;
    }

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

    void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    void RangeObservableCollectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void RangeObservableCollectionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void IList.Remove(object? value) =>
        throw new NotSupportedException();

    bool ICollection<T>.Remove(T item) =>
        throw new NotSupportedException();

    void IList.RemoveAt(int index) =>
        throw new NotSupportedException();

    void IList<T>.RemoveAt(int index) =>
        throw new NotSupportedException();
}
