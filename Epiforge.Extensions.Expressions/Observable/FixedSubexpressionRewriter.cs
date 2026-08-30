namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Replaces every closure field chain in a lambda with a read from an array of values resolved when an observation is constructed, so that a fast path evaluates the same frozen inputs the graph caches in its nodes rather than dereferencing the closure afresh every time
/// </summary>
sealed class FixedSubexpressionRewriter :
    ExpressionVisitor
{
    internal FixedSubexpressionRewriter(ParameterExpression values) =>
        this.values = values;

    readonly List<Expression> fixedSubexpressions = [];
    readonly ParameterExpression values;

    internal IReadOnlyList<Expression> FixedSubexpressions =>
        fixedSubexpressions;

    public override Expression? Visit(Expression? node) =>
        node switch
        {
            MemberExpression memberExpression when DirectSubscriptionAnalyzer.IsFixed(memberExpression) => Substitute(memberExpression),
            UnaryExpression { NodeType: ExpressionType.Quote } => node,
            _ => base.Visit(node)
        };

    UnaryExpression Substitute(MemberExpression memberExpression)
    {
        var index = fixedSubexpressions.Count;
        for (var i = 0; i < index; ++i)
            if (ReferenceEquals(fixedSubexpressions[i], memberExpression))
            {
                index = i;
                break;
            }
        if (index == fixedSubexpressions.Count)
            fixedSubexpressions.Add(memberExpression);
        return Expression.Convert(Expression.ArrayIndex(values, Expression.Constant(index)), memberExpression.Type);
    }
}
