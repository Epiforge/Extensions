namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryFirstOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var firstOrDefaultQuery = sourceQuery.ObserveFirstOrDefault())
            {
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(0);
                Assert.AreEqual(1, firstOrDefaultQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstOrDefaultQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var firstOrDefaultQuery = sourceQuery.ObserveFirstOrDefault((key, value) => value % 3 == 0))
            {
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(0);
                Assert.AreEqual(3, firstOrDefaultQuery.Evaluation.Result.Value);
                source.RemoveAll((key, value) => value % 3 == 0);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstOrDefaultQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(firstOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstOrDefaultQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
