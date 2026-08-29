namespace Epiforge.Extensions.Expressions.Observable;

abstract class ObservableExpression :
    PlainSyncDisposable
{
    internal static readonly PropertyChangedEventArgs EvaluationPropertyChangedEventArgs = new(nameof(Evaluation));
    internal static readonly PropertyChangingEventArgs EvaluationPropertyChangingEventArgs = new(nameof(Evaluation));

    protected ObservableExpression(ExpressionObserver observer, Expression expression, bool deferEvaluation)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        this.observer = observer;
        Expression = expression;
        var type = Expression.Type;
        defaultResult = type.FastDefault();
        resultEqualityComparer = FastEqualityComparer.Get(type);
        deferringEvaluation = deferEvaluation ? 1 : 0;
        evaluation = (null, defaultResult);
    }

    protected readonly object? defaultResult;
    int deferringEvaluation;
#if IS_NET_9_0_OR_GREATER
    readonly Lock dependentsAccess = new();
#else
    readonly object dependentsAccess = new();
#endif
    ObservableExpressionSubscription? firstDependent;
    ObservableExpressionSubscription? lastDependent;
    (Exception? Fault, object? Result) evaluation;
    protected readonly ExpressionObserver observer;
    readonly FastEqualityComparer resultEqualityComparer;

    internal readonly Expression Expression;
#if IS_NET_9_0_OR_GREATER
    internal Lock? InitializationAccess = new();
#else
    internal object? InitializationAccess = new();
#endif
    internal Exception? InitializationException;
    internal bool IsInitialized;
    internal int Observations;

    internal virtual bool CanChange =>
        true;

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
                evaluation = value;
                NotifyDependentsChanged();
                DisposeIfNecessaryAndPossible(previousValue);
            }
        }
    }

    protected bool IsDeferringEvaluation =>
        Volatile.Read(ref deferringEvaluation) != 0;

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
        if (Volatile.Read(ref deferringEvaluation) != 0 && Interlocked.Exchange(ref deferringEvaluation, 0) != 0)
            Evaluate();
    }

    protected void EvaluateIfNotDeferred()
    {
        if (Volatile.Read(ref deferringEvaluation) == 0)
            Evaluate();
    }

    protected virtual bool GetShouldValueBeDisposed() =>
        false;

    internal void Initialize()
    {
        OnInitialization();
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionInitialized, "Initialized observation of {Expression}", Expression);
    }

    private protected void NotifyDependentsChanged()
    {
        var current = Volatile.Read(ref firstDependent);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Dependent.OnDependencyEvaluationChanged(this);
            current = following;
        }
    }

    private protected void NotifyDependentsOfValueContentsChanged()
    {
        var current = Volatile.Read(ref firstDependent);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Dependent.OnDependencyValueContentsChanged(this);
            current = following;
        }
    }

    internal (Exception? Fault, object? Result) CurrentEvaluation =>
        evaluation;

    internal bool CurrentEvaluationEquals(in (Exception? Fault, object? Result) other) =>
        ReferenceEquals(evaluation.Fault, other.Fault) && resultEqualityComparer.Equals(evaluation.Result, other.Result);

    protected abstract void OnInitialization();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionDisposed, "Disposed observation of {Expression}", Expression);

    internal ObservableExpressionSubscription SubscribeDependent(IObservableExpressionDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        var subscription = new ObservableExpressionSubscription(dependent);
        lock (dependentsAccess)
        {
            subscription.Previous = lastDependent;
            if (lastDependent is null)
                Volatile.Write(ref firstDependent, subscription);
            else
                lastDependent.Next = subscription;
            lastDependent = subscription;
        }
        return subscription;
    }

    public override string ToString() =>
        Expression.ToString();

    protected bool TryGetUndeferredResult(out object? result)
    {
        if (Volatile.Read(ref deferringEvaluation) != 0)
        {
            result = null;
            return false;
        }
        result = evaluation.Result;
        return true;
    }

    internal void UnsubscribeDependent(ObservableExpressionSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (dependentsAccess)
        {
            if (subscription.IsRemoved)
                return;
            subscription.IsRemoved = true;
            if (subscription.Previous is null)
                Volatile.Write(ref firstDependent, subscription.Next);
            else
                subscription.Previous.Next = subscription.Next;
            if (subscription.Next is null)
                lastDependent = subscription.Previous;
            else
                subscription.Next.Previous = subscription.Previous;
            subscription.Previous = null;
        }
    }
}

abstract class ScopedObservableExpression :
    IObservableExpressionDependent
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
        evaluation = observableExpression.CurrentEvaluation;
        if (this.observableExpression.CanChange)
            subscription = this.observableExpression.SubscribeDependent(this);
        Arguments = arguments;
    }

    private protected readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;
    int disposed;
    private protected (Exception? Fault, object? Result) evaluation;
    bool notificationForced;
    bool notificationPending;
    readonly ObservableExpressionSubscription? subscription;

    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public bool IsDisposed =>
        disposed != 0;

    public IExpressionObserver Observer =>
        observer;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    public event EventHandler? Disposed;

    public event EventHandler? Disposing;

    internal void ClearPendingNotification() =>
        notificationPending = false;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = EventArgs.Empty;
        Disposing?.Invoke(this, e);
        if (subscription is { } dependency)
            observableExpression.UnsubscribeDependent(dependency);
        observableExpression.Dispose();
        Disposed?.Invoke(this, e);
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency)
    {
        if (Enlisted())
            return;
        RaiseIfEvaluationChanged();
    }

    void IObservableExpressionDependent.OnDependencyValueContentsChanged(ObservableExpression dependency)
    {
        notificationForced = true;
        if (Enlisted())
            return;
        RaiseIfEvaluationChanged();
    }

    bool Enlisted()
    {
        if (!PropagationScope.IsPropagating)
            return false;
        if (!notificationPending)
        {
            notificationPending = true;
            PropagationScope.Enlist(this);
        }
        return true;
    }

    void RaiseIfEvaluationChanged()
    {
        if (!notificationForced && observableExpression.CurrentEvaluationEquals(in evaluation))
            return;
        notificationForced = false;
        PropertyChanging?.Invoke(this, ObservableExpression.EvaluationPropertyChangingEventArgs);
        evaluation = observableExpression.CurrentEvaluation;
        PropertyChanged?.Invoke(this, ObservableExpression.EvaluationPropertyChangedEventArgs);
    }

    internal void RaisePendingNotification()
    {
        if (!IsDisposed)
            RaiseIfEvaluationChanged();
    }

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
            var (fault, result) = evaluation;
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
