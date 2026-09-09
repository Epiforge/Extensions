namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableCoalesceExpression
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FaultPropagation(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name!.ToString() ?? p2.Name!.ToString(), john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = null;
            john.Name = "John";
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
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        using (var expr = observer.Observe<TestPerson, TestPerson, string>((p1, p2) => p1.Name ?? p2.Name!, john, null))
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            Assert.AreEqual(john.Name, expr.Evaluation.Result);
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    #region Implicit Conversion TestMethod Classes

    public class A
    {
        [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
        public static implicit operator B?(A a) => null;

        [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
        public static implicit operator C?(A a) => throw new Exception();
    }

    public class B
    {
    }

    public class C
    {
    }

    #endregion Implicit Conversion TestMethod Classes

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ImplicitConversion(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe(() => new A() ?? new B()))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsNull(expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ImplicitConversionFailure(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe(() => new A() ?? new C()))
            Assert.IsNotNull(expr.Evaluation.Fault);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PropertyChanges(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var values = new BlockingCollection<string>();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name ?? p2.Name, john, emily))
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
    [DataRow(false)]
    [DataRow(true)]
    public void ValueShortCircuiting(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe((p1, p2) => p1.Name ?? p2.Name, john, emily))
            Assert.AreEqual(john.Name, expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(0, emily.NameGets);
    }
}
