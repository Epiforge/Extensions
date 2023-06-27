namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableIndexExpression :
    ObservableExpression
{
    public ObservableIndexExpression(ExpressionObserver observer, IndexExpression indexExpression, bool deferEvaluation) :
        base(observer, indexExpression, deferEvaluation) =>
        IndexExpression = indexExpression;

    EquatableList<ObservableExpression>? arguments;
    MethodInfo? getMethod;
    PropertyInfo? indexer;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? @object;
    object? objectResult;

    internal readonly IndexExpression IndexExpression;

    void ArgumentPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                UnsubscribeFromObjectValueNotifications();
                if (@object is not null)
                {
                    @object.PropertyChanged -= ObjectPropertyChanged;
                    @object.Dispose();
                }
                if (arguments is { } nonNullArguments)
                    for (int i = 0, ii = nonNullArguments.Count; i < ii; ++i)
                    {
                        var argument = nonNullArguments[i];
                        argument.PropertyChanged -= ArgumentPropertyChanged;
                        argument.Dispose();
                    }
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        try
        {
            var (objectFault, objectResult) = @object?.Evaluation ?? (null, null);
            if (objectFault is not null)
                Evaluation = (objectFault, defaultResult);
            else if (arguments?.Select(argument => argument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } argumentFault)
                Evaluation = (argumentFault, defaultResult);
            else
            {
                if (!ReferenceEquals(objectResult, this.objectResult))
                {
                    UnsubscribeFromObjectValueNotifications();
                    this.objectResult = objectResult;
                    SubscribeToObjectValueNotifications();
                }
                Evaluation = (null, getMethod?.FastInvoke(this.objectResult, arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? Array.Empty<object?>()));
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        getMethod is not null && observer.IsMethodReturnValueDisposed(getMethod);

    void ObjectPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    [SuppressMessage("Code Analysis", "CA1502: Avoid excessive complexity")]
    void ObjectValueCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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

    void ObjectValueDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        if (e.Action == NotifyDictionaryChangedAction.Reset)
            Evaluate();
        else if (arguments?.Count == 1)
        {
            var removed = false;
            var key = arguments?[0].Evaluation.Result;
            if (key is not null)
            {
                removed = e.OldItems?.Any(kv => key.Equals(kv.Key)) ?? false;
                var keyValuePair = e.NewItems?.FirstOrDefault(kv => key.Equals(kv.Key)) ?? default;
                if (keyValuePair.Key is not null)
                {
                    removed = false;
                    Evaluation = (null, keyValuePair.Value);
                }
            }
            if (removed)
                Evaluation = (new KeyNotFoundException($"Key '{key}' was removed"), defaultResult);
        }
    }

    void ObjectValuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == indexer?.Name)
            Evaluate();
    }

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            indexer = IndexExpression.Indexer;
            getMethod = indexer!.GetMethod;
            @object = observer.GetObservableExpression(IndexExpression.Object!, IsDeferringEvaluation);
            @object.PropertyChanged += ObjectPropertyChanged;
            var indexedExpressionArguments = IndexExpression.Arguments;
            for (var i = 0; i < indexedExpressionArguments.Count; ++i)
            {
                var indexExpressionArgument = indexedExpressionArguments[i];
                var argument = observer.GetObservableExpression(indexExpressionArgument, IsDeferringEvaluation);
                argument.PropertyChanged += ArgumentPropertyChanged;
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
                @object.PropertyChanged -= ObjectPropertyChanged;
                @object.Dispose();
            }
            for (int i = 0, ii = argumentsList.Count; i < ii; ++i)
            {
                var argument = argumentsList[i];
                argument.PropertyChanged -= ArgumentPropertyChanged;
                argument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void SubscribeToObjectValueNotifications()
    {
        if (objectResult is INotifyDictionaryChanged dictionaryChangedNotifier)
            dictionaryChangedNotifier.DictionaryChanged += ObjectValueDictionaryChanged;
        else if (objectResult is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += ObjectValueCollectionChanged;
        if (objectResult is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += ObjectValuePropertyChanged;
    }

    void UnsubscribeFromObjectValueNotifications()
    {
        if (objectResult is INotifyDictionaryChanged dictionaryChangedNotifier)
            dictionaryChangedNotifier.DictionaryChanged -= ObjectValueDictionaryChanged;
        else if (objectResult is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged -= ObjectValueCollectionChanged;
        if (objectResult is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged -= ObjectValuePropertyChanged;
    }
}
