namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryUsingSynchronizationContext
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using var synchronizationContext = new AsyncSynchronizationContext();
            using (var usingSynchronizationContextQuery = sourceQuery.ObserveUsingSynchronizationContext(synchronizationContext))
            {
                Assert.AreEqual(14, usingSynchronizationContextQuery.Count);
                source.Add(14, new TestPerson("Daniel"));
                Assert.AreEqual(15, usingSynchronizationContextQuery.Count);
                source[14] = new TestPerson("Javon");
                Assert.AreEqual("Javon", usingSynchronizationContextQuery[14].Name);
                source[0] = usingSynchronizationContextQuery[14];
                Assert.AreEqual("Javon", usingSynchronizationContextQuery[0].Name);
                source.Remove(0);
                Assert.AreEqual(14, usingSynchronizationContextQuery.Count);
                source.Reset(new Dictionary<int, TestPerson> { { 0, new TestPerson("Sarah") } });
                Assert.AreEqual(1, usingSynchronizationContextQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
