namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Read-only wrapper around an <see cref="IObservableRangeDictionary{TKey, TValue}"/>
/// </summary>
/// <typeparam name="TKey">The type of keys in the read-only dictionary</typeparam>
/// <typeparam name="TValue">The type of values in the read-only dictionary</typeparam>
public class ReadOnlyObservableRangeDictionary<TKey, TValue> :
    ReadOnlyRangeDictionary<TKey, TValue>,
    IDisposable,
    IObservableRangeDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyObservableRangeDictionary{TKey, TValue}"/> class
    /// </summary>
    /// <param name="observableRangeDictionary">The <see cref="IObservableRangeDictionary{TKey, TValue}"/> around which to wrap</param>
    [SuppressMessage("Design", "CA1062: Validate arguments of public methods", Justification = "The base class constructor will.")]
    public ReadOnlyObservableRangeDictionary(IObservableRangeDictionary<TKey, TValue> observableRangeDictionary) :
        base(observableRangeDictionary)
    {
        this.observableRangeDictionary = observableRangeDictionary;
        this.observableRangeDictionary!.CollectionChanged += HandleCollectionChanged;
        ((INotifyDictionaryChanged)this.observableRangeDictionary).DictionaryChanged += HandleDictionaryChanged;
        ((INotifyDictionaryChanged<TKey, TValue>)this.observableRangeDictionary).DictionaryChanged += HandleDictionaryChanged;
    }

    ~ReadOnlyObservableRangeDictionary() =>
        Dispose(false);

    bool isDisposed;
    readonly IObservableRangeDictionary<TKey, TValue> observableRangeDictionary;

    /// <summary>
    /// Occurs when the dictionary changes
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Occurs when the dictionary changes
    /// </summary>
    public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? DictionaryChanged;

    /// <summary>
    /// Occurs when the dictionary changes
    /// </summary>
    protected event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? NonGenericDictionaryChanged;

    event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? INotifyDictionaryChanged.DictionaryChanged
    {
        add => NonGenericDictionaryChanged += value;
        remove => NonGenericDictionaryChanged -= value;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
            return;
        if (disposing)
        {
            observableRangeDictionary!.CollectionChanged += HandleCollectionChanged;
            ((INotifyDictionaryChanged)observableRangeDictionary).DictionaryChanged -= HandleDictionaryChanged;
            ((INotifyDictionaryChanged<TKey, TValue>)observableRangeDictionary).DictionaryChanged -= HandleDictionaryChanged;
        }
        isDisposed = true;
    }

    void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void HandleDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        OnDictionaryChanged(e);

    void HandleDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        OnDictionaryChanged(e);

    /// <summary>
    /// Raises the <see cref="INotifyCollectionChanged.CollectionChanged"/> event
    /// </summary>
    /// <param name="e">The event arguments</param>
    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="INotifyDictionaryChanged.DictionaryChanged"/> event
    /// </summary>
    /// <param name="e">The event arguments</param>
    protected virtual void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        NonGenericDictionaryChanged?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="INotifyDictionaryChanged{TKey, TValue}.DictionaryChanged"/> event
    /// </summary>
    /// <param name="e">The event arguments</param>
    protected virtual void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        DictionaryChanged?.Invoke(this, e);
}
