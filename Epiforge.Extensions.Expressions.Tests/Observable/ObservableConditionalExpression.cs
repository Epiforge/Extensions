namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableConditionalExpression
{
    [TestMethod]
    public void FaultPropagationIfFalse()
    {
        var john = TestPerson.CreateJohn();
        john.Name = null;
        var emily = TestPerson.CreateEmily();
        emily.Name = null;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name != null ? p1.Name.Length : p2.Name!.Length, john, emily))
        {
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = "John";
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultPropagationIfTrue()
    {
        var john = TestPerson.CreateJohn();
        john.Name = null;
        var emily = TestPerson.CreateEmily();
        emily.Name = null;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p2.Name == null ? p1.Name!.Length : p2.Name.Length, john, emily))
        {
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
            emily.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = "John";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultPropagationTest()
    {
        var john = TestPerson.CreateJohn();
        john.Name = null;
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length > 0 ? p1.Name : p2.Name, john, emily))
        {
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = "John";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultShortCircuiting()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        using (var expr = observer.Observe<TestPerson, TestPerson, string>((p1, p2) => p1.Name!.Length > 0 ? p1.Name! : p2.Name!, john, null))
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            Assert.AreEqual(john.Name, expr.Evaluation.Result);
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var values = new BlockingCollection<string>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => string.IsNullOrEmpty(p1.Name) ? p2.Name : p1.Name, john, emily))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result!);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result!);
            john.Name = "J";
            john.Name = "John";
            john.Name = null;
            emily.Name = "E";
            emily.Name = "Emily";
            emily.Name = null;
            emily.Name = "Emily";
            john.Name = "John";
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new string[] { "John", "J", "John", "Emily", "E", "Emily", null!, "Emily", "John" }.SequenceEqual(values));
    }

    [TestMethod]
    public void ValueShortCircuiting()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length > 0 ? p1.Name : p2.Name, john, emily))
            Assert.AreEqual(john.Name, expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(0, emily.NameGets);
    }
}
