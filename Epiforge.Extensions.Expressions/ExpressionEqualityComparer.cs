namespace Epiforge.Extensions.Expressions;

/// <summary>
/// Defines methods to support the comparison of expression trees for equality
/// </summary>
public sealed class ExpressionEqualityComparer :
    IEqualityComparer<Expression>
{
    sealed class Diagram
    {
        internal Diagram(IReadOnlyList<object?> elements)
        {
            Elements = elements;
            var hashCode = new System.HashCode();
            for (int i = 0, ii = elements.Count; i < ii; ++i)
                hashCode.Add(elements[i]);
            HashCode = hashCode.ToHashCode();
        }

        internal readonly IReadOnlyList<object?> Elements;
        internal readonly int HashCode;
    }

    static readonly ConditionalWeakTable<Expression, Diagram> cachedDiagrams = [];

    static Diagram GetDiagram(Expression expression)
    {
        if (cachedDiagrams.TryGetValue(expression, out var cachedDiagram))
            return cachedDiagram;
        var diagram = new Diagram(ExpressionDiagramGenerator.GenerateDiagram(expression));
        cachedDiagrams.AddOrUpdate(expression, diagram);
        return diagram;
    }

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
        if (x is null || y is null)
            return false;
        var xDiagram = GetDiagram(x);
        var yDiagram = GetDiagram(y);
        if (xDiagram.HashCode != yDiagram.HashCode)
            return false;
        var xElements = xDiagram.Elements;
        var yElements = yDiagram.Elements;
        if (xElements.Count != yElements.Count)
            return false;
        for (int i = 0, ii = xElements.Count; i < ii; ++i)
            if (!Equals(xElements[i], yElements[i]))
                return false;
        return true;
    }

    /// <summary>
    /// Returns a hash code for the specified expression tree
    /// </summary>
    /// <param name="obj">The expression tree for which a hash code is to be returned</param>
    /// <returns>A hash code for the specified expression tree</returns>
    public int GetHashCode(Expression? obj) =>
        obj is null ? 0 : GetDiagram(obj).HashCode;
}
