namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMemberExpression :
    ObservableExpression
{
    public ObservableMemberExpression(ExpressionObserver observer, MemberExpression memberExpression, bool deferEvaluation) :
        base(observer, memberExpression, deferEvaluation) =>
        MemberExpression = memberExpression;

    bool doNotListenForPropertyChanges;
    FieldInfo? field;
    MethodInfo? getMethod;
    bool isFieldOfCompilerGeneratedType;
    MemberInfo? member;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableExpression;
    object? observableExpressionResult;

    internal readonly MemberExpression MemberExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                if (getMethod is not null)
                    UnsubscribeFromExpressionValueNotifications();
                else if (field is not null)
                    UnsubscribeFromValueNotifications();
                if (observableExpression is not null)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                base.Dispose(disposing);
            }
            return removedFromCache;
        }
        return base.Dispose(disposing);
    }

    protected override void Evaluate()
    {
        try
        {
            var (observableExpressionFault, observableExpressionResult) = observableExpression?.Evaluation ?? (null, null);
            if (observableExpressionFault is not null)
            {
                Evaluation = (observableExpressionFault, defaultResult);
                observer.Logger?.LogTrace("{MemberExpression} faulted: {Fault}", MemberExpression, observableExpressionFault);
            }
            else if (getMethod is not null)
            {
                if (observableExpressionResult != this.observableExpressionResult)
                {
                    UnsubscribeFromExpressionValueNotifications();
                    this.observableExpressionResult = observableExpressionResult;
                    SubscribeToExpressionValueNotifications();
                }
                var value = getMethod.FastInvoke(observableExpressionResult, Array.Empty<object?>());
                Evaluation = (null, value);
                observer.Logger?.LogTrace("{MemberExpression} evaluated: {Value}", MemberExpression, value);
            }
            else if (field is not null)
            {
                UnsubscribeFromValueNotifications();
                var value = field.GetValue(observableExpressionResult);
                Evaluation = (null, value);
                observer.Logger?.LogTrace("{MemberExpression} evaluated: {Value}", MemberExpression, value);
                SubscribeToValueNotifications();
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace("{MemberExpression} faulted: {Fault}", MemberExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        getMethod is not null && observer.IsMethodReturnValueDisposed(getMethod);

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    void ObservableExpressionValuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == member?.Name)
            Evaluate();
    }

    protected override void OnInitialization()
    {
        try
        {
            if (MemberExpression.Expression is { } memberExpressionExpression)
            {
                observableExpression = observer.GetObservableExpression(memberExpressionExpression, IsDeferringEvaluation);
                observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
            }
            member = MemberExpression.Member;
            switch (member)
            {
                case FieldInfo field:
                    this.field = field;
                    isFieldOfCompilerGeneratedType = MemberExpression.Expression?.Type.Name.StartsWith("<") ?? false;
                    break;
                case PropertyInfo property:
                    doNotListenForPropertyChanges = observer.IsIgnoredPropertyChangeNotification(property);
                    getMethod = property.GetMethod;
                    isFieldOfCompilerGeneratedType = false;
                    break;
            }
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (getMethod is not null)
                UnsubscribeFromExpressionValueNotifications();
            else if (field is not null)
                UnsubscribeFromValueNotifications();
            if (observableExpression is not null)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void SubscribeToExpressionValueNotifications()
    {
        if (doNotListenForPropertyChanges)
            return;
        if (observableExpressionResult is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged += ObservableExpressionValuePropertyChanged;
    }

    void SubscribeToValueNotifications()
    {
        if (isFieldOfCompilerGeneratedType)
        {
            if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged && Evaluation.Result is INotifyDictionaryChanged dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged += ValueChanged;
            else if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged && Evaluation.Result is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged += ValueChanged;
        }
    }

    void UnsubscribeFromExpressionValueNotifications()
    {
        if (doNotListenForPropertyChanges)
            return;
        if (observableExpressionResult is INotifyPropertyChanged propertyChangedNotifier)
            propertyChangedNotifier.PropertyChanged -= ObservableExpressionValuePropertyChanged;
    }

    void UnsubscribeFromValueNotifications()
    {
        if (isFieldOfCompilerGeneratedType && TryGetUndeferredResult(out var value))
        {
            if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged && value is INotifyDictionaryChanged dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged -= ValueChanged;
            else if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged && value is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged -= ValueChanged;
        }
    }

    void ValueChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(Evaluation));
}
