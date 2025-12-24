namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableScalarTransformQuery<TResult, TTransform>(CollectionObserver collectionObserver, ObservableScalarQuery<TResult> sourceQuery, Expression<Func<TResult, TTransform>> transform) :
    ObservableScalarQuery<TTransform>(collectionObserver)
{
    static readonly ConcurrentDictionary<Expression<Func<TResult, TTransform>>, Func<TResult, TTransform>> compiledTransformCache = new(ExpressionEqualityComparer.Default);

    static Func<TResult, TTransform> CompiledTransformCacheValueFactory(Expression<Func<TResult, TTransform>> transform) =>
        transform.Compile();

    Func<TResult, TTransform>? transformDelegate;

    internal readonly Expression<Func<TResult, TTransform>> Transform = transform;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = sourceQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                sourceQuery.PropertyChanged -= SourceQueryPropertyChanged;
                sourceQuery.PropertyChanging -= SourceQueryPropertyChanging;
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        var (sourceFault, sourceResult) = sourceQuery.Evaluation;
        if (sourceFault is not null)
            Evaluation = (sourceFault, default!);
        else
        {
            try
            {
                Evaluation = (null, transformDelegate!(sourceResult));
            }
            catch (Exception ex)
            {
                Evaluation = (ex, default!);
            }
        }
    }

    protected override void OnInitialization()
    {
        transformDelegate = compiledTransformCache.GetOrAdd(Transform, CompiledTransformCacheValueFactory);
        sourceQuery.PropertyChanged += SourceQueryPropertyChanged;
        sourceQuery.PropertyChanging += SourceQueryPropertyChanging;
        Evaluate();
    }

    void SourceQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Evaluation))
            Evaluate();
    }

    void SourceQueryPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(Evaluation))
            OnPropertyChanging(e);
    }
}
