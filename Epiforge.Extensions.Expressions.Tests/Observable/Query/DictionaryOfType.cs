namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryOfType
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableDictionary<int, object>
        {
            { 0, 0 },
            { 1, false },
            { 2, "John" },
            { 3, DateTime.Now },
            { 4, "Emily" },
            { 5, Guid.NewGuid() },
            { 6, "Charles" },
            { 7, TimeSpan.Zero },
            { 8, new object() }
        };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var stringsQuery = sourceQuery.ObserveOfType<string>())
            {
                void checkStrings(params string[] against) =>
                    Assert.IsTrue(stringsQuery!.Values.OrderBy(s => s).SequenceEqual(against));
                checkStrings("Charles", "Emily", "John");
                source.Add(9, "Bridget");
                source.Remove(2);
                checkStrings("Bridget", "Charles", "Emily");
                source.Reset(new Dictionary<int, object>
                {
                    { 0, new object() },
                    { 1, TimeSpan.Zero },
                    { 2, "George" },
                    { 3, Guid.NewGuid() },
                    { 4, "Craig" },
                    { 5, DateTime.Now },
                    { 6, "Cliff" },
                    { 7, false },
                    { 8, 0 }
                });
                checkStrings("Cliff", "Craig", "George");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
