namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryLastOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(0, 10).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var lastOrDefaultQuery = sourceQuery.ObserveLastOrDefault())
            {
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(9);
                Assert.AreEqual(8, lastOrDefaultQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastOrDefaultQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastOrDefaultQuery.Evaluation.Result.Value);
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
            using (var lastOrDefaultQuery = sourceQuery.ObserveLastOrDefault((key, value) => value % 3 == 0))
            {
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(9, lastOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(9);
                Assert.AreEqual(6, lastOrDefaultQuery.Evaluation.Result.Value);
                source.RemoveAll((key, value) => value % 3 == 0);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, lastOrDefaultQuery.Evaluation.Result.Value);
                source.Add(30, 30);
                Assert.IsNull(lastOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(30, lastOrDefaultQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
