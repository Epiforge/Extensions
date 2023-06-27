namespace Epiforge.Extensions.Expressions.Observable;

class ObservableExpressionEqualityComparer :
    IEqualityComparer<ObservableExpression>
{
    public static ObservableExpressionEqualityComparer Default { get; } = new();

    public bool Equals(ObservableExpression? x, ObservableExpression? y) =>
        ExpressionEqualityComparer.Default.Equals(x?.Expression, y?.Expression);

    public int GetHashCode(ObservableExpression obj) =>
        ExpressionEqualityComparer.Default.GetHashCode(obj.Expression);
}
