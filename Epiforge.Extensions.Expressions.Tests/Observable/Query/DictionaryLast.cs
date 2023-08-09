namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryLast
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var lastQuery = sourceQuery.ObserveLast())
            {
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastQuery.Evaluation.Result.Value);
                source.Remove(9);
                Assert.AreEqual(8, lastQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNotNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastQuery.Evaluation.Result.Value);
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
            using (var lastQuery = sourceQuery.ObserveLast((key, value) => value % 3 == 0))
            {
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastQuery.Evaluation.Result.Value);
                source.Remove(9);
                Assert.AreEqual(6, lastQuery.Evaluation.Result.Value);
                source.RemoveAll((key, value) => value % 3 == 0);
                Assert.IsNotNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(lastQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
