namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DictionaryIndexerFaults
{
    static IReadOnlyList<string> MessagesWhenTheKeyIsRemoved(bool useDirectSubscription)
    {
        var perfectNumbers = new ObservableDictionary<int, int>(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * i));
        var messages = new List<string>();
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
        using (var expr = observer.Observe(p1 => p1[5], perfectNumbers))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    messages.Add(expr.Evaluation.Fault?.Message ?? "no fault");
            }

            expr.PropertyChanged += propertyChanged;
            perfectNumbers.Remove(5);
            expr.PropertyChanged -= propertyChanged;
        }
        return messages;
    }

    [TestMethod]
    public void ARemovedKeyIsReportedTheSameWayByBothMechanisms() =>
        Assert.AreEqual(string.Join(" | ", MessagesWhenTheKeyIsRemoved(false)), string.Join(" | ", MessagesWhenTheKeyIsRemoved(true)));
}
