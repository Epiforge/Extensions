namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DictionaryIndexerValues
{
    sealed class TenTimesDictionary :
        ObservableDictionary<int, int>
    {
        public override int this[int key]
        {
            get => base[key] * 10;
            set => base[key] = value;
        }
    }

    [TestMethod]
    public void AnAddedKeyIsReportedAsTheIndexerReadsIt()
    {
        var dictionary = new TenTimesDictionary();
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { UseDirectSubscription = false });
        using (var expr = observer.Observe(p1 => p1[5], dictionary))
        {
            dictionary.Add(5, 4);
            Assert.AreEqual(dictionary[5], expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void AReplacedKeyIsReportedAsTheIndexerReadsIt()
    {
        var dictionary = new TenTimesDictionary();
        dictionary.Add(5, 4);
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { UseDirectSubscription = false });
        using (var expr = observer.Observe(p1 => p1[5], dictionary))
        {
            dictionary[5] = 7;
            Assert.AreEqual(dictionary[5], expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
