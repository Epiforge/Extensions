namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMemberExpression(ExpressionObserver observer, MemberExpression memberExpression, bool deferEvaluation) :
    ObservableExpression(observer, memberExpression, deferEvaluation),
    IObservableExpressionDependent
{
    SourceNotificationAttachment? contentsAttachment;
    bool doNotListenForPropertyChanges;
    FieldInfo? field;
    MethodInfo? getMethod;
    FastInvoker? getMethodInvoker;
    bool isFieldOfCompilerGeneratedType;
    MemberInfo? member;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableExpression;
    object? observableExpressionResult;
    ObservableExpressionSubscription? observableExpressionSubscription;
    SourceNotificationAttachment? propertyAttachment;

    internal readonly MemberExpression MemberExpression = memberExpression;

    protected override bool DisposeCore()
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
                if (observableExpressionSubscription is { } observableExpressionDependency)
                    observableExpression.UnsubscribeDependent(observableExpressionDependency);
                observableExpression.Dispose();
            }
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void Evaluate()
    {
        try
        {
            var (observableExpressionFault, observableExpressionResult) = observableExpression?.Evaluation ?? (null, null);
            if (observableExpressionFault is not null)
            {
                Evaluation = (observableExpressionFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, observableExpressionFault, "{MemberExpression} faulted: {Fault}", MemberExpression, observableExpressionFault);
            }
            else if (getMethod is not null)
            {
                if (observableExpressionResult != this.observableExpressionResult)
                {
                    UnsubscribeFromExpressionValueNotifications();
                    this.observableExpressionResult = observableExpressionResult;
                    SubscribeToExpressionValueNotifications();
                }
                if (!getMethod.IsStatic && observableExpressionResult is null)
                {
                    var nullReference = new NullReferenceException();
                    Evaluation = (nullReference, defaultResult);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, nullReference, "{MemberExpression} object was null: {Fault}", MemberExpression, nullReference);
                }
                else
                {
                    var value = getMethodInvoker!.Invoke(observableExpressionResult);
                    Evaluation = (null, value);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{MemberExpression} evaluated: {Value}", MemberExpression, value);
                }
            }
            else if (field is { IsStatic: false } && observableExpressionResult is null)
            {
                var nullReference = new NullReferenceException();
                Evaluation = (nullReference, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, nullReference, "{MemberExpression} object was null: {Fault}", MemberExpression, nullReference);
            }
            else if (field is not null)
            {
                var previousValue = Evaluation.Result;
                var value = field.GetValue(observableExpressionResult);
                if (!ReferenceEquals(previousValue, value))
                {
                    UnsubscribeFromValueNotifications();
                    Evaluation = (null, value);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{MemberExpression} evaluated: {Value}", MemberExpression, value);
                    SubscribeToValueNotifications();
                }
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{MemberExpression} faulted: {Fault}", MemberExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        getMethod is not null && observer.IsMethodReturnValueDisposed(getMethod);

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();

    void ObservableExpressionValuePropertyChanged(object? sender, EventArgs eventArgs)
    {
        var e = (PropertyChangedEventArgs)eventArgs;
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == member?.Name)
            Evaluate();
    }

    protected override void OnInitialization()
    {
        try
        {
            if (MemberExpression.Expression is { } memberExpressionExpression)
            {
                observableExpression = observer.GetObservableExpression(memberExpressionExpression, IsDeferringEvaluation);
                if (observableExpression.CanChange)
                    observableExpressionSubscription = observableExpression.SubscribeDependent(this);
            }
            member = MemberExpression.Member;
            switch (member)
            {
                case FieldInfo field:
                    this.field = field;
                    isFieldOfCompilerGeneratedType = MemberExpression.Expression?.Type.Name.StartsWith('<') ?? false;
                    break;
                case PropertyInfo property:
                    doNotListenForPropertyChanges = observer.IsIgnoredPropertyChangeNotification(property);
                    getMethod = property.GetMethod;
                    getMethodInvoker = getMethod?.GetFastInvoker();
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
                if (observableExpressionSubscription is { } observableExpressionDependency)
                    observableExpression.UnsubscribeDependent(observableExpressionDependency);
                observableExpression.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void SubscribeToExpressionValueNotifications()
    {
        if (doNotListenForPropertyChanges)
            return;
        if (observableExpressionResult is INotifyPropertyChanged)
            propertyAttachment = observer.SourceNotifications.Attach(observableExpressionResult, SourceNotificationKind.PropertyChanged, ObservableExpressionValuePropertyChanged);
    }

    void SubscribeToValueNotifications()
    {
        if (isFieldOfCompilerGeneratedType && Evaluation.Result is { } value)
        {
            if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged && value is INotifyDictionaryChanged)
                contentsAttachment = observer.SourceNotifications.Attach(value, SourceNotificationKind.DictionaryChanged, ValueChanged);
            else if (observer.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged && value is INotifyCollectionChanged)
                contentsAttachment = observer.SourceNotifications.Attach(value, SourceNotificationKind.CollectionChanged, ValueChanged);
        }
    }

    void UnsubscribeFromExpressionValueNotifications()
    {
        observer.SourceNotifications.Detach(propertyAttachment);
        propertyAttachment = null;
    }

    void UnsubscribeFromValueNotifications()
    {
        observer.SourceNotifications.Detach(contentsAttachment);
        contentsAttachment = null;
    }

    void ValueChanged(object? sender, EventArgs e) =>
        NotifyDependentsOfValueContentsChanged();
}
