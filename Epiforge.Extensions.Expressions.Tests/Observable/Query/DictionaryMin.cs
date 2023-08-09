namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryMin
{
    [TestMethod]
    public void ExpressionlessSourceManipulation()
    {
        var source = new ObservableDictionary<int, int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var minQuery = sourceQuery.ObserveMin())
            {
                Assert.IsNotNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(0, minQuery.Evaluation.Result);
                source.Add(1, 1);
                Assert.IsNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source.AddRange(Enumerable.Range(2, 3).Select(i => new KeyValuePair<int, int>(i, i)));
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source.RemoveRange(new int[] { 1, 2 });
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source.Remove(3);
                Assert.AreEqual(4, minQuery.Evaluation.Result);
                source.Reset(Enumerable.Range(1, 3).ToDictionary(i => i));
                Assert.AreEqual(1, minQuery.Evaluation.Result);
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
        var source = new ObservableDictionary<string, TestPerson>(TestPerson.CreatePeopleCollection().ToDictionary(p => p.Name!));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var minQuery = sourceQuery.ObserveMin((key, value) => value.Name!.Length))
            {
                Assert.IsNull(minQuery.Evaluation.Fault);
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source.Add("John2", source["John"]);
                Assert.AreEqual(3, minQuery.Evaluation.Result);
                source["John"].Name = "J";
                Assert.AreEqual(1, minQuery.Evaluation.Result);
                source["John"].Name = "John";
                Assert.AreEqual(3, minQuery.Evaluation.Result);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
