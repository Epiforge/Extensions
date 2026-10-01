namespace Epiforge.Extensions.Expressions;

static class ExpressionKeyStability
{
    sealed class ConstantVisitor :
        ExpressionVisitor
    {
        internal bool Stable = true;

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (Stable && node.Value is { } value && !typesWithValueEquality.GetOrAdd(value.GetType(), HasValueEquality))
                Stable = false;
            return node;
        }
    }

    static readonly ConcurrentDictionary<Type, bool> typesWithValueEquality = new();

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Were an override of Equals or GetHashCode trimmed away, the type would be treated as lacking value equality, which only keeps it out of the cache compared by structure")]
    static bool HasValueEquality(Type type) =>
        type.IsValueType
        || type.GetMethod(nameof(Equals), [typeof(object)])?.DeclaringType != typeof(object)
        && type.GetMethod(nameof(GetHashCode), Type.EmptyTypes)?.DeclaringType != typeof(object);

    internal static bool IsStable(Expression expression)
    {
        var visitor = new ConstantVisitor();
        visitor.Visit(expression);
        return visitor.Stable;
    }
}
