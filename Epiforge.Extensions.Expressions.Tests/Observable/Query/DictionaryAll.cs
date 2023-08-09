namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryAll
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, int>(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * 3));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var allQuery = sourceQuery.ObserveAll((key, value) => value % 3 == 0))
            {
                Assert.IsNull(allQuery.Evaluation.Fault);
                Assert.IsTrue(allQuery.Evaluation.Result);
                source[1] = 2;
                Assert.IsFalse(allQuery.Evaluation.Result);
                source.Remove(1);
                Assert.IsTrue(allQuery.Evaluation.Result);
                --source[2];
                Assert.IsFalse(allQuery.Evaluation.Result);
                source.Clear();
                Assert.IsTrue(allQuery.Evaluation.Result);
                source.Add(11, 7);
                Assert.IsFalse(allQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
