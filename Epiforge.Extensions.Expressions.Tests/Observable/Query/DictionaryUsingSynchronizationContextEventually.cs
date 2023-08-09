namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryUsingSynchronizationContextEventually
{
    [TestMethod]
    public async Task SourceManipulationAsync()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using var synchronizationContext = new AsyncSynchronizationContext();
            using (var usingSynchronizationContextEventuallyQuery = sourceQuery.ObserveUsingSynchronizationContextEventually(synchronizationContext))
            {
                var queryChanged = new AsyncManualResetEvent();
                void dictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<int, TestPerson> e)
                {
                    queryChanged!.Set();
                    queryChanged.Reset();
                }
                usingSynchronizationContextEventuallyQuery.DictionaryChanged += dictionaryChanged;
                Assert.AreEqual(14, usingSynchronizationContextEventuallyQuery.Count);
                var queryChangedTask = queryChanged.WaitAsync();
                source.Add(14, new TestPerson("Daniel"));
                await queryChangedTask;
                Assert.AreEqual(15, usingSynchronizationContextEventuallyQuery.Count);
                queryChangedTask = queryChanged.WaitAsync();
                source[14] = new TestPerson("Javon");
                await queryChangedTask;
                Assert.AreEqual("Javon", usingSynchronizationContextEventuallyQuery[14].Name);
                queryChangedTask = queryChanged.WaitAsync();
                source[0] = source[14];
                await queryChangedTask;
                Assert.AreEqual("Javon", usingSynchronizationContextEventuallyQuery[0].Name);
                queryChangedTask = queryChanged.WaitAsync();
                source.Remove(0);
                await queryChangedTask;
                Assert.AreEqual(14, usingSynchronizationContextEventuallyQuery.Count);
                queryChangedTask = queryChanged.WaitAsync();
                source.Reset(new Dictionary<int, TestPerson> { { 0, new TestPerson("Sarah") } });
                await queryChangedTask;
                Assert.AreEqual(1, usingSynchronizationContextEventuallyQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
