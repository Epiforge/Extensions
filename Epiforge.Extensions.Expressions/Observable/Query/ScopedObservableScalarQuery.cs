namespace Epiforge.Extensions.Expressions.Observable.Query;

// Queries are cached and reference counted, so what a caller receives has to be scoped to that
// caller rather than the shared query itself; otherwise one caller's Dispose releases another's
// claim. Same pattern as ScopedObservableExpression at the expression layer.
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

    public event EventHandler<DisposalNotificationEventArgs>? Disposed;

    public event EventHandler<DisposalNotificationEventArgs>? Disposing;

#pragma warning disable CS0067 // disposal here is never overridden: releasing this scope's single claim on the query always succeeds
    public event EventHandler<DisposalNotificationEventArgs>? DisposalOverridden;
#pragma warning restore CS0067

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = DisposalNotificationEventArgs.ByCallingDispose;
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
