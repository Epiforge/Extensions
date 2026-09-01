namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableAndAlsoExpression
{
    [TestMethod]
    public void ACountGuardHoldsWhenTheCollectionShrinksInOneEvent()
    {
        var numbers = new ObservableRangeCollection<int>(new[] { 0, 1, 2, 3 });
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => numbers.Count == 4 && numbers[2] == 2))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual(true, expr.Evaluation.Result);
            numbers.RemoveRange(2, 2);
            Assert.IsNull(expr.Evaluation.Fault, $"the guard was defeated: {expr.Evaluation.Fault}");
            Assert.AreEqual(false, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ACountGuardHoldsWhenTheCollectionShrinksOneElementAtATime()
    {
        var numbers = new ObservableRangeCollection<int>(new[] { 0, 1, 2, 3 });
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => numbers.Count == 4 && numbers[2] == 2))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual(true, expr.Evaluation.Result);
            numbers.RemoveAt(3);
            Assert.IsNull(expr.Evaluation.Fault, $"the guard was defeated removing the last element: {expr.Evaluation.Fault}");
            numbers.RemoveAt(2);
            Assert.IsNull(expr.Evaluation.Fault, $"the guard was defeated removing the indexed element: {expr.Evaluation.Fault}");
            Assert.AreEqual(false, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length > 0 && p2.Name!.Length > 0, john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = null;
            john.Name = "John";
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultShortCircuiting()
    {
        var john = TestPerson.CreateJohn();
        TestPerson? noOne = null;
        var observer = ExpressionObserverHelpers.Create();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        using (var expr = observer.Observe((p1, p2) => string.IsNullOrEmpty(p1.Name) && string.IsNullOrEmpty(p2.Name), john, noOne))
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        {
            Assert.IsFalse(expr.Evaluation.Result);
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var values = new BlockingCollection<bool>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length == 1 && p2.Name!.Length == 1, john, emily))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            john.Name = "J";
            emily.Name = "E";
            john.Name = "John";
            john.Name = "J";
            emily.Name = "Emily";
            emily.Name = "E";
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new bool[] { false, true, false, true, false, true }.SequenceEqual(values));
    }

    [TestMethod]
    public void ValueShortCircuiting()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length == 1 && p2.Name!.Length > 3, john, emily))
            Assert.IsFalse(expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(0, emily.NameGets);
    }
}
