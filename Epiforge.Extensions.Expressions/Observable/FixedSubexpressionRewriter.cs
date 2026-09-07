namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Replaces every closure field chain in a lambda with a read from an array of values resolved when an observation is constructed, so that a fast path evaluates the same frozen inputs the graph caches in its nodes rather than dereferencing the closure afresh every time
/// </summary>
/// <remarks>
/// Each operand whose evaluation the expression defers is also wrapped so that reaching it records the fact in an array the observation reads once the evaluation has returned, which is how the fast path learns that the subscriptions of that operand are now the graph's as well
/// </remarks>
sealed class FixedSubexpressionRewriter :
    ExpressionVisitor
{
    internal FixedSubexpressionRewriter(ParameterExpression values, ParameterExpression reached, IReadOnlyList<Expression> deferredGroups)
    {
        this.deferredGroups = deferredGroups;
        this.reached = reached;
        this.values = values;
    }

    readonly IReadOnlyList<Expression> deferredGroups;
    readonly List<Expression> fixedSubexpressions = [];
    readonly ParameterExpression reached;
    readonly ParameterExpression values;
    Expression? wrapping;

    internal List<Expression> FixedSubexpressions =>
        fixedSubexpressions;

    int GroupOf(Expression node)
    {
        for (int i = 0, ii = deferredGroups.Count; i < ii; ++i)
            if (ReferenceEquals(deferredGroups[i], node))
                return i;
        return -1;
    }

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

    public override Expression? Visit(Expression? node)
    {
        if (node is not null && !ReferenceEquals(node, wrapping) && GroupOf(node) is var group && group >= 0)
        {
            var enclosing = wrapping;
            wrapping = node;
            var operand = Visit(node)!;
            wrapping = enclosing;
            return Expression.Block(operand.Type, Expression.Assign(Expression.ArrayAccess(reached, Expression.Constant(group)), Expression.Constant(true)), operand);
        }
        return node switch
        {
            MemberExpression memberExpression when DirectSubscriptionAnalyzer.IsFixed(memberExpression) => Substitute(memberExpression),
            UnaryExpression { NodeType: ExpressionType.Quote } => node,
            _ => base.Visit(node)
        };
    }
}
