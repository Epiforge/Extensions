namespace Epiforge.Extensions.Expressions.Tests;

[TestClass]
public class ExpressionEqualityComparer
{
    readonly Expressions.ExpressionEqualityComparer comparer = Expressions.ExpressionEqualityComparer.Default;

    [TestMethod]
    public void BlockWithVariablesAreEqual()
    {
        var expressionX = Expression.Block(new[] { Expression.Variable(typeof(int)) }, Expression.Constant(1));
        var expressionY = Expression.Block(new[] { Expression.Variable(typeof(int)) }, Expression.Constant(1));
        Assert.IsTrue(comparer.Equals(expressionX, expressionY));
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
