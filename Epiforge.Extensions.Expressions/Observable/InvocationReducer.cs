namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Replaces every invocation of a literal lambda in an expression with the body that invocation would have evaluated, and every default value with the constant it evaluates to, so that what is analyzed and what is compiled contain no invocation or default value the analyzer would refuse
/// </summary>
/// <remarks>
/// The graph performs the same reduction in <see cref="ObservableInvocationExpression"/>, substituting each argument's evaluated value where this substitutes the argument expression itself; the two were measured to take the same subscriptions, which is what makes admitting the reduced form an agreement with the graph rather than an approximation of it. A parameter referenced other than exactly once is not substituted: referenced twice, the argument would be evaluated twice where the graph evaluates it once, and referenced not at all, the argument would be dropped along with the subscription the graph still takes for it
/// </remarks>
/// <remarks>
/// A parameter referenced inside an operand whose evaluation is deferred, or inside a try, is not substituted either, unless its argument is a constant or a parameter. The graph evaluates every argument before the body and reports a fault any of them raised, where the substituted argument would be evaluated only if that operand is reached, or inside a try which would catch its fault. A constant or a parameter can raise nothing, so it is substituted wherever it is referenced
/// </remarks>
/// <remarks>
/// An invocation whose arguments cannot all be substituted is bound as a let instead: a block assigns each argument, in order, to a variable which the body then reads wherever its parameter was referenced, except a constant or a parameter which the body reads, which is substituted as before. Every argument is therefore evaluated once and before the body, and the first to fault faults the invocation, which is what the graph's node does
/// </remarks>
sealed class InvocationReducer :
    ExpressionVisitor
{
    sealed class ParameterReferenceCounter(IReadOnlyCollection<ParameterExpression> parameters) :
        ExpressionVisitor
    {
        int deferral;

        internal readonly HashSet<ParameterExpression> Deferred = [];
        internal readonly Dictionary<ParameterExpression, int> References = parameters.ToDictionary(parameter => parameter, _ => 0);

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is not (ExpressionType.AndAlso or ExpressionType.OrElse or ExpressionType.Coalesce))
                return base.VisitBinary(node);
            Visit(node.Left);
            VisitDeferred(node.Right);
            Visit(node.Conversion);
            return node;
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Visit(node.Test);
            VisitDeferred(node.IfTrue);
            VisitDeferred(node.IfFalse);
            return node;
        }

        void VisitDeferred(Expression node)
        {
            ++deferral;
            Visit(node);
            --deferral;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (References.ContainsKey(node))
            {
                ++References[node];
                if (deferral > 0)
                    Deferred.Add(node);
            }
            return base.VisitParameter(node);
        }

        protected override Expression VisitTry(TryExpression node)
        {
            ++deferral;
            base.VisitTry(node);
            --deferral;
            return node;
        }
    }

    static readonly ConditionalWeakTable<ParameterExpression, object> letVariables = [];
    static readonly object read = new();
    static readonly object unread = new();

    /// <summary>
    /// Yields the constant a default value evaluates to, which the graph also substitutes for it when it replaces parameters, so that the two mechanisms observe the same constant
    /// </summary>
    /// <remarks>
    /// A value type's default is its zeroed instance, which is not necessarily what its parameterless constructor produces, so it is made without running one; a nullable value type's default is null
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The default of a value type is its instance with every field zeroed, which no constructor makes, so nothing a constructor needs has to survive trimming")]
    internal static ConstantExpression Constant(DefaultExpression defaultExpression) =>
        Expression.Constant(defaultExpression.Type.IsValueType && Nullable.GetUnderlyingType(defaultExpression.Type) is null ? RuntimeHelpers.GetUninitializedObject(defaultExpression.Type) : null, defaultExpression.Type);

    /// <summary>
    /// Determines whether a parameter is a variable a let binds an argument to
    /// </summary>
    internal static bool IsLetVariable(ParameterExpression parameterExpression) =>
        letVariables.TryGetValue(parameterExpression, out _);

    /// <summary>
    /// Determines whether a parameter is a variable a let binds an argument to which the body reads
    /// </summary>
    internal static bool IsReadLetVariable(ParameterExpression parameterExpression) =>
        letVariables.TryGetValue(parameterExpression, out var reference) && ReferenceEquals(reference, read);

    internal static LambdaExpression Reduce(LambdaExpression lambdaExpression) =>
        (LambdaExpression)new InvocationReducer().Visit(lambdaExpression);

    static bool EachArgumentCanBeSubstituted(ReadOnlyCollection<ParameterExpression> parameters, ReadOnlyCollection<Expression> arguments, ParameterReferenceCounter counter)
    {
        for (int i = 0, ii = parameters.Count; i < ii; ++i)
        {
            var parameter = parameters[i];
            if (counter.References[parameter] != 1 || counter.Deferred.Contains(parameter) && arguments[i] is not (ConstantExpression or ParameterExpression))
                return false;
        }
        return true;
    }

    static Expression? Let(LambdaExpression lambdaExpression, ReadOnlyCollection<Expression> arguments, ParameterReferenceCounter counter)
    {
        var parameters = lambdaExpression.Parameters;
        var substitutions = new Expression[parameters.Count];
        var variables = new List<ParameterExpression>();
        var expressions = new List<Expression>();
        for (int i = 0, ii = parameters.Count; i < ii; ++i)
        {
            var parameter = parameters[i];
            var argument = arguments[i];
            var references = counter.References[parameter];
            if (references > 0 && argument is ConstantExpression or ParameterExpression)
            {
                substitutions[i] = argument;
                continue;
            }
            var variable = Expression.Variable(parameter.Type, parameter.Name);
            letVariables.AddOrUpdate(variable, references > 0 ? read : unread);
            variables.Add(variable);
            expressions.Add(Expression.Assign(variable, argument));
            substitutions[i] = variable;
        }
        if (LambdaInvocationRewriter.Apply(lambdaExpression, substitutions) is not { } body)
            return null;
        if (variables.Count == 0)
            return body;
        expressions.Add(body);
        return Expression.Block(body.Type, variables, expressions);
    }

    protected override Expression VisitDefault(DefaultExpression node) =>
        node.Type == typeof(void) ? node : Constant(node);

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        var visited = (InvocationExpression)base.VisitInvocation(node);
        if (visited.Expression is not LambdaExpression lambdaExpression)
            return visited;
        var counter = new ParameterReferenceCounter(lambdaExpression.Parameters);
        counter.Visit(lambdaExpression.Body);
        if (EachArgumentCanBeSubstituted(lambdaExpression.Parameters, visited.Arguments, counter) && LambdaInvocationRewriter.Apply(lambdaExpression, [.. visited.Arguments]) is { } reduced)
            return Visit(reduced)!;
        return Let(lambdaExpression, visited.Arguments, counter) is { } let ? Visit(let)! : visited;
    }
}
