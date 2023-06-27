namespace Epiforge.Extensions.Expressions.Observable;

class ConstantExpressionExpressionEqualityComparer :
    IEqualityComparer<ConstantExpression>
{
    public static ConstantExpressionExpressionEqualityComparer Default { get; } = new();

    public bool Equals(ConstantExpression? x, ConstantExpression? y) =>
        ExpressionEqualityComparer.Default.Equals(x?.Value as Expression, y?.Value as Expression);

    public int GetHashCode(ConstantExpression obj) =>
        obj.Value is Expression expression ? ExpressionEqualityComparer.Default.GetHashCode(expression) : 0;
}
