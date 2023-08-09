namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionarySingleOrDefault
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(1, 1).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var singleOrDefaultQuery = sourceQuery.ObserveSingleOrDefault())
            {
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(1, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Add(2, 2);
                Assert.IsNotNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(1);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(2, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result.Value);
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
        var source = new ObservableDictionary<int, int>(Enumerable.Range(1, 3).ToDictionary(i => i));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var singleOrDefaultQuery = sourceQuery.ObserveSingleOrDefault((key, value) => value % 3 == 0))
            {
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Remove(3);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Add(3, 3);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Add(5, 5);
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(3, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Add(6, 6);
                Assert.IsNotNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result.Value);
                source.Clear();
                Assert.IsNull(singleOrDefaultQuery.Evaluation.Fault);
                Assert.AreEqual(0, singleOrDefaultQuery.Evaluation.Result.Value);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
