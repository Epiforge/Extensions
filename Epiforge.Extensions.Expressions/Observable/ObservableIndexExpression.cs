namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableIndexExpression(ExpressionObserver observer, IndexExpression indexExpression, bool deferEvaluation) :
    ObservableExpression(observer, indexExpression, deferEvaluation),
    IObservableExpressionDependent
{
    EquatableList<ObservableExpression>? arguments;
    ObservableExpressionSubscription?[]? argumentSubscriptions;
    /// <summary>
    /// The name by which .NET announces that an indexer changed, which is the indexer's own name followed by empty brackets and not the name alone
    /// </summary>
    string? conventionalIndexerName;
    SourceNotificationAttachment? contentsAttachment;
    SourceNotificationAttachment? propertyAttachment;
    MethodInfo? getMethod;
    FastInvoker? getMethodInvoker;
    PropertyInfo? indexer;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? @object;
    ObservableExpressionSubscription? objectSubscription;
    object? objectResult;

    internal readonly IndexExpression IndexExpression = indexExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            DisposeValueIfNecessaryAndPossible();
            UnsubscribeFromObjectValueNotifications();
            if (@object is not null)
            {
                if (objectSubscription is { } objectDependency)
                    @object.UnsubscribeDependent(objectDependency);
                @object.Dispose();
            }
            if (arguments is { } nonNullArguments)
                for (int i = 0, ii = nonNullArguments.Count; i < ii; ++i)
                {
                    var argument = nonNullArguments[i];
                    if (argumentSubscriptions?[i] is { } argumentDependency)
                        argument.UnsubscribeDependent(argumentDependency);
                    argument.Dispose();
                }
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void Evaluate()
    {
        try
        {
            var (objectFault, objectResult) = @object?.Evaluation ?? (null, null);
            if (objectFault is not null)
            {
                Evaluation = (objectFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, objectFault, "{IndexExpression} object expression faulted: {Fault}", IndexExpression, objectFault);
            }
            else if (arguments is { } faultedArguments && FirstFault(faultedArguments) is { } argumentFault)
            {
                Evaluation = (argumentFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, argumentFault, "{IndexExpression} argument expression faulted: {Fault}", IndexExpression, argumentFault);
            }
            else
            {
                if (!ReferenceEquals(objectResult, this.objectResult))
                {
                    UnsubscribeFromObjectValueNotifications();
                    this.objectResult = objectResult;
                    SubscribeToObjectValueNotifications();
                }
                if (getMethod is { IsStatic: false } && this.objectResult is null)
                {
                    var nullReference = new NullReferenceException();
                    Evaluation = (nullReference, defaultResult);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, nullReference, "{IndexExpression} object was null: {Fault}", IndexExpression, nullReference);
                }
                else
                {
                    var value = getMethodInvoker is { } invoker ? Invoke(invoker, this.objectResult, arguments) : null;
                    Evaluation = (null, value);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{IndexExpression} evaluated: {Value}", IndexExpression, value);
                }
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{IndexExpression} faulted: {Fault}", IndexExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        getMethod is not null && observer.IsMethodReturnValueDisposed(getMethod);

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        EvaluateOnce();

    [SuppressMessage("Code Analysis", "CA1502: Avoid excessive complexity")]
    void ObjectValueCollectionChanged(object? sender, EventArgs eventArgs)
    {
        var e = (NotifyCollectionChangedEventArgs)eventArgs;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewStartingIndex >= 0 && (e.NewItems?.Count ?? 0) > 0 && arguments?.Count == 1 && arguments?[0].Evaluation.Result is int index && e.NewStartingIndex <= index)
                        Evaluate();
                }
                break;
            case NotifyCollectionChangedAction.Move:
                {
                    var movingCount = Math.Max(e.OldItems?.Count ?? 0, e.NewItems?.Count ?? 0);
                    if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && movingCount > 0 && arguments?.Count == 1 && arguments?[0].Evaluation.Result is int index && (index >= e.OldStartingIndex && index < e.OldStartingIndex + movingCount || index >= e.NewStartingIndex && index < e.NewStartingIndex + movingCount))
                        Evaluate();
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                {
                    if (e.OldStartingIndex >= 0 && (e.OldItems?.Count ?? 0) > 0 && arguments?.Count == 1 && arguments?[0].Evaluation.Result is int index && e.OldStartingIndex <= index)
                        Evaluate();
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                {
                    if (arguments?.Count == 1 && arguments?[0].Evaluation.Result is int index)
                    {
                        var oldCount = e.OldItems?.Count ?? 0;
                        var newCount = e.NewItems?.Count ?? 0;
                        if (oldCount != newCount && (e.OldStartingIndex >= 0 || e.NewStartingIndex >= 0) && index >= Math.Min(Math.Max(e.OldStartingIndex, 0), Math.Max(e.NewStartingIndex, 0)) || e.OldStartingIndex >= 0 && index >= e.OldStartingIndex && index < e.OldStartingIndex + oldCount || e.NewStartingIndex >= 0 && index >= e.NewStartingIndex && index < e.NewStartingIndex + newCount)
                            Evaluate();
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Evaluate();
                break;
        }
    }

    void ObjectValueDictionaryChanged(object? sender, EventArgs eventArgs)
    {
        var e = (NotifyDictionaryChangedEventArgs<object?, object?>)eventArgs;
        if (e.Action == NotifyDictionaryChangedAction.Reset)
            Evaluate();
        else if (arguments is { Count: 1 } indexArguments && indexArguments[0].Evaluation.Result is { } key)
        {
            var newItems = e.NewItems;
            for (int i = 0, ii = newItems.Count; i < ii; ++i)
                if (key.Equals(newItems[i].Key))
                {
                    Evaluate();
                    return;
                }
            var oldItems = e.OldItems;
            for (int i = 0, ii = oldItems.Count; i < ii; ++i)
                if (key.Equals(oldItems[i].Key))
                {
                    Evaluate();
                    return;
                }
        }
    }

    void ObjectValuePropertyChanged(object? sender, EventArgs eventArgs)
    {
        var e = (PropertyChangedEventArgs)eventArgs;
        if (e.PropertyName == indexer?.Name || e.PropertyName == conventionalIndexerName)
            Evaluate();
    }

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            indexer = IndexExpression.Indexer;
            getMethod = indexer!.GetMethod;
            getMethodInvoker = getMethod?.GetFastInvoker();
            conventionalIndexerName = indexer.Name + "[]";
            @object = observer.GetObservableExpression(IndexExpression.Object!, IsDeferringEvaluation);
            if (@object.CanChange)
                objectSubscription = @object.SubscribeDependent(this);
            var indexedExpressionArguments = IndexExpression.Arguments;
            var subscriptions = new ObservableExpressionSubscription?[indexedExpressionArguments.Count];
            argumentSubscriptions = subscriptions;
            for (var i = 0; i < indexedExpressionArguments.Count; ++i)
            {
                var indexExpressionArgument = indexedExpressionArguments[i];
                var argument = observer.GetObservableExpression(indexExpressionArgument, IsDeferringEvaluation);
                if (argument.CanChange)
                    subscriptions[i] = argument.SubscribeDependent(this);
                argumentsList.Add(argument);
            }
            arguments = new EquatableList<ObservableExpression>(argumentsList);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            UnsubscribeFromObjectValueNotifications();
            if (@object is not null)
            {
                if (objectSubscription is { } objectDependency)
                    @object.UnsubscribeDependent(objectDependency);
                @object.Dispose();
            }
            for (int i = 0, ii = argumentsList.Count; i < ii; ++i)
            {
                var argument = argumentsList[i];
                if (argumentSubscriptions?[i] is { } argumentDependency)
                    argument.UnsubscribeDependent(argumentDependency);
                argument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void SubscribeToObjectValueNotifications()
    {
        if (objectResult is INotifyDictionaryChanged)
            contentsAttachment = observer.SourceNotifications.Attach(objectResult, SourceNotificationKind.DictionaryChanged, ObjectValueDictionaryChanged);
        else if (objectResult is INotifyCollectionChanged)
            contentsAttachment = observer.SourceNotifications.Attach(objectResult, SourceNotificationKind.CollectionChanged, ObjectValueCollectionChanged);
        if (objectResult is INotifyPropertyChanged)
            propertyAttachment = observer.SourceNotifications.Attach(objectResult, SourceNotificationKind.PropertyChanged, ObjectValuePropertyChanged);
    }

    void UnsubscribeFromObjectValueNotifications()
    {
        observer.SourceNotifications.Detach(contentsAttachment);
        contentsAttachment = null;
        observer.SourceNotifications.Detach(propertyAttachment);
        propertyAttachment = null;
    }
}
