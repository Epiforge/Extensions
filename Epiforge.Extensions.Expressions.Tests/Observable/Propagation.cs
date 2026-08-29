namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class Propagation
{
    [TestMethod]
    public void ANotificationHandlerMayStartAnotherPropagation()
    {
        var subject = new Numbered { Number = 5 };
        var values = new List<int>();
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(numbered => numbered.Number * 2 + (numbered.Number + 1), subject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                values.Add(expr.Evaluation.Result);
                if (subject.Number == 7)
                    subject.Number = 9;
            }
            expr.PropertyChanged += propertyChanged;
            Assert.AreEqual(16, expr.Evaluation.Result);
            subject.Number = 7;
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 22, 28 }.SequenceEqual(values));
    }

    [TestMethod]
    public void ChainNotifiesOncePerChange()
    {
        var subject = new Numbered { Number = 5 };
        var values = new List<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(numbered => numbered.Number * 2 + 1, subject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            Assert.AreEqual(11, expr.Evaluation.Result);
            subject.Number = 7;
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 15 }.SequenceEqual(values));
    }

    [TestMethod]
    public void DiamondNotifiesOncePerChange()
    {
        var subject = new Numbered { Number = 5 };
        var values = new List<int>();
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(numbered => numbered.Number * 2 + (numbered.Number + 1), subject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            Assert.AreEqual(16, expr.Evaluation.Result);
            subject.Number = 7;
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 22 }.SequenceEqual(values));
    }

    [TestMethod]
    public void DiamondNotifiesOncePerChangeAcrossManyChanges()
    {
        const int changes = 100000;
        var subject = new Numbered { Number = 0 };
        var notifications = 0;
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(numbered => numbered.Number * 2 + (numbered.Number + 1), subject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => ++notifications;
            expr.PropertyChanged += propertyChanged;
            for (var i = 1; i <= changes; ++i)
                subject.Number = i;
            expr.PropertyChanged -= propertyChanged;
            Assert.AreEqual(changes * 3 + 1, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(changes, notifications);
    }

    [TestMethod]
    public void DiamondRaisesChangingOncePerChange()
    {
        var subject = new Numbered { Number = 5 };
        var changings = 0;
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(numbered => numbered.Number * 2 + (numbered.Number + 1), subject))
        {
            void propertyChanging(object? sender, PropertyChangingEventArgs e) => ++changings;
            expr.PropertyChanging += propertyChanging;
            Assert.AreEqual(16, expr.Evaluation.Result);
            subject.Number = 7;
            expr.PropertyChanging -= propertyChanging;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(1, changings);
    }

    [TestMethod]
    public void OptimizedDiamondNotifiesOncePerChangeEvenWhenItsValueIsUnchanged()
    {
        var subject = new Numbered { Number = 3 };
        var values = new List<bool>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(numbered => numbered.Number > 5 == numbered.Number > 10, subject))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            Assert.IsTrue(expr.Evaluation.Result);
            subject.Number = 12;
            Assert.IsTrue(expr.Evaluation.Result);
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new bool[] { true }.SequenceEqual(values));
    }
}

public class Numbered :
    PropertyChangeNotifier
{
    int number;

    public int Number
    {
        get => number;
        set => SetBackedProperty(ref number, in value);
    }
}
