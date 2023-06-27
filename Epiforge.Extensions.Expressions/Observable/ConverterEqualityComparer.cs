namespace Epiforge.Extensions.Expressions.Observable;

class ConverterEqualityComparer :
    IEqualityComparer<LambdaExpression>
{
    public static ConverterEqualityComparer Default { get; } = new();

    public bool Equals(LambdaExpression? x, LambdaExpression? y) =>
        x?.Parameters[0].Type == y?.Parameters[0].Type && x?.Body.Type == y?.Body.Type;

    public int GetHashCode(LambdaExpression obj) =>
        Components.HashCode.Combine(obj.Parameters[0].Type, obj.Body.Type);
}
