namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents an observer of expressions
/// </summary>
public class ExpressionObserver :
    IExpressionObserver
{
    #region Cache Comparers

    class ConstantExpressionExpressionEqualityComparer :
        IEqualityComparer<ConstantExpression>
    {
        public static ConstantExpressionExpressionEqualityComparer Default { get; } = new();

        public bool Equals(ConstantExpression? x, ConstantExpression? y) =>
            ExpressionEqualityComparer.Default.Equals(x?.Value as Expression, y?.Value as Expression);

        public int GetHashCode(ConstantExpression obj) =>
            obj.Value is Expression expression ? ExpressionEqualityComparer.Default.GetHashCode(expression) : 0;
    }

    #endregion Cache Comparers

    static readonly ConcurrentDictionary<MethodInfo, PropertyInfo?> propertyGetMethodToProperty = new();

    static PropertyInfo? GetPropertyFromGetMethod(MethodInfo getMethod) =>
        getMethod.DeclaringType?.GetProperties().FirstOrDefault(property => property.GetMethod == getMethod);

    static Expression ReplaceParameters(Dictionary<ParameterExpression, ConstantExpression> parameterTranslation, Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression binaryExpression:
                return Expression.MakeBinary(binaryExpression.NodeType, ReplaceParameters(parameterTranslation, binaryExpression.Left), ReplaceParameters(parameterTranslation, binaryExpression.Right), binaryExpression.IsLiftedToNull, binaryExpression.Method, binaryExpression.Conversion);
            case ConditionalExpression conditionalExpression:
                return Expression.Condition(ReplaceParameters(parameterTranslation, conditionalExpression.Test), ReplaceParameters(parameterTranslation, conditionalExpression.IfTrue), ReplaceParameters(parameterTranslation, conditionalExpression.IfFalse), conditionalExpression.Type);
            case ConstantExpression constantExpression:
                return constantExpression;
            case InvocationExpression invocationExpression:
                return Expression.Invoke(ReplaceParameters(parameterTranslation, invocationExpression.Expression), [..invocationExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument))]);
            case IndexExpression indexExpression:
                return Expression.MakeIndex(ReplaceParameters(parameterTranslation, indexExpression.Object!), indexExpression.Indexer, indexExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument)));
            case LambdaExpression lambdaExpression:
                return lambdaExpression;
            case MemberExpression memberExpression:
                return Expression.MakeMemberAccess(memberExpression.Expression is { } instanceExpression ? ReplaceParameters(parameterTranslation, instanceExpression) : null, memberExpression.Member);
            case MemberInitExpression memberInitExpression:
                return Expression.MemberInit((NewExpression)ReplaceParameters(parameterTranslation, memberInitExpression.NewExpression)!, [..memberInitExpression.Bindings.Cast<MemberAssignment>().Select(memberAssignment => memberAssignment.Update(ReplaceParameters(parameterTranslation, memberAssignment.Expression)))]);
            case MethodCallExpression methodCallExpression:
                return methodCallExpression.Object is null ? Expression.Call(methodCallExpression.Method, methodCallExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument))) : Expression.Call(ReplaceParameters(parameterTranslation, methodCallExpression.Object), methodCallExpression.Method, methodCallExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument)));
            case NewArrayExpression newArrayInitExpression when newArrayInitExpression.NodeType == ExpressionType.NewArrayInit:
                return Expression.NewArrayInit(newArrayInitExpression.Type.GetElementType()!, newArrayInitExpression.Expressions.Select(expression => ReplaceParameters(parameterTranslation, expression)));
            case NewExpression newExpression:
                var newArguments = newExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument));
                return newExpression.Constructor is null ? newExpression : newExpression.Members is null ? Expression.New(newExpression.Constructor, newArguments) : Expression.New(newExpression.Constructor, newArguments, newExpression.Members);
            case ParameterExpression parameterExpression:
                return parameterTranslation[parameterExpression];
            case TypeBinaryExpression typeBinaryExpression:
                return Expression.TypeIs(ReplaceParameters(parameterTranslation, typeBinaryExpression.Expression), typeBinaryExpression.TypeOperand);
            case UnaryExpression unaryExpression:
                return Expression.MakeUnary(unaryExpression.NodeType, ReplaceParameters(parameterTranslation, unaryExpression.Operand), unaryExpression.Type, unaryExpression.Method);
            default:
                throw new NotSupportedException($"Cannot replace parameters in {expression?.GetType().Name ?? "null expression"}");
        }
    }

    internal static Expression? ReplaceParametersWithoutOptimization(LambdaExpression lambdaExpression, params object?[] arguments)
    {
        var parameterTranslation = new Dictionary<ParameterExpression, ConstantExpression>();
        for (var i = 0; i < lambdaExpression.Parameters.Count; ++i)
        {
            var parameter = lambdaExpression.Parameters[i];
            var constant = Expression.Constant(arguments[i], parameter.Type);
            parameterTranslation.Add(parameter, constant);
        }
        return ReplaceParameters(parameterTranslation, lambdaExpression.Body);
    }

    /// <summary>
    /// Instantiates an expression observer with the default options
    /// </summary>
    public ExpressionObserver() :
        this(new ExpressionObserverOptions())
    {
    }

    /// <summary>
    /// Instantiates an expression observer with the specified options
    /// </summary>
    /// <param name="options">The options</param>
    public ExpressionObserver(ExpressionObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        BlockOnAsyncDisposal = options.BlockOnAsyncDisposal;
        ConstantExpressionsListenForCollectionChanged = options.ConstantExpressionsListenForCollectionChanged;
        ConstantExpressionsListenForDictionaryChanged = options.ConstantExpressionsListenForDictionaryChanged;
        DisposeConstructedObjects = options.DisposeConstructedObjects;
        DisposeStaticMethodReturnValues = options.DisposeStaticMethodReturnValues;
        MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged = options.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged;
        MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged = options.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged;
        Optimizer = options.Optimizer;
        PreferAsyncDisposal = options.PreferAsyncDisposal;
        disposeConstructedTypes = [..options.DisposeConstructedTypes.Keys];
        disposeMethodReturnValues = [..options.DisposeMethodReturnValues.Keys];
        ignoredPropertyChangeNotifications = [..options.IgnoredPropertyChangeNotifications.Keys];
        Logger = options.Logger;
    }

    readonly Dictionary<Expression, IDisposable> cachedDoubleArgumentObservableExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<BinaryExpression, ObservableBinaryExpression> cachedObservableBinaryExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<ConditionalExpression, ObservableConditionalExpression> cachedObservableConditionalExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<ConstantExpression, ObservableConstantExpression> cachedObservableConstantExpressionExpressions = new(ConstantExpressionExpressionEqualityComparer.Default);
    readonly Dictionary<ConstantExpression, ObservableConstantExpression> cachedObservableConstantExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<Expression, IDisposable> cachedObservableExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<IndexExpression, ObservableIndexExpression> cachedObservableIndexExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<InvocationExpression, ObservableInvocationExpression> cachedObservableInvocationExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<MemberExpression, ObservableMemberExpression> cachedObservableMemberExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<MemberInitExpression, ObservableMemberInitExpression> cachedObservableMemberInitExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<MethodCallExpression, ObservableMethodCallExpression> cachedObservableMethodCallExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<NewArrayExpression, ObservableNewArrayInitExpression> cachedObservableNewArrayInitExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<NewExpression, ObservableNewExpression> cachedObservableNewExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<TypeBinaryExpression, ObservableTypeBinaryExpression> cachedObservableTypeBinaryExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<Expression, IDisposable> cachedSingleArgumentObservableExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<Expression, IDisposable> cachedTripleArgumentObservableExpressions = new(ExpressionEqualityComparer.Default);
    readonly Dictionary<UnaryExpression, ObservableUnaryExpression> cachedObservableUnaryExpressions = new(ExpressionEqualityComparer.Default);
