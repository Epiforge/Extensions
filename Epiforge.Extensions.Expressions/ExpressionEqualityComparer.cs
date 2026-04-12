namespace Epiforge.Extensions.Expressions;

/// <summary>
/// Defines methods to support the comparison of expression trees for equality
/// </summary>
public sealed class ExpressionEqualityComparer :
    IEqualityComparer<Expression>
{
    static readonly ConditionalWeakTable<Expression, IReadOnlyList<object?>> cachedDiagrams = [];
    static readonly ConditionalWeakTable<Expression, object> cachedHashCodes = [];

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
        cachedDiagrams.TryGetValue(x, out var cachedXDiagram);
        cachedDiagrams.TryGetValue(y, out var cachedYDiagram);
        if (cachedXDiagram is not null && cachedYDiagram is not null)
            return cachedXDiagram.SequenceEqual(cachedYDiagram);
        var xDiagram = cachedXDiagram is not null ? null : new List<object?>();
        var yDiagram = cachedYDiagram is not null ? null : new List<object?>();
        using var xDiagramEnumerator = cachedXDiagram is null ? ExpressionDiagramGenerator.GenerateDiagram(x).GetEnumerator() : cachedXDiagram.GetEnumerator();
        using var yDiagramEnumerator = cachedYDiagram is null ? ExpressionDiagramGenerator.GenerateDiagram(y).GetEnumerator() : cachedYDiagram.GetEnumerator();
        while (true)
        {
            var xDiagramEnumeratorMoved = xDiagramEnumerator.MoveNext();
            var yDiagramEnumeratorMoved = yDiagramEnumerator.MoveNext();
            if (xDiagramEnumeratorMoved != yDiagramEnumeratorMoved)
                return false;
            if (!xDiagramEnumeratorMoved)
            {
                if (xDiagram is not null)
                    cachedDiagrams.AddOrUpdate(x, xDiagram.AsReadOnly());
                if (yDiagram is not null)
                    cachedDiagrams.AddOrUpdate(y, yDiagram.AsReadOnly());
                return true;
            }
            if (!Equals(xDiagramEnumerator.Current, yDiagramEnumerator.Current))
                return false;
            xDiagram?.Add(xDiagramEnumerator.Current);
            yDiagram?.Add(yDiagramEnumerator.Current);
        }
    }

    /// <summary>
    /// Returns a hash code for the specified expression tree
    /// </summary>
    /// <param name="obj">The expression tree for which a hash code is to be returned</param>
    /// <returns>A hash code for the specified expression tree</returns>
    public int GetHashCode(Expression? obj)
    {
        if (obj is not null && cachedHashCodes.TryGetValue(obj, out var cachedHashCode))
            return (int)cachedHashCode;
        var hashCode = new System.HashCode();
        if (obj is null)
            return hashCode.ToHashCode();
        cachedDiagrams.TryGetValue(obj, out var cachedObjDiagram);
        var objDiagram = cachedObjDiagram is not null ? null : new List<object?>();
        foreach (var element in cachedObjDiagram ?? ExpressionDiagramGenerator.GenerateDiagram(obj))
        {
            hashCode.Add(element);
            objDiagram?.Add(element);
        }
        if (objDiagram is not null)
            cachedDiagrams.AddOrUpdate(obj, objDiagram.AsReadOnly());
        var result = hashCode.ToHashCode();
        cachedHashCodes.AddOrUpdate(obj, result);
        return result;
    }
}
