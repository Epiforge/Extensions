namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableConstantExpression
{
    [TestMethod]
    public void ValueCollectionChanged()
    {
        var collection = new ObservableCollection<string>();
        var notifications = 0;
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(c => c, collection))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => ++notifications;
            expr.PropertyChanged += propertyChanged;
            collection.Add("a");
            collection.Add("b");
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.AreEqual(2, notifications);
    }
}