#if IS_NET_9_0_OR_GREATER
    readonly Lock cachedDoubleArgumentObservableExpressionsAccess = new();
    readonly Lock cachedObservableBinaryExpressionsAccess = new();
    readonly Lock cachedObservableConditionalExpressionsAccess = new();
    readonly Lock cachedObservableConstantExpressionsAccess = new();
    readonly Lock cachedObservableExpressionsAccess = new();
    readonly Lock cachedObservableIndexExpressionsAccess = new();
    readonly Lock cachedObservableInvocationExpressionsAccess = new();
    readonly Lock cachedObservableMemberExpressionsAccess = new();
    readonly Lock cachedObservableMemberInitExpressionsAccess = new();
    readonly Lock cachedObservableMethodCallExpressionsAccess = new();
    readonly Lock cachedObservableNewArrayInitExpressionsAccess = new();
    readonly Lock cachedObservableNewExpressionsAccess = new();
    readonly Lock cachedObservableTypeBinaryExpressionsAccess = new();
    readonly Lock cachedSingleArgumentObservableExpressionsAccess = new();
    readonly Lock cachedTripleArgumentObservableExpressionsAccess = new();
    readonly Lock cachedObservableUnaryExpressionsAccess = new();
#else
    readonly object cachedDoubleArgumentObservableExpressionsAccess = new();
    readonly object cachedObservableBinaryExpressionsAccess = new();
    readonly object cachedObservableConditionalExpressionsAccess = new();
    readonly object cachedObservableConstantExpressionsAccess = new();
    readonly object cachedObservableExpressionsAccess = new();
    readonly object cachedObservableIndexExpressionsAccess = new();
    readonly object cachedObservableInvocationExpressionsAccess = new();
    readonly object cachedObservableMemberExpressionsAccess = new();
    readonly object cachedObservableMemberInitExpressionsAccess = new();
    readonly object cachedObservableMethodCallExpressionsAccess = new();
    readonly object cachedObservableNewArrayInitExpressionsAccess = new();
    readonly object cachedObservableNewExpressionsAccess = new();
    readonly object cachedObservableTypeBinaryExpressionsAccess = new();
    readonly object cachedSingleArgumentObservableExpressionsAccess = new();
    readonly object cachedTripleArgumentObservableExpressionsAccess = new();
    readonly object cachedObservableUnaryExpressionsAccess = new();
