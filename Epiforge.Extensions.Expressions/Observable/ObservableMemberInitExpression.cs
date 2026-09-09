namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMemberInitExpression(ExpressionObserver observer, MemberInitExpression memberInitExpression, bool deferEvaluation) :
    ObservableExpression(observer, memberInitExpression, deferEvaluation),
    IObservableExpressionDependent
{
    IReadOnlyDictionary<ObservableExpression, (MemberInfo Member, ObservableExpressionSubscription? Subscription)>? memberAssignmentObservableExpressions;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableNewExpression? newObservableExpression;
    ObservableExpressionSubscription? newObservableExpressionSubscription;

    internal readonly MemberInitExpression MemberInitExpression = memberInitExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            DisposeValueIfNecessaryAndPossible();
            if (newObservableExpression is not null)
            {
                if (newObservableExpressionSubscription is { } newObservableExpressionDependency)
                    newObservableExpression.UnsubscribeDependent(newObservableExpressionDependency);
                newObservableExpression.Dispose();
            }
            if (memberAssignmentObservableExpressions is not null)
                foreach (var kv in memberAssignmentObservableExpressions)
                {
                    if (kv.Value.Subscription is { } memberAssignmentDependency)
                        kv.Key.UnsubscribeDependent(memberAssignmentDependency);
                    kv.Key.Dispose();
                }
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void Evaluate()
    {
        try
        {
            var newObservableExpressionFault = newObservableExpression?.Evaluation.Fault;
            if (newObservableExpressionFault is not null)
            {
                Evaluation = (newObservableExpressionFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, newObservableExpressionFault, "{MemberInitExpression} new faulted: {Fault}", MemberInitExpression, newObservableExpressionFault);
            }
            else if (FirstMemberAssignmentFault() is { } memberAssignmentObservableExpressionFault)
            {
                Evaluation = (memberAssignmentObservableExpressionFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, memberAssignmentObservableExpressionFault, "{MemberInitExpression} member assignment faulted: {Fault}", MemberInitExpression, memberAssignmentObservableExpressionFault);
            }
            else
            {
                var value = newObservableExpression?.Construct();
                if (memberAssignmentObservableExpressions is not null)
                    foreach (var kv in memberAssignmentObservableExpressions)
                    {
                        if (kv.Value.Member is FieldInfo field)
                            field.SetValue(value, kv.Key.Evaluation.Result);
                        else if (kv.Value.Member is PropertyInfo property)
                            property.FastSetValue(value, kv.Key.Evaluation.Result);
                        else
                            throw new NotSupportedException("Cannot handle member that is not a field or property");
                    }
                Evaluation = (null, value);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{MemberInitExpression} evaluated: {Value}", MemberInitExpression, value);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{MemberInitExpression} faulted: {Fault}", MemberInitExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        newObservableExpression?.ShouldConstructedValueBeDisposed ?? false;

    /// <summary>
    /// Yields the fault of the first member assignment which has one, written as a loop here rather than through the shared helper because these expressions are the keys of a dictionary rather than a list
    /// </summary>
    Exception? FirstMemberAssignmentFault()
    {
        if (memberAssignmentObservableExpressions is null)
            return null;
        foreach (var memberAssignmentObservableExpression in memberAssignmentObservableExpressions.Keys)
            if (memberAssignmentObservableExpression.Evaluation.Fault is { } fault)
                return fault;
        return null;
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();

    protected override void OnInitialization()
    {
        if (MemberInitExpression.NewExpression.Type.IsValueType)
            throw new NotSupportedException("Member initialization expressions of value types are not supported");
        var memberAssignmentObservableExpressions = new Dictionary<ObservableExpression, (MemberInfo Member, ObservableExpressionSubscription? Subscription)>(ObservableExpressionEqualityComparer.Default);
        try
        {
            newObservableExpression = (ObservableNewExpression)observer.GetObservableExpression(MemberInitExpression.NewExpression, IsDeferringEvaluation);
            if (newObservableExpression.CanChange)
                newObservableExpressionSubscription = newObservableExpression.SubscribeDependent(this);
            var bindings = MemberInitExpression.Bindings;
            for (int i = 0, ii = bindings.Count; i < ii; ++i)
            {
                var binding = bindings[i];
                if (binding is MemberAssignment memberAssignmentBinding)
                {
                    var memberAssignmentObservableExpression = observer.GetObservableExpression(memberAssignmentBinding.Expression, IsDeferringEvaluation);
                    memberAssignmentObservableExpressions.Add(memberAssignmentObservableExpression, (memberAssignmentBinding.Member, memberAssignmentObservableExpression.CanChange ? memberAssignmentObservableExpression.SubscribeDependent(this) : null));
                }
                else
                    throw new NotSupportedException("Only member assignment bindings are supported in member init expressions");
            }
            this.memberAssignmentObservableExpressions = memberAssignmentObservableExpressions;
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (newObservableExpression is not null)
            {
                if (newObservableExpressionSubscription is { } newObservableExpressionDependency)
                    newObservableExpression.UnsubscribeDependent(newObservableExpressionDependency);
                newObservableExpression.Dispose();
            }
            foreach (var kv in memberAssignmentObservableExpressions)
            {
                if (kv.Value.Subscription is { } memberAssignmentDependency)
                    kv.Key.UnsubscribeDependent(memberAssignmentDependency);
                kv.Key.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
