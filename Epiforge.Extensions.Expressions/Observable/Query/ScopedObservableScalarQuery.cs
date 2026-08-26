namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ScopedObservableScalarQuery<TResult> :
    IObservableScalarQuery<TResult>
{
    internal ScopedObservableScalarQuery(ObservableScalarQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        this.query = query;
        this.query.PropertyChanged += QueryPropertyChanged;
        this.query.PropertyChanging += QueryPropertyChanging;
    }

    readonly ObservableScalarQuery<TResult> query;
    int disposed;

    public int CachedObservableQueries =>
        query.CachedObservableQueries;

    public ICollectionObserver CollectionObserver =>
        query.CollectionObserver;

    public (Exception? Fault, TResult Result) Evaluation =>
        query.Evaluation;

    public bool IsDisposed =>
        disposed != 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    public event EventHandler? Disposed;

    public event EventHandler? Disposing;

    event EventHandler? INotifyDisposalOverridden.DisposalOverridden
    {
        add { }
        remove { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = EventArgs.Empty;
        Disposing?.Invoke(this, e);
        query.PropertyChanged -= QueryPropertyChanged;
        query.PropertyChanging -= QueryPropertyChanging;
        query.Dispose();
        Disposed?.Invoke(this, e);
    }

    public IObservableScalarQuery<TTransform> ObserveTransform<TTransform>(Expression<Func<TResult, TTransform>> transform) =>
        query.ObserveTransform(transform);

    void QueryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    void QueryPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        PropertyChanging?.Invoke(this, e);

    public override string ToString() =>
        query.ToString() ?? string.Empty;
}
