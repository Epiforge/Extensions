namespace Epiforge.Extensions.Expressions.Tests;

[TestClass]
public class ExpressionEqualityComparer
{
    sealed class ClosureReadCollector(List<MemberExpression> found) :
        ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression)
                found.Add(node);
            return base.VisitMember(node);
        }
    }

    readonly Expressions.ExpressionEqualityComparer comparer = Expressions.ExpressionEqualityComparer.Default;

    [TestMethod]
    public void BlockWithVariablesAreEqual()
    {
        var expressionX = Expression.Block(new[] { Expression.Variable(typeof(int)) }, Expression.Constant(1));
        var expressionY = Expression.Block(new[] { Expression.Variable(typeof(int)) }, Expression.Constant(1));
        Assert.IsTrue(comparer.Equals(expressionX, expressionY));
    }

    static Expression<Func<int>> CaptureAVersion()
    {
        var captured = new Version(1, 0);
        return () => captured.Major;
    }

    static List<MemberExpression> ClosureReads(Expression expression)
    {
        var found = new List<MemberExpression>();
        new ClosureReadCollector(found).Visit(expression);
        return found;
    }

    [TestMethod]
    public void ClosureReadsOfOneVariableAreTwoNodesWhichCompareEqual()
    {
        var captured = new Version(1, 0);
        Expression<Func<bool>> lambda = () => captured.Major > 0 && captured.Minor > 0;
        var reads = ClosureReads(lambda);
        Assert.AreEqual(2, reads.Count);
        Assert.IsFalse(ReferenceEquals(reads[0], reads[1]));
        Assert.IsTrue(comparer.Equals(reads[0], reads[1]));
        Assert.AreEqual(comparer.GetHashCode(reads[0]), comparer.GetHashCode(reads[1]));
    }

    [TestMethod]
    public void ClosureReadsOfSeparateVariablesOfTheSameTypeAreUnequal()
    {
        var readX = ClosureReads(CaptureAVersion())[0];
        var readY = ClosureReads(CaptureAVersion())[0];
        Assert.IsFalse(ReferenceEquals(readX, readY));
        Assert.AreEqual(readX.Member, readY.Member);
        Assert.IsFalse(comparer.Equals(readX, readY));
    }

    [TestMethod]
    public void DebugInfoAreEqual()
    {
        var expressionX = Expression.DebugInfo(Expression.SymbolDocument("foo"), 1, 2, 3, 4);
        var expressionY = Expression.DebugInfo(Expression.SymbolDocument("foo"), 1, 2, 3, 4);
        Assert.IsTrue(comparer.Equals(expressionX, expressionY));
    }

    [TestMethod]
    public void DifferentConstantsAreUnequal()
    {
        var expressionX = Expression.Constant(1);
        var expressionY = Expression.Constant(2);
        Assert.IsFalse(comparer.Equals(expressionX, expressionY));
    }

    [TestMethod]
    public void NewWithMembersAreEqual()
    {
        var expressionX = Expression.New(typeof(Version).GetConstructor(new[] { typeof(int), typeof(int) })!, new[] { Expression.Constant(1), Expression.Constant(2) }, typeof(Version).GetProperty(nameof(Version.Major))!, typeof(Version).GetProperty(nameof(Version.Minor))!);
        var expressionY = Expression.New(typeof(Version).GetConstructor(new[] { typeof(int), typeof(int) })!, new[] { Expression.Constant(1), Expression.Constant(2) }, typeof(Version).GetProperty(nameof(Version.Major))!, typeof(Version).GetProperty(nameof(Version.Minor))!);
        Assert.IsTrue(comparer.Equals(expressionX, expressionY));
    }

    [TestMethod]
    public void NewWithInconsistentMembersAreUnequal()
    {
        var expressionX = Expression.New(typeof(Version).GetConstructor(new[] { typeof(int), typeof(int) })!, new[] { Expression.Constant(1), Expression.Constant(2) }, typeof(Version).GetProperty(nameof(Version.Major))!, typeof(Version).GetProperty(nameof(Version.Minor))!);
        var expressionY = Expression.New(typeof(Version).GetConstructor(new[] { typeof(int), typeof(int) })!, new[] { Expression.Constant(1), Expression.Constant(2) });
        Assert.IsFalse(comparer.Equals(expressionX, expressionY));
    }

    [TestMethod]
    public void RuntimeVariablesAreEqual()
    {
        var expressionX = Expression.RuntimeVariables(Expression.Variable(typeof(int)));
        var expressionY = Expression.RuntimeVariables(Expression.Variable(typeof(int)));
        Assert.IsTrue(comparer.Equals(expressionX, expressionY));
    }

    [TestMethod]
    public void SameAreEqual()
    {
        var expression = Expression.Constant(1);
        Assert.IsTrue(comparer.Equals(expression, expression));
    }
}
