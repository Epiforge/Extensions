namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Replaces a lambda's parameters with the expressions an invocation would have supplied for them, yielding the body it would have evaluated, so that an observation carries the caller's expression itself rather than an invocation node which rebuilds everything beneath it whenever an argument changes and which the analyzer cannot see through
/// </summary>
/// <remarks>
/// An argument reached by more than one parameter reference is evaluated once per reference rather than once, so this is equivalent only for argument expressions without side effects, which is what a parameter or a read of one yields
/// </remarks>
sealed class LambdaInvocationRewriter :
    ExpressionVisitor
{
    LambdaInvocationRewriter(IReadOnlyDictionary<ParameterExpression, Expression> substitutions) =>
        this.substitutions = substitutions;

    bool declined;
    readonly IReadOnlyDictionary<ParameterExpression, Expression> substitutions;

    /// <summary>
    /// Yields the body the specified lambda expression would have evaluated for the specified arguments, or <see langword="null"/> where the body quotes an expression, since substituting within a quoted tree changes what the caller reads back from it
    /// </summary>
    internal static Expression? Apply(LambdaExpression lambdaExpression, params Expression[] arguments)
    {
        var parameters = lambdaExpression.Parameters;
        if (parameters.Count != arguments.Length)
            throw new ArgumentException("the number of arguments does not match the number of the lambda expression's parameters", nameof(arguments));
        var substitutions = new Dictionary<ParameterExpression, Expression>(parameters.Count);
        for (var i = 0; i < parameters.Count; ++i)
            substitutions.Add(parameters[i], arguments[i]);
        var rewriter = new LambdaInvocationRewriter(substitutions);
        var body = rewriter.Visit(lambdaExpression.Body);
        return rewriter.declined ? null : body;
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is UnaryExpression { NodeType: ExpressionType.Quote })
        {
            declined = true;
            return node;
        }
        return node is ParameterExpression parameterExpression && substitutions.TryGetValue(parameterExpression, out var substitution) ? substitution : base.Visit(node);
    }
}
