namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionarySum
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var sumQuery = sourceQuery.ObserveSum())
            {
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(0, sumQuery.Evaluation.Result);
                source.Add(1, 1);
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(1, sumQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3).ToDictionary(i => i));
                Assert.AreEqual(10, sumQuery.Evaluation.Result);
                source.RemoveRange(new int[] { 1, 2 });
                Assert.AreEqual(7, sumQuery.Evaluation.Result);
                source.Remove(3);
                Assert.AreEqual(4, sumQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(1, 3).ToDictionary(i => i));
                Assert.AreEqual(6, sumQuery.Evaluation.Result);
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
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var sumQuery = sourceQuery.ObserveSum((key, value) => value.Name!.Length))
            {
                Assert.IsNull(sumQuery.Evaluation.Fault);
                Assert.AreEqual(74, sumQuery.Evaluation.Result);
                source.Add(source.Count, source[0]);
                Assert.AreEqual(78, sumQuery.Evaluation.Result);
                source[0].Name = "Johnny";
                Assert.AreEqual(82, sumQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