#endif
    readonly ImmutableHashSet<(Type type, EquatableList<Type> constuctorParameterTypes)> disposeConstructedTypes;
    readonly ImmutableHashSet<MethodInfo> disposeMethodReturnValues;
    readonly ImmutableHashSet<PropertyInfo> ignoredPropertyChangeNotifications;

    /// <inheritdoc/>
    public bool BlockOnAsyncDisposal { get; }

    /// <inheritdoc/>
    public int CachedObservableExpressions
    {
        get
        {
            var count = 0;
            lock (cachedDoubleArgumentObservableExpressionsAccess)
                count += cachedDoubleArgumentObservableExpressions.Count;
            lock (cachedObservableBinaryExpressionsAccess)
                count += cachedObservableBinaryExpressions.Count;
            lock (cachedObservableConditionalExpressionsAccess)
                count += cachedObservableConditionalExpressions.Count;
            lock (cachedObservableConstantExpressionsAccess)
            {
                count += cachedObservableConstantExpressionExpressions.Count;
                count += cachedObservableConstantExpressions.Count;
            }
            lock (cachedObservableExpressionsAccess)
                count += cachedObservableExpressions.Count;
            lock (cachedObservableIndexExpressionsAccess)
                count += cachedObservableIndexExpressions.Count;
            lock (cachedObservableInvocationExpressionsAccess)
                count += cachedObservableInvocationExpressions.Count;
            lock (cachedObservableMemberExpressionsAccess)
                count += cachedObservableMemberExpressions.Count;
            lock (cachedObservableMemberInitExpressionsAccess)
                count += cachedObservableMemberInitExpressions.Count;
            lock (cachedObservableMethodCallExpressionsAccess)
                count += cachedObservableMethodCallExpressions.Count;
            lock (cachedObservableNewArrayInitExpressionsAccess)
                count += cachedObservableNewArrayInitExpressions.Count;
            lock (cachedObservableNewExpressionsAccess)
                count += cachedObservableNewExpressions.Count;
            lock (cachedObservableTypeBinaryExpressionsAccess)
                count += cachedObservableTypeBinaryExpressions.Count;
            lock (cachedSingleArgumentObservableExpressionsAccess)
                count += cachedSingleArgumentObservableExpressions.Count;
            lock (cachedTripleArgumentObservableExpressionsAccess)
                count += cachedTripleArgumentObservableExpressions.Count;
            lock (cachedObservableUnaryExpressionsAccess)
                count += cachedObservableUnaryExpressions.Count;
            return count;
        }
    }

    /// <inheritdoc/>
    public bool ConstantExpressionsListenForCollectionChanged { get; }

    /// <inheritdoc/>
    public bool ConstantExpressionsListenForDictionaryChanged { get; }

    /// <inheritdoc/>
    public bool DisposeConstructedObjects { get; }

    /// <inheritdoc/>
    public bool DisposeStaticMethodReturnValues { get; }

    /// <inheritdoc/>
    public ILogger? Logger { get; }

    /// <inheritdoc/>
    public bool MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged { get; }

    /// <inheritdoc/>
    public bool MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged { get; }

    /// <inheritdoc/>
    public Func<Expression, Expression>? Optimizer { get; }

    /// <inheritdoc/>
    public bool PreferAsyncDisposal { get; }

    /// <inheritdoc/>
    public Task ConditionAsync(Expression<Func<bool>> condition) =>
        ConditionAsync(condition, CancellationToken.None);

    /// <inheritdoc/>
    public Task ConditionAsync(Expression<Func<bool>> condition, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<object?>();
        IObservableExpression<bool>? observableExpression = null;
        void cancellationTokenCancelled()
        {
            observableExpression.PropertyChanged -= propertyChangedHandler;
            observableExpression.Dispose();
            taskCompletionSource.SetCanceled(cancellationToken);
        }
        void propertyChangedHandler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IObservableExpression<>.Evaluation))
            {
                if (observableExpression!.Evaluation.Fault is { } fault)
                {
                    observableExpression.PropertyChanged -= propertyChangedHandler;
                    observableExpression.Dispose();
                    taskCompletionSource!.SetException(fault);
                }
                else if (observableExpression!.Evaluation.Result)
                {
                    observableExpression.PropertyChanged -= propertyChangedHandler;
                    observableExpression.Dispose();
                    taskCompletionSource!.SetResult(null);
                }
            }
        }
        if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);
        observableExpression = Observe(condition);
        var (observableExpressionFault, observableExpressionResult) = observableExpression.Evaluation;
        if (observableExpressionFault is not null)
        {
            observableExpression.Dispose();
            return Task.FromException(observableExpressionFault);
        }
        else if (observableExpressionResult)
        {
            observableExpression.Dispose();
            return Task.CompletedTask;
        }
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(cancellationTokenCancelled);
        observableExpression.PropertyChanged += propertyChangedHandler;
        return taskCompletionSource.Task;
    }

    internal bool ExpressionDisposed(ObservableBinaryExpression observableBinaryExpression)
    {
        lock (cachedObservableBinaryExpressionsAccess)
        {
            if (--observableBinaryExpression.Observations == 0)
            {
                cachedObservableBinaryExpressions.Remove(observableBinaryExpression.BinaryExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableConditionalExpression observableConditionalExpression)
    {
        lock (cachedObservableConditionalExpressionsAccess)
        {
            if (--observableConditionalExpression.Observations == 0)
            {
                cachedObservableConditionalExpressions.Remove(observableConditionalExpression.ConditionalExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableConstantExpression observableConstantExpression)
    {
        var cachedExpressions = typeof(Expression).IsAssignableFrom(observableConstantExpression.ConstantExpression.Type) ? cachedObservableConstantExpressionExpressions : cachedObservableConstantExpressions;
        lock (cachedObservableConstantExpressionsAccess)
        {
            if (--observableConstantExpression.Observations == 0)
            {
                cachedExpressions.Remove(observableConstantExpression.ConstantExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed<TResult>(ObservableExpression<TResult> observableExpression)
    {
        lock (cachedObservableExpressionsAccess)
        {
            if (--observableExpression.Observations == 0)
            {
                cachedObservableExpressions.Remove(observableExpression.Expression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableIndexExpression observableIndexExpression)
    {
        lock (cachedObservableIndexExpressionsAccess)
        {
            if (--observableIndexExpression.Observations == 0)
            {
                cachedObservableIndexExpressions.Remove(observableIndexExpression.IndexExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableInvocationExpression observableInvocationExpression)
    {
        lock (cachedObservableInvocationExpressionsAccess)
        {
            if (--observableInvocationExpression.Observations == 0)
            {
                cachedObservableInvocationExpressions.Remove(observableInvocationExpression.InvocationExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableMemberExpression observableMemberExpression)
    {
        lock (cachedObservableMemberExpressionsAccess)
        {
            if (--observableMemberExpression.Observations == 0)
            {
                cachedObservableMemberExpressions.Remove(observableMemberExpression.MemberExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableMemberInitExpression observableMemberInitExpression)
    {
        lock (cachedObservableMemberInitExpressionsAccess)
        {
            if (--observableMemberInitExpression.Observations == 0)
            {
                cachedObservableMemberInitExpressions.Remove(observableMemberInitExpression.MemberInitExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableMethodCallExpression observableMethodCallExpression)
    {
        lock (cachedObservableMethodCallExpressionsAccess)
        {
            if (--observableMethodCallExpression.Observations == 0)
            {
                cachedObservableMethodCallExpressions.Remove(observableMethodCallExpression.MethodCallExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableNewArrayInitExpression observableNewArrayInitExpression)
    {
        lock (cachedObservableNewArrayInitExpressionsAccess)
        {
            if (--observableNewArrayInitExpression.Observations == 0)
            {
                cachedObservableNewArrayInitExpressions.Remove(observableNewArrayInitExpression.NewArrayExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableNewExpression observableNewExpression)
    {
        lock (cachedObservableNewExpressionsAccess)
        {
            if (--observableNewExpression.Observations == 0)
            {
                cachedObservableNewExpressions.Remove(observableNewExpression.NewExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableTypeBinaryExpression observableTypeBinaryExpression)
    {
        lock (cachedObservableTypeBinaryExpressionsAccess)
        {
            if (--observableTypeBinaryExpression.Observations == 0)
            {
                cachedObservableTypeBinaryExpressions.Remove(observableTypeBinaryExpression.TypeBinaryExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed(ObservableUnaryExpression observableUnaryExpression)
    {
        lock (cachedObservableUnaryExpressionsAccess)
        {
            if (--observableUnaryExpression.Observations == 0)
            {
                cachedObservableUnaryExpressions.Remove(observableUnaryExpression.UnaryExpression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed<TArgument, TResult>(ObservableExpression<TArgument, TResult> observableExpression)
    {
        lock (cachedSingleArgumentObservableExpressionsAccess)
        {
            if (--observableExpression.Observations == 0)
            {
                cachedSingleArgumentObservableExpressions.Remove(observableExpression.Expression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed<TArgument1, TArgument2, TResult>(ObservableExpression<TArgument1, TArgument2, TResult> observableExpression)
    {
        lock (cachedDoubleArgumentObservableExpressionsAccess)
        {
            if (--observableExpression.Observations == 0)
            {
                cachedDoubleArgumentObservableExpressions.Remove(observableExpression.Expression);
                return true;
            }
        }
        return false;
    }

    internal bool ExpressionDisposed<TArgument1, TArgument2, TArgument3, TResult>(ObservableExpression<TArgument1, TArgument2, TArgument3, TResult> observableExpression)
    {
        lock (cachedTripleArgumentObservableExpressionsAccess)
        {
            if (--observableExpression.Observations == 0)
            {
                cachedTripleArgumentObservableExpressions.Remove(observableExpression.Expression);
                return true;
            }
        }
        return false;
    }

    internal ObservableExpression GetObservableExpression(Expression expression, bool deferEvaluation)
    {
        var observableExpression = expression switch
        {
            BinaryExpression binaryExpression => GetObservableExpression(binaryExpression, deferEvaluation),
            ConditionalExpression conditionalExpression => GetObservableExpression(conditionalExpression, deferEvaluation),
            ConstantExpression constantExpression => GetObservableExpression(constantExpression, deferEvaluation),
            IndexExpression indexExpression => GetObservableExpression(indexExpression, deferEvaluation),
            InvocationExpression invocationExpression => GetObservableExpression(invocationExpression, deferEvaluation),
            MemberExpression memberExpression => GetObservableExpression(memberExpression, deferEvaluation),
            MemberInitExpression memberInitExpression => GetObservableExpression(memberInitExpression, deferEvaluation),
            MethodCallExpression methodCallExpressionForPropertyGet when propertyGetMethodToProperty.GetOrAdd(methodCallExpressionForPropertyGet.Method, GetPropertyFromGetMethod) is { } property => GetObservableExpression(methodCallExpressionForPropertyGet.Arguments.Count > 0 ? Expression.MakeIndex(methodCallExpressionForPropertyGet.Object!, property, methodCallExpressionForPropertyGet.Arguments) : Expression.MakeMemberAccess(methodCallExpressionForPropertyGet.Object, property), deferEvaluation),
            MethodCallExpression methodCallExpression => GetObservableExpression(methodCallExpression, deferEvaluation),
            NewArrayExpression newArrayExpression when newArrayExpression.NodeType is ExpressionType.NewArrayInit => GetObservableExpression(newArrayExpression, deferEvaluation),
            NewExpression newExpression => GetObservableExpression(newExpression, deferEvaluation),
            TypeBinaryExpression typeBinaryExpression when typeBinaryExpression.NodeType is not ExpressionType.TypeAs => GetObservableExpression(typeBinaryExpression, deferEvaluation),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => GetObservableExpression(Expression.Constant(unaryExpression.Operand), deferEvaluation),
            UnaryExpression unaryExpression => GetObservableExpression(unaryExpression, deferEvaluation),
            _ => throw new NotSupportedException()
        };
        lock (observableExpression.InitializationAccess)
        {
            if (observableExpression.InitializationException is { } initializationException)
                ExceptionDispatchInfo.Capture(initializationException).Throw();
            try
            {
                if (!observableExpression.IsInitialized)
                {
                    observableExpression.Initialize();
                    observableExpression.IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                observableExpression.InitializationException = ex;
                observableExpression.Dispose();
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        }
        if (!deferEvaluation)
            observableExpression.EvaluateIfDeferred();
        return observableExpression;
    }

    ObservableBinaryExpression GetObservableExpression(BinaryExpression binaryExpression, bool deferEvaluation)
    {
        lock (cachedObservableBinaryExpressionsAccess)
        {
            if (!cachedObservableBinaryExpressions.TryGetValue(binaryExpression, out var observableBinaryExpression))
            {
                observableBinaryExpression = binaryExpression.NodeType switch
                {
                    ExpressionType.AndAlso => new ObservableAndAlsoExpression(this, binaryExpression, deferEvaluation),
                    ExpressionType.Coalesce => new ObservableCoalesceExpression(this, binaryExpression, deferEvaluation),
                    ExpressionType.OrElse => new ObservableOrElseExpression(this, binaryExpression, deferEvaluation),
                    _ => new ObservableBinaryExpression(this, binaryExpression, deferEvaluation)
                };
                cachedObservableBinaryExpressions.Add(binaryExpression, observableBinaryExpression);
            }
            ++observableBinaryExpression.Observations;
            return observableBinaryExpression;
        }
    }

    ObservableConditionalExpression GetObservableExpression(ConditionalExpression conditionalExpression, bool deferEvaluation)
    {
        lock (cachedObservableConditionalExpressionsAccess)
        {
            if (!cachedObservableConditionalExpressions.TryGetValue(conditionalExpression, out var observableConditionalExpression))
            {
                observableConditionalExpression = new ObservableConditionalExpression(this, conditionalExpression, deferEvaluation);
                cachedObservableConditionalExpressions.Add(conditionalExpression, observableConditionalExpression);
            }
            ++observableConditionalExpression.Observations;
            return observableConditionalExpression;
        }
    }

    ObservableConstantExpression GetObservableExpression(ConstantExpression constantExpression, bool deferEvaluation)
    {
        lock (cachedObservableConstantExpressionsAccess)
        {
            var cachedExpressions = typeof(Expression).IsAssignableFrom(constantExpression.Type) ? cachedObservableConstantExpressionExpressions : cachedObservableConstantExpressions;
            if (!cachedExpressions.TryGetValue(constantExpression, out var observableConstantExpression))
            {
                observableConstantExpression = new ObservableConstantExpression(this, constantExpression, deferEvaluation);
                cachedExpressions.Add(constantExpression, observableConstantExpression);
            }
            ++observableConstantExpression.Observations;
            return observableConstantExpression;
        }
    }

    ObservableIndexExpression GetObservableExpression(IndexExpression indexExpression, bool deferEvaluation)
    {
        lock (cachedObservableIndexExpressionsAccess)
        {
            if (!cachedObservableIndexExpressions.TryGetValue(indexExpression, out var observableIndexExpression))
            {
                observableIndexExpression = new ObservableIndexExpression(this, indexExpression, deferEvaluation);
                cachedObservableIndexExpressions.Add(indexExpression, observableIndexExpression);
            }
            ++observableIndexExpression.Observations;
            return observableIndexExpression;
        }
    }

    ObservableInvocationExpression GetObservableExpression(InvocationExpression invocationExpression, bool deferEvaluation)
    {
        lock (cachedObservableInvocationExpressionsAccess)
        {
            if (!cachedObservableInvocationExpressions.TryGetValue(invocationExpression, out var observableInvocationExpression))
            {
                observableInvocationExpression = new ObservableInvocationExpression(this, invocationExpression, deferEvaluation);
                cachedObservableInvocationExpressions.Add(invocationExpression, observableInvocationExpression);
            }
            ++observableInvocationExpression.Observations;
            return observableInvocationExpression;
        }
    }

    ObservableMemberExpression GetObservableExpression(MemberExpression memberExpression, bool deferEvaluation)
    {
        lock (cachedObservableMemberExpressionsAccess)
        {
            if (!cachedObservableMemberExpressions.TryGetValue(memberExpression, out var observableInvocationExpression))
            {
                observableInvocationExpression = new ObservableMemberExpression(this, memberExpression, deferEvaluation);
                cachedObservableMemberExpressions.Add(memberExpression, observableInvocationExpression);
            }
            ++observableInvocationExpression.Observations;
            return observableInvocationExpression;
        }
    }

    ObservableMemberInitExpression GetObservableExpression(MemberInitExpression memberInitExpression, bool deferEvaluation)
    {
        lock (cachedObservableMemberInitExpressionsAccess)
        {
            if (!cachedObservableMemberInitExpressions.TryGetValue(memberInitExpression, out var observableInvocationExpression))
            {
                observableInvocationExpression = new ObservableMemberInitExpression(this, memberInitExpression, deferEvaluation);
                cachedObservableMemberInitExpressions.Add(memberInitExpression, observableInvocationExpression);
            }
            ++observableInvocationExpression.Observations;
            return observableInvocationExpression;
        }
    }

    ObservableMethodCallExpression GetObservableExpression(MethodCallExpression methodCallExpression, bool deferEvaluation)
    {
        lock (cachedObservableMethodCallExpressionsAccess)
        {
            if (!cachedObservableMethodCallExpressions.TryGetValue(methodCallExpression, out var observableMethodCallExpression))
            {
                observableMethodCallExpression = new ObservableMethodCallExpression(this, methodCallExpression, deferEvaluation);
                cachedObservableMethodCallExpressions.Add(methodCallExpression, observableMethodCallExpression);
            }
            ++observableMethodCallExpression.Observations;
            return observableMethodCallExpression;
        }
    }

    ObservableNewArrayInitExpression GetObservableExpression(NewArrayExpression newArrayInitExpression, bool deferEvaluation)
    {
        lock (cachedObservableNewArrayInitExpressionsAccess)
        {
            if (!cachedObservableNewArrayInitExpressions.TryGetValue(newArrayInitExpression, out var observableNewArrayInitExpression))
            {
                observableNewArrayInitExpression = new ObservableNewArrayInitExpression(this, newArrayInitExpression, deferEvaluation);
                cachedObservableNewArrayInitExpressions.Add(newArrayInitExpression, observableNewArrayInitExpression);
            }
            ++observableNewArrayInitExpression.Observations;
            return observableNewArrayInitExpression;
        }
    }

    ObservableNewExpression GetObservableExpression(NewExpression newExpression, bool deferEvaluation)
    {
        lock (cachedObservableNewExpressionsAccess)
        {
            if (!cachedObservableNewExpressions.TryGetValue(newExpression, out var observableNewExpression))
            {
                observableNewExpression = new ObservableNewExpression(this, newExpression, deferEvaluation);
                cachedObservableNewExpressions.Add(newExpression, observableNewExpression);
            }
            ++observableNewExpression.Observations;
            return observableNewExpression;
        }
    }

    ObservableTypeBinaryExpression GetObservableExpression(TypeBinaryExpression typeBinaryExpression, bool deferEvaluation)
    {
        lock (cachedObservableTypeBinaryExpressionsAccess)
        {
            if (!cachedObservableTypeBinaryExpressions.TryGetValue(typeBinaryExpression, out var observableTypeBinaryExpression))
            {
                observableTypeBinaryExpression = new ObservableTypeBinaryExpression(this, typeBinaryExpression, deferEvaluation);
                cachedObservableTypeBinaryExpressions.Add(typeBinaryExpression, observableTypeBinaryExpression);
            }
            ++observableTypeBinaryExpression.Observations;
            return observableTypeBinaryExpression;
        }
    }

    ObservableUnaryExpression GetObservableExpression(UnaryExpression unaryExpression, bool deferEvaluation)
    {
        lock (cachedObservableUnaryExpressionsAccess)
        {
            if (!cachedObservableUnaryExpressions.TryGetValue(unaryExpression, out var observableUnaryExpression))
            {
                observableUnaryExpression = new ObservableUnaryExpression(this, unaryExpression, deferEvaluation);
                cachedObservableUnaryExpressions.Add(unaryExpression, observableUnaryExpression);
            }
            ++observableUnaryExpression.Observations;
            return observableUnaryExpression;
        }
    }

    internal bool IsConstructedTypeDisposed(Type type, EquatableList<Type> constructorParameterTypes) =>
        DisposeConstructedObjects || disposeConstructedTypes.Contains((type, constructorParameterTypes));

    /// <inheritdoc/>
    public bool IsConstructedTypeDisposed(Type type, params Type[] constructorParameterTypes) =>
        IsConstructedTypeDisposed(type, new EquatableList<Type>(constructorParameterTypes));

    /// <inheritdoc/>
    public bool IsConstructedTypeDisposed(ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        if (constructor.DeclaringType is not { } declaringType)
            throw new ArgumentException("the constructor specified does not have a declaring type", nameof(constructor));
        return disposeConstructedTypes.Contains((declaringType, new EquatableList<Type>([..constructor.GetParameters().Select(parameterInfo => parameterInfo.ParameterType)])));
    }

    /// <inheritdoc/>
    public bool IsExpressionValueDisposed<T>(Expression<Func<T>> lambda)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        return lambda.Body switch
        {
            BinaryExpression binary when binary.Method is { } method => IsMethodReturnValueDisposed(method),
            IndexExpression index when index.Indexer is { } indexer => IsPropertyValueDisposed(indexer),
            NewExpression @new when @new.Constructor is { } constructor => IsConstructedTypeDisposed(constructor),
            MemberExpression member when member.Member is PropertyInfo property => IsPropertyValueDisposed(property),
            MethodCallExpression methodCallExpressionForPropertyGet when methodCallExpressionForPropertyGet.Method is { } method && ExpressionObserverOptions.PropertyGetMethodToProperty.GetOrAdd(method, ExpressionObserverOptions.GetPropertyFromGetMethod) is { } property => IsPropertyValueDisposed(property),
            MethodCallExpression methodCall when methodCall.Method is { } method => IsMethodReturnValueDisposed(method),
            UnaryExpression unary when unary.Method is { } method => IsMethodReturnValueDisposed(method),
            _ => throw new NotSupportedException(),
        };
    }

    /// <inheritdoc/>
    public bool IsIgnoredPropertyChangeNotification(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return ignoredPropertyChangeNotifications.Contains(property);
    }

    /// <inheritdoc/>
    public bool IsMethodReturnValueDisposed(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method.IsStatic && DisposeStaticMethodReturnValues || disposeMethodReturnValues.Contains(method) || method.IsGenericMethod && disposeMethodReturnValues.Contains(ExpressionObserverOptions.GenericMethodToGenericMethodDefinition.GetOrAdd(method, ExpressionObserverOptions.GetGenericMethodDefinitionFromGenericMethod)) || method.ReturnParameter.GetCustomAttributes(true).OfType<DisposeWhenDiscardedAttribute>().Any();
    }

    /// <inheritdoc/>
    public bool IsPropertyValueDisposed(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property.GetMethod is not { } getMethod)
            throw new ArgumentException("the property specified does not have a getter", nameof(property));
        return IsMethodReturnValueDisposed(getMethod);
    }

    ObservableExpression<TResult> Observe<TResult>(object?[] arguments, Expression? parameterReplacedExpression)
    {
        lock (cachedObservableExpressionsAccess)
        {
            ObservableExpression<TResult> typedInstance;
            if (cachedObservableExpressions.TryGetValue(parameterReplacedExpression!, out var instance))
                typedInstance = (ObservableExpression<TResult>)instance;
            else
            {
                typedInstance = new ObservableExpression<TResult>(this, parameterReplacedExpression!, GetObservableExpression(parameterReplacedExpression!, false), arguments);
                cachedObservableExpressions.Add(parameterReplacedExpression!, typedInstance);
            }
            ++typedInstance.Observations;
            return typedInstance;
        }
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TResult> Observe<TResult>(LambdaExpression lambdaExpression, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(lambdaExpression);
        ArgumentNullException.ThrowIfNull(arguments);
        var parameterReplacedExpression = ReplaceParameters(lambdaExpression, arguments);
        return Observe<TResult>(arguments, parameterReplacedExpression);
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TResult> ObserveWithoutOptimization<TResult>(LambdaExpression lambdaExpression, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(lambdaExpression);
        ArgumentNullException.ThrowIfNull(arguments);
        var parameterReplacedExpression = ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, arguments);
        return Observe<TResult>(arguments, parameterReplacedExpression);
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TResult> Observe<TResult>(Expression<Func<TResult>> expression) =>
        Observe<TResult>((LambdaExpression)expression);

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TResult> ObserveWithoutOptimization<TResult>(Expression<Func<TResult>> expression) =>
        ObserveWithoutOptimization<TResult>((LambdaExpression)expression);

    ObservableExpression<TArgument, TResult> Observe<TArgument, TResult>(TArgument argument, Expression? parameterReplacedExpression)
    {
        lock (cachedSingleArgumentObservableExpressionsAccess)
        {
            ObservableExpression<TArgument, TResult> typedInstance;
            if (cachedSingleArgumentObservableExpressions.TryGetValue(parameterReplacedExpression!, out var instance))
                typedInstance = (ObservableExpression<TArgument, TResult>)instance;
            else
            {
                typedInstance = new ObservableExpression<TArgument, TResult>(this, parameterReplacedExpression!, GetObservableExpression(parameterReplacedExpression!, false), argument);
                cachedSingleArgumentObservableExpressions.Add(parameterReplacedExpression!, typedInstance);
            }
            ++typedInstance.Observations;
            return typedInstance;
        }
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument, TResult> Observe<TArgument, TResult>(Expression<Func<TArgument, TResult>> expression, TArgument argument)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ReplaceParameters(expression, argument);
        return Observe<TArgument, TResult>(argument, parameterReplacedExpression);
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument, TResult> ObserveWithoutOptimization<TArgument, TResult>(Expression<Func<TArgument, TResult>> expression, TArgument argument)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ExpressionObserver.ReplaceParametersWithoutOptimization(expression, argument);
        return Observe<TArgument, TResult>(argument, parameterReplacedExpression);
    }

    ObservableExpression<TArgument1, TArgument2, TResult> Observe<TArgument1, TArgument2, TResult>(TArgument1 argument1, TArgument2 argument2, Expression? parameterReplacedExpression)
    {
        lock (cachedDoubleArgumentObservableExpressionsAccess)
        {
            ObservableExpression<TArgument1, TArgument2, TResult> typedInstance;
            if (cachedDoubleArgumentObservableExpressions.TryGetValue(parameterReplacedExpression!, out var instance))
                typedInstance = (ObservableExpression<TArgument1, TArgument2, TResult>)instance;
            else
            {
                typedInstance = new ObservableExpression<TArgument1, TArgument2, TResult>(this, parameterReplacedExpression!, GetObservableExpression(parameterReplacedExpression!, false), argument1, argument2);
                cachedDoubleArgumentObservableExpressions.Add(parameterReplacedExpression!, typedInstance);
            }
            ++typedInstance.Observations;
            return typedInstance;
        }
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument1, TArgument2, TResult> Observe<TArgument1, TArgument2, TResult>(Expression<Func<TArgument1, TArgument2, TResult>> expression, TArgument1 argument1, TArgument2 argument2)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ReplaceParameters(expression, argument1, argument2);
        return Observe<TArgument1, TArgument2, TResult>(argument1, argument2, parameterReplacedExpression);
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument1, TArgument2, TResult> ObserveWithoutOptimization<TArgument1, TArgument2, TResult>(Expression<Func<TArgument1, TArgument2, TResult>> expression, TArgument1 argument1, TArgument2 argument2)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ExpressionObserver.ReplaceParametersWithoutOptimization(expression, argument1, argument2);
        return Observe<TArgument1, TArgument2, TResult>(argument1, argument2, parameterReplacedExpression);
    }

    ObservableExpression<TArgument1, TArgument2, TArgument3, TResult> Observe<TArgument1, TArgument2, TArgument3, TResult>(TArgument1 argument1, TArgument2 argument2, TArgument3 argument3, Expression? parameterReplacedExpression)
    {
        lock (cachedTripleArgumentObservableExpressionsAccess)
        {
            ObservableExpression<TArgument1, TArgument2, TArgument3, TResult> typedInstance;
            if (cachedTripleArgumentObservableExpressions.TryGetValue(parameterReplacedExpression!, out var instance))
                typedInstance = (ObservableExpression<TArgument1, TArgument2, TArgument3, TResult>)instance;
            else
            {
                typedInstance = new ObservableExpression<TArgument1, TArgument2, TArgument3, TResult>(this, parameterReplacedExpression!, GetObservableExpression(parameterReplacedExpression!, false), argument1, argument2, argument3);
                cachedTripleArgumentObservableExpressions.Add(parameterReplacedExpression!, typedInstance);
            }
            ++typedInstance.Observations;
            return typedInstance;
        }
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument1, TArgument2, TArgument3, TResult> Observe<TArgument1, TArgument2, TArgument3, TResult>(Expression<Func<TArgument1, TArgument2, TArgument3, TResult>> expression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ReplaceParameters(expression, argument1, argument2, argument3);
        return Observe<TArgument1, TArgument2, TArgument3, TResult>(argument1, argument2, argument3, parameterReplacedExpression);
    }

    /// <inheritdoc/>
    [return: DisposeWhenDiscarded]
    public IObservableExpression<TArgument1, TArgument2, TArgument3, TResult> ObserveWithoutOptimization<TArgument1, TArgument2, TArgument3, TResult>(Expression<Func<TArgument1, TArgument2, TArgument3, TResult>> expression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var parameterReplacedExpression = ExpressionObserver.ReplaceParametersWithoutOptimization(expression, argument1, argument2, argument3);
        return Observe<TArgument1, TArgument2, TArgument3, TResult>(argument1, argument2, argument3, parameterReplacedExpression);
    }

    internal Expression? ReplaceParameters(LambdaExpression lambdaExpression, params object?[] arguments)
    {
        lambdaExpression = (LambdaExpression)(Optimizer?.Invoke(lambdaExpression) ?? lambdaExpression);
        return ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, arguments);
    }
}
