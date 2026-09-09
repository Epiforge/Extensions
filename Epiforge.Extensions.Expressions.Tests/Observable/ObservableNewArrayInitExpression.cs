namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableNewArrayInitExpression
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InitializerFaultPropagation(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe(() => string.Concat(new string?[] { john.Name!.Length.ToString(), emily.Name!.Length.ToString() })))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = "John";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
