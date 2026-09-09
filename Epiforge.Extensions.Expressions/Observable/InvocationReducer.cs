namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Replaces every invocation of a literal lambda in an expression with the body that invocation would have evaluated, so that what is analyzed and what is compiled contain no invocation the analyzer would refuse
/// </summary>
/// <remarks>
/// The graph performs the same reduction in <see cref="ObservableInvocationExpression"/>, substituting each argument's evaluated value where this substitutes the argument expression itself; the two were measured to take the same subscriptions, which is what makes admitting the reduced form an agreement with the graph rather than an approximation of it. A parameter referenced other than exactly once is refused: referenced twice, the argument would be evaluated twice where the graph evaluates it once, and referenced not at all, the argument would be dropped along with the subscription the graph still takes for it
/// </remarks>
sealed class InvocationReducer :
    ExpressionVisitor
{
    sealed class ParameterReferenceCounter(IReadOnlyCollection<ParameterExpression> parameters) :
        ExpressionVisitor
    {
        internal readonly Dictionary<ParameterExpression, int> References = parameters.ToDictionary(parameter => parameter, _ => 0);

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (References.ContainsKey(node))
                ++References[node];
            return base.VisitParameter(node);
        }
    }

    internal static LambdaExpression Reduce(LambdaExpression lambdaExpression) =>
        (LambdaExpression)new InvocationReducer().Visit(lambdaExpression);

    static bool EachParameterIsReferencedExactlyOnce(LambdaExpression lambdaExpression)
    {
        if (lambdaExpression.Parameters.Count == 0)
            return true;
        var counter = new ParameterReferenceCounter(lambdaExpression.Parameters);
        counter.Visit(lambdaExpression.Body);
        foreach (var references in counter.References.Values)
            if (references != 1)
                return false;
        return true;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        var visited = (InvocationExpression)base.VisitInvocation(node);
        if (visited.Expression is LambdaExpression lambdaExpression
            && EachParameterIsReferencedExactlyOnce(lambdaExpression)
            && LambdaInvocationRewriter.Apply(lambdaExpression, [.. visited.Arguments]) is { } reduced)
            return Visit(reduced)!;
        return visited;
    }
}
