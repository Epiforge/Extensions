namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Supplies the only two boxes a boolean result ever needs, since nothing which consumes a result distinguishes boxes of equal value
/// </summary>
static class BooleanBoxes
{
    internal static readonly object False = false;
    internal static readonly object True = true;

    static readonly Expression FalseExpression = Expression.Field(null, typeof(BooleanBoxes), nameof(False));
    static readonly Expression TrueExpression = Expression.Field(null, typeof(BooleanBoxes), nameof(True));

    internal static object Box(bool value) =>
        value ? True : False;

    /// <summary>
    /// Yields an expression producing the operation's value as a reference, which for a boolean is one of the two shared boxes rather than a fresh allocation on every evaluation
    /// </summary>
    internal static Expression Convert(Expression operation) =>
        operation.Type == typeof(bool) ? Expression.Condition(operation, TrueExpression, FalseExpression) : Expression.Convert(operation, typeof(object));
}
