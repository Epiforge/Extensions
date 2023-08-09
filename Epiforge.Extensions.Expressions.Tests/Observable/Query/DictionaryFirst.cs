namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryFirst
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var firstQuery = sourceQuery.ObserveFirst())
            {
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result.Value);
                source.Remove(0);
                Assert.AreEqual(1, firstQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNotNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstQuery.Evaluation.Result.Value);
                source.Reset(new Dictionary<int, int> { { 15, 15 } });
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(15, firstQuery.Evaluation.Result.Value);
                source.Remove(15);
                Assert.IsNotNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result.Value);
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
            using (var firstQuery = sourceQuery.ObserveFirst((key, value) => value % 3 == 0))
            {
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result.Value);
                source.Remove(0);
                Assert.AreEqual(3, firstQuery.Evaluation.Result.Value);
                source.RemoveAll((key, value) => value % 3 == 0);
                Assert.IsNotNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(0, firstQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(firstQuery.Evaluation.Fault);
                Assert.AreEqual(30, firstQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
