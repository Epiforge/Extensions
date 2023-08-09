namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryAverage
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<string, decimal>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var averageQuery = sourceQuery.ObserveAverage())
            {
                Assert.IsNotNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(0, averageQuery.Evaluation.Result);
                source.Add("1", 1m);
                Assert.IsNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(1m, averageQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3).ToDictionary(i => i.ToString(), Convert.ToDecimal));
                Assert.AreEqual(2.5m, averageQuery.Evaluation.Result);
                source.RemoveRange(new string[] { "1", "2" });
                Assert.AreEqual(3.5m, averageQuery.Evaluation.Result);
                source.Remove("3");
                Assert.AreEqual(4m, averageQuery.Evaluation.Result);
                source.Remove("4");
                Assert.IsNotNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(0m, averageQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(2, 3).ToDictionary(i => i.ToString(), Convert.ToDecimal));
                Assert.IsNull(averageQuery.Evaluation.Fault);
                Assert.AreEqual(3m, averageQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
