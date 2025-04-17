namespace Epiforge.Extensions.Expressions;

/// <summary>
/// Defines methods to support the comparison of expression trees for equality
/// </summary>
public sealed class ExpressionEqualityComparer :
    IEqualityComparer<Expression>
{
    /// <summary>
    /// Gets the default instance of <see cref="ExpressionEqualityComparer"/>
    /// </summary>
    public static ExpressionEqualityComparer Default { get; } = new ExpressionEqualityComparer();

    /// <summary>
    /// Determines whether the specified expression trees are equal
    /// </summary>
    /// <param name="x">The first <see cref="Expression"/> to compare</param>
    /// <param name="y">The second <see cref="Expression"/> to compare</param>
    /// <returns><c>true</c> if the specified objects are equal; otherwise, <c>false</c></returns>
    public bool Equals(Expression? x, Expression? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        using var xDiagramEnumerator = ExpressionDiagramGenerator.GenerateDiagram(x).GetEnumerator();
        using var yDiagramEnumerator = ExpressionDiagramGenerator.GenerateDiagram(y).GetEnumerator();
        while (true)
        {
            var xDiagramEnumeratorMoved = xDiagramEnumerator.MoveNext();
            var yDiagramEnumeratorMoved = yDiagramEnumerator.MoveNext();
            if (xDiagramEnumeratorMoved != yDiagramEnumeratorMoved)
                return false;
            if (!xDiagramEnumeratorMoved)
                return true;
            if (!Equals(xDiagramEnumerator.Current, yDiagramEnumerator.Current))
                return false;
        }
    }

    /// <summary>
    /// Returns a hash code for the specified expression tree
    /// </summary>
    /// <param name="obj">The expression tree for which a hash code is to be returned</param>
    /// <returns>A hash code for the specified expression tree</returns>
    public int GetHashCode(Expression? obj)
    {
        var hashCode = new System.HashCode();
        if (obj is not null)
            foreach (var element in ExpressionDiagramGenerator.GenerateDiagram(obj))
                hashCode.Add(element);
        return hashCode.ToHashCode();
    }
}
