namespace Epiforge.Extensions.Expressions.Observable;

abstract class ObservableExpression :
    SyncDisposable
{
    internal static readonly PropertyChangedEventArgs EvaluationPropertyChangedEventArgs = new(nameof(Evaluation));
    internal static readonly PropertyChangingEventArgs EvaluationPropertyChangingEventArgs = new(nameof(Evaluation));

    protected ObservableExpression(ExpressionObserver observer, Expression expression, bool deferEvaluation)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        this.observer = observer;
        Logger = observer.Logger;
        Expression = expression;
        var type = Expression.Type;
        defaultResult = type.FastDefault();
        resultEqualityComparer = FastEqualityComparer.Get(type);
        deferringEvaluation = deferEvaluation;
        evaluation = (null, defaultResult);
    }

    protected readonly object? defaultResult;
    bool deferringEvaluation;
#if IS_NET_9_0_OR_GREATER
    readonly Lock deferringEvaluationAccess = new();
#else
    readonly object deferringEvaluationAccess = new();
#endif
    (Exception? Fault, object? Result) evaluation;
    protected readonly ExpressionObserver observer;
    readonly FastEqualityComparer resultEqualityComparer;

    internal readonly Expression Expression;
#if IS_NET_9_0_OR_GREATER
    internal readonly Lock InitializationAccess = new();
#else
    internal readonly object InitializationAccess = new();
#endif
    internal Exception? InitializationException;
    internal bool IsInitialized;
    internal int Observations;

    public (Exception? Fault, object? Result) Evaluation
    {
        get
        {
            EvaluateIfDeferred();
            return evaluation;
        }
        protected set
        {
            if (!ReferenceEquals(evaluation.Fault, value.Fault) || !resultEqualityComparer.Equals(evaluation.Result, value.Result))
            {
                var previousValue = evaluation.Result;
                OnPropertyChanging(EvaluationPropertyChangingEventArgs);
                evaluation = value;
                OnPropertyChanged(EvaluationPropertyChangedEventArgs);
                DisposeIfNecessaryAndPossible(previousValue);
            }
        }
    }

    protected bool IsDeferringEvaluation
    {
        get
        {
            lock (deferringEvaluationAccess)
                return deferringEvaluation;
        }
    }

    void DisposeIfNecessaryAndPossible(object? value)
    {
        if (GetShouldValueBeDisposed())
        {
            if (!observer.PreferAsyncDisposal && value is IDisposable preferredDisposable)
                preferredDisposable.Dispose();
            else if (value is IAsyncDisposable asyncDisposable)
            {
                if (observer.BlockOnAsyncDisposal)
                    asyncDisposable.DisposeAsync().AsTask().Wait();
                else
                    Task.Run(async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false));
            }
            else if (value is IDisposable disposable)
                disposable.Dispose();
        }
    }

    protected void DisposeValueIfNecessaryAndPossible() =>
        DisposeIfNecessaryAndPossible(evaluation.Result);

    protected virtual void Evaluate()
    {
    }

    internal void EvaluateIfDeferred()
    {
        var shouldEvaluate = false;
        lock (deferringEvaluationAccess)
        {
            if (deferringEvaluation)
            {
                deferringEvaluation = false;
                shouldEvaluate = true;
            }
        }
        if (shouldEvaluate)
            Evaluate();
    }

    protected void EvaluateIfNotDeferred()
    {
        bool shouldEvaluate;
        lock (deferringEvaluationAccess)
            shouldEvaluate = !deferringEvaluation;
        if (shouldEvaluate)
            Evaluate();
    }

    protected virtual bool GetShouldValueBeDisposed() =>
        false;

    internal void Initialize()
    {
        OnInitialization();
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionInitialized, "Initialized observation of {Expression}", Expression);
    }

    protected abstract void OnInitialization();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionDisposed, "Disposed observation of {Expression}", Expression);

    public override string ToString() =>
        Expression.ToString();

    protected bool TryGetUndeferredResult(out object? result)
    {
        lock (deferringEvaluationAccess)
        {
            if (deferringEvaluation)
            {
                result = null;
                return false;
            }
        }
        result = evaluation.Result;
        return true;
    }
}

// What callers of IExpressionObserver.Observe receive. Unlike the ObservableExpression nodes
// beneath them, these are NOT cached: every call to Observe produces a new one holding exactly
// one reference to the shared node. Identity is therefore per-caller, so disposing one cannot
// release another caller's claim on the same node, and Dispose can be idempotent
// without becoming a no-op for a second legitimate owner.
abstract class ScopedObservableExpression
{
    protected ScopedObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(observableExpression);
        ArgumentNullException.ThrowIfNull(arguments);
        this.observer = observer;
        Expression = expression;
        this.observableExpression = observableExpression;
        this.observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        this.observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
        Arguments = arguments;
    }

    private protected readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;
    int disposed;

    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public bool IsDisposed =>
        disposed != 0;

    public IExpressionObserver Observer =>
        observer;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    public event EventHandler<DisposalNotificationEventArgs>? Disposed;

    public event EventHandler<DisposalNotificationEventArgs>? Disposing;

#pragma warning disable CS0067 // disposal here is never overridden: releasing this scope's single claim on the node always succeeds
    public event EventHandler<DisposalNotificationEventArgs>? DisposalOverridden;
#pragma warning restore CS0067

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = DisposalNotificationEventArgs.ByCallingDispose;
        Disposing?.Invoke(this, e);
        observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
        observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
        observableExpression.Dispose();
        Disposed?.Invoke(this, e);
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        PropertyChanging?.Invoke(this, e);

    public override string ToString() =>
        Expression.ToString();
}

class ScopedObservableExpression<TResult> :
    ScopedObservableExpression,
    IObservableExpression<TResult>
{
    public ScopedObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, IReadOnlyList<object?> arguments) :
        base(observer, expression, observableExpression, arguments)
    {
    }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = observableExpression.Evaluation;
            return (fault, (TResult)result!);
        }
    }
}

class ScopedObservableExpression<TArgument, TResult> :
    ScopedObservableExpression<TResult>,
    IObservableExpression<TArgument, TResult>
{
    public ScopedObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument argument) :
        base(observer, expression, observableExpression, [argument]) =>
        Argument = argument;

    public TArgument Argument { get; }
}

class ScopedObservableExpression<TArgument1, TArgument2, TResult> :
    ScopedObservableExpression<TResult>,
    IObservableExpression<TArgument1, TArgument2, TResult>
{
    public ScopedObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2) :
        base(observer, expression, observableExpression, [argument1, argument2])
    {
        Argument1 = argument1;
        Argument2 = argument2;
    }

    public TArgument1 Argument1 { get; }

    public TArgument2 Argument2 { get; }
}

class ScopedObservableExpression<TArgument1, TArgument2, TArgument3, TResult> :
    ScopedObservableExpression<TResult>,
    IObservableExpression<TArgument1, TArgument2, TArgument3, TResult>
{
    public ScopedObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) :
        base(observer, expression, observableExpression, [argument1, argument2, argument3])
    {
        Argument1 = argument1;
        Argument2 = argument2;
        Argument3 = argument3;
    }

    public TArgument1 Argument1 { get; }

    public TArgument2 Argument2 { get; }

    public TArgument3 Argument3 { get; }
}
