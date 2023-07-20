namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableTypeBinaryExpression
{
    #region Helper Class

    public class SomeObject :
        PropertyChangeNotifier
    {
        object? field;

        public object? Property
        {
            get => field;
            set => SetBackedProperty(ref field, in value);
        }
    }

    #endregion Helper Class

    [TestMethod]
    public void FaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
#pragma warning disable CS0183, CS8602, IDE0150 // 'is' expression's given expression is always of the provided type, Dereference of a possibly null reference, Prefer 'null' check over type check.
        using (var expr = observer.Observe(p1 => p1.Name!.Length is int, john))
#pragma warning restore CS0183, CS8602, IDE0150 // 'is' expression's given expression is always of the provided type, Dereference of a possibly null reference, Prefer 'null' check over type check.
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = string.Empty;
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var john = TestPerson.CreateJohn();
        var someObject = new SomeObject();
        var values = new BlockingCollection<bool>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1.Property is TestPerson, someObject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            someObject.Property = john;
            someObject.Property = "John";
            someObject.Property = john;
            someObject.Property = null;
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new bool[] { false, true, false, true, false }.SequenceEqual(values));
    }
}
