namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryAny
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var anyQuery = sourceQuery.ObserveAny())
            {
                Assert.IsNull(anyQuery.Evaluation.Fault);
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source[1] = 2;
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Remove(1);
                Assert.IsTrue(anyQuery.Evaluation.Result);
                --source[2];
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Clear();
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source.Add(1, 7);
                Assert.IsTrue(anyQuery.Evaluation.Result);
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
        var source = new ObservableDictionary<int, int>(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var anyQuery = sourceQuery.ObserveAny((key, value) => value % 3 != 0))
            {
                Assert.IsNull(anyQuery.Evaluation.Fault);
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source[1] = 2;
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Remove(1);
                Assert.IsFalse(anyQuery.Evaluation.Result);
                --source[2];
                Assert.IsTrue(anyQuery.Evaluation.Result);
                source.Clear();
                Assert.IsFalse(anyQuery.Evaluation.Result);
                source.Add(1, 7);
                Assert.IsTrue(anyQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
