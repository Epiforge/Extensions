namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableInvocationExpression
{
    #region TestMethod Methods

    public TestPerson CombinePeople(TestPerson a, TestPerson b) =>
        new() { Name = $"{a.Name} {b.Name}" };

    #endregion TestMethod Methods

    [TestMethod]
    public void ArgumentChangePropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var testPersonNamePropertyInfo = typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<int>(Expression.Lambda(Expression.Invoke((Expression<Func<string, string, int>>)((p1, p2) => p1.Length + p2.Length), Expression.MakeMemberAccess(firstParameter, testPersonNamePropertyInfo), Expression.MakeMemberAccess(secondParameter, testPersonNamePropertyInfo)), firstParameter, secondParameter), john, emily))
        {
            Assert.AreEqual(9, expr.Evaluation.Result);
            emily.Name = "Arya";
            Assert.AreEqual(8, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ArgumentFaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var testPersonNamePropertyInfo = typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!;
        var stringLengthPropertyInfo = typeof(string).GetProperty(nameof(string.Length))!;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<int>(Expression.Lambda(Expression.Invoke((Expression<Func<int, int, int>>)((p1, p2) => p1 + p2), Expression.MakeMemberAccess(Expression.MakeMemberAccess(firstParameter, testPersonNamePropertyInfo), stringLengthPropertyInfo), Expression.MakeMemberAccess(Expression.MakeMemberAccess(secondParameter, testPersonNamePropertyInfo), stringLengthPropertyInfo)), firstParameter, secondParameter), john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            emily.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ExpressionFaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var testPersonNamePropertyInfo = typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<int>(Expression.Lambda(Expression.Invoke((Expression<Func<string, string, int>>)((p1, p2) => p1.Length + p2.Length), Expression.MakeMemberAccess(firstParameter, testPersonNamePropertyInfo), Expression.MakeMemberAccess(secondParameter, testPersonNamePropertyInfo)), firstParameter, secondParameter), john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            emily.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void LambdaDelegateValue()
    {
        Func<TestPerson, TestPerson, TestPerson> @delegate = (p1, p2) =>
            CombinePeople(p1, p2);
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<TestPerson>(Expression.Lambda(Expression.Invoke(Expression.Constant(@delegate), firstParameter, secondParameter), firstParameter, secondParameter), john, emily))
            Assert.AreEqual("John Emily", expr.Evaluation.Result!.Name);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void LambdaValue()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<TestPerson>(Expression.Lambda(Expression.Invoke((Expression<Func<TestPerson, TestPerson, TestPerson>>)((p1, p2) => CombinePeople(p1, p2)), firstParameter, secondParameter), firstParameter, secondParameter), john, emily))
            Assert.AreEqual("John Emily", expr.Evaluation.Result!.Name);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void LocalMethodDelegateValue()
    {
        TestPerson localMethod(TestPerson p1, TestPerson p2) =>
            CombinePeople(p1, p2);
        Func<TestPerson, TestPerson, TestPerson> @delegate = localMethod;
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<TestPerson>(Expression.Lambda(Expression.Invoke(Expression.Constant(@delegate), firstParameter, secondParameter), firstParameter, secondParameter), john, emily))
            Assert.AreEqual("John Emily", expr.Evaluation.Result!.Name);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void MethodDelegateValue()
    {
        Func<TestPerson, TestPerson, TestPerson> @delegate = CombinePeople;
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var firstParameter = Expression.Parameter(typeof(TestPerson));
        var secondParameter = Expression.Parameter(typeof(TestPerson));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<TestPerson>(Expression.Lambda(Expression.Invoke(Expression.Constant(@delegate), firstParameter, secondParameter), firstParameter, secondParameter), john, emily))
            Assert.AreEqual("John Emily", expr.Evaluation.Result!.Name);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
