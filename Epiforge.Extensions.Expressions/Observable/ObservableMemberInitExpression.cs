namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMemberInitExpression :
    ObservableExpression
{
    public ObservableMemberInitExpression(ExpressionObserver observer, MemberInitExpression memberInitExpression, bool deferEvaluation) :
        base(observer, memberInitExpression, deferEvaluation) =>
        MemberInitExpression = memberInitExpression;

    IReadOnlyDictionary<ObservableExpression, MemberInfo>? memberAssignmentObservableExpressions;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? newObservableExpression;

    internal readonly MemberInitExpression MemberInitExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                if (newObservableExpression is not null)
                {
                    newObservableExpression.PropertyChanged -= NewObservableExpressionPropertyChanged;
                    newObservableExpression.Dispose();
                }
                if (memberAssignmentObservableExpressions is not null)
                    foreach (var kv in memberAssignmentObservableExpressions)
                    {
                        kv.Key.PropertyChanged -= MemberAssignmentObservableExpressionPropertyChanged;
                        kv.Key.Dispose();
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
            var (newObservableExpressionFault, newObservableExpressionResult) = newObservableExpression?.Evaluation ?? (null, null);
            if (newObservableExpressionFault is not null)
                Evaluation = (newObservableExpressionFault, defaultResult);
            else if (memberAssignmentObservableExpressions?.Keys.Select(memberAssignmentObservableExpression => memberAssignmentObservableExpression.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } memberAssignmentObservableExpressionFault)
                Evaluation = (memberAssignmentObservableExpressionFault, defaultResult);
            else
            {
                if (memberAssignmentObservableExpressions is not null)
                    foreach (var kv in memberAssignmentObservableExpressions)
                    {
                        if (kv.Value is FieldInfo field)
                            field.SetValue(newObservableExpressionResult, kv.Key.Evaluation.Result);
                        else if (kv.Value is PropertyInfo property)
                            property.FastSetValue(newObservableExpressionResult, kv.Key.Evaluation.Result);
                        else
                            throw new NotSupportedException("Cannot handle member that is not a field or property");
                    }
                Evaluation = (null, newObservableExpressionResult);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
        }
    }

    void MemberAssignmentObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ObservableExpression memberAssignmentObservableExpression && (memberAssignmentObservableExpressions?.TryGetValue(memberAssignmentObservableExpression, out var member) ?? false))
        {
            var (memberAssignmentObservableExpressionFault, memberAssignmentObservableExpressionResult) = memberAssignmentObservableExpression.Evaluation;
            if (memberAssignmentObservableExpressionFault is not null)
                Evaluation = (memberAssignmentObservableExpressionFault, defaultResult);
            else
            {
                var intactResult = TryGetUndeferredResult(out var result) && result is not null;
                if (!intactResult)
                    result = newObservableExpression?.Evaluation.Result;
                if (result is not null)
                {
                    if (member is FieldInfo field)
                        field.SetValue(result, memberAssignmentObservableExpressionResult);
                    else if (member is PropertyInfo property)
                        property.FastSetValue(result, memberAssignmentObservableExpressionResult);
                    else
                        throw new NotSupportedException("Cannot handle member that is not a field or property");
                }
                if (!intactResult)
                    Evaluation = (null, result);
            }
        }
    }

    void NewObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        if (MemberInitExpression.NewExpression.Type.IsValueType)
            throw new NotSupportedException("Member initialization expressions of value types are not supported");
        var memberAssignmentObservableExpressions = new Dictionary<ObservableExpression, MemberInfo>(ObservableExpressionEqualityComparer.Default);
        try
        {
            newObservableExpression = observer.GetObservableExpression(MemberInitExpression.NewExpression, IsDeferringEvaluation);
            newObservableExpression.PropertyChanged += NewObservableExpressionPropertyChanged;
            var bindings = MemberInitExpression.Bindings;
            for (int i = 0, ii = bindings.Count; i < ii; ++i)
            {
                var binding = bindings[i];
                if (binding is MemberAssignment memberAssignmentBinding)
                {
                    var memberAssignmentObservableExpression = observer.GetObservableExpression(memberAssignmentBinding.Expression, IsDeferringEvaluation);
                    memberAssignmentObservableExpressions.Add(memberAssignmentObservableExpression, memberAssignmentBinding.Member);
                    memberAssignmentObservableExpression.PropertyChanged += MemberAssignmentObservableExpressionPropertyChanged;
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
                newObservableExpression.PropertyChanged -= NewObservableExpressionPropertyChanged;
                newObservableExpression.Dispose();
            }
            foreach (var memberAssignmentObservableExpression in memberAssignmentObservableExpressions.Keys)
            {
                memberAssignmentObservableExpression.PropertyChanged -= MemberAssignmentObservableExpressionPropertyChanged;
                memberAssignmentObservableExpression.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
