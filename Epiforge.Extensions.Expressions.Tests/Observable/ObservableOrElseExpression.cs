namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableOrElseExpression
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FaultPropagation(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length > 0 || p2.Name!.Length > 0, john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = null;
            john.Name = string.Empty;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FaultShortCircuiting(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        TestPerson? noOne = null;
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => !string.IsNullOrEmpty(p1.Name) || !string.IsNullOrEmpty(p2!.Name), john, noOne))
        {
            Assert.IsTrue(expr.Evaluation.Result);
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PropertyChanges(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var values = new BlockingCollection<bool>();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length == 1 || p2.Name!.Length == 1, john, emily))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            john.Name = "J";
            john.Name = "John";
            emily.Name = "E";
            emily.Name = "Emily";
            john.Name = "J";
            emily.Name = "E";
            john.Name = "John";
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new bool[] { false, true, false, true, false, true }.SequenceEqual(values));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ValueShortCircuiting(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length > 1 || p2.Name!.Length > 3, john, emily))
            Assert.IsTrue(expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(0, emily.NameGets);
    }
}
