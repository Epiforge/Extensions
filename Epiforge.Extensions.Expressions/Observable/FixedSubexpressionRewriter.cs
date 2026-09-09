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
    static readonly ConstructorInfo slotFaultConstructor = typeof(DirectSlotFault).GetConstructor([typeof(Exception)])!;
    static readonly MethodInfo unwrapMethod = typeof(DirectObservableExpression).GetMethod(nameof(DirectObservableExpression.Unwrap), BindingFlags.NonPublic | BindingFlags.Static)!;
    static readonly ConstantExpression unresolved = Expression.Constant(DirectObservableExpression.Unresolved, typeof(object));

    internal FixedSubexpressionRewriter(ParameterExpression values, ParameterExpression reached, ParameterExpression links, ParameterExpression held, IReadOnlyList<Expression> deferredGroups, IReadOnlyList<Expression> linkTargets, IReadOnlyList<(Expression Expression, bool Disposed)> heldSubexpressions)
    {
        this.deferredGroups = deferredGroups;
        this.held = held;
        this.heldSubexpressions = heldSubexpressions;
        this.links = links;
        this.linkTargets = linkTargets;
        this.reached = reached;
        this.values = values;
    }

    readonly IReadOnlyList<Expression> deferredGroups;
    readonly List<Expression> fixedSubexpressions = [];
    readonly ParameterExpression held;
    readonly IReadOnlyList<(Expression Expression, bool Disposed)> heldSubexpressions;
    readonly ParameterExpression links;
    readonly IReadOnlyList<Expression> linkTargets;
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

    int HeldOf(Expression node)
    {
        for (int i = 0, ii = heldSubexpressions.Count; i < ii; ++i)
            if (ExpressionEqualityComparer.Default.Equals(heldSubexpressions[i].Expression, node))
                return i;
        return -1;
    }

    /// <summary>
    /// Yields the value of a subexpression the graph produces once and then holds, resolving it into a slot the first time an evaluation reads it and reading that slot on every evaluation after
    /// </summary>
    /// <remarks>
    /// Resolution waits for the first read rather than happening when the observation is constructed, so that a held subexpression sitting in a branch which is not taken is not evaluated at all, which is where the graph's node for it would first be evaluated. A fault is put in the slot and thrown again on every later read, so that what is behind the slot happens exactly once whether it returned or threw, which is also what the graph's node does with a fault it has no reason to re-evaluate
    /// </remarks>
    Expression Hold(int slot, Expression resolved, Type type)
    {
        var index = Expression.Constant(slot);
        var fault = Expression.Parameter(typeof(Exception), "fault");
        var resolving = Expression.TryCatch
        (
            Expression.Assign(Expression.ArrayAccess(held, index), Expression.Convert(resolved, typeof(object))),
            Expression.Catch(fault, Expression.Block(typeof(object), Expression.Assign(Expression.ArrayAccess(held, index), Expression.New(slotFaultConstructor, fault)), Expression.Rethrow(typeof(object))))
        );
        return Expression.Convert(Expression.Call(unwrapMethod, Expression.Condition(Expression.ReferenceEqual(Expression.ArrayIndex(held, index), unresolved), resolving, Expression.ArrayIndex(held, index), typeof(object))), type);
    }

    int LinkOf(Expression node)
    {
        for (int i = 0, ii = linkTargets.Count; i < ii; ++i)
            if (ReferenceEquals(linkTargets[i], node))
                return i;
        return -1;
    }

    /// <summary>
    /// Yields the value of a target the observation must follow, recording it in an array the observation reads once the evaluation has returned, which is how the fast path learns which object a chain now reaches
    /// </summary>
    BlockExpression Record(int link, Expression target)
    {
        var held = Expression.Variable(target.Type);
        return Expression.Block(target.Type, [held], Expression.Assign(held, target), Expression.Assign(Expression.ArrayAccess(links, Expression.Constant(link)), Expression.Convert(held, typeof(object))), held);
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
        if (node is null)
            return null;
        if (!ReferenceEquals(node, wrapping) && GroupOf(node) is var group && group >= 0)
        {
            var enclosing = wrapping;
            wrapping = node;
            var operand = Visit(node)!;
            wrapping = enclosing;
            return Expression.Block(operand.Type, Expression.Assign(Expression.ArrayAccess(reached, Expression.Constant(group)), Expression.Constant(true)), operand);
        }
        var visited = node switch
        {
            MemberExpression memberExpression when DirectSubscriptionAnalyzer.IsFixed(memberExpression) => Substitute(memberExpression),
            UnaryExpression { NodeType: ExpressionType.Quote } => node,
            _ => base.Visit(node)!
        };
        if (HeldOf(node) is var slot && slot >= 0)
            visited = Hold(slot, visited, node.Type);
        var link = LinkOf(node);
        return link < 0 ? visited : Record(link, visited);
    }
}
