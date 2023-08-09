namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryToCollection
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var toCollectionQuery = sourceQuery.ObserveToCollection((k, v) => v.Name))
            {
                void checkSum(int against) =>
                    Assert.AreEqual(against, toCollectionQuery!.Sum(name => name!.Length));
                Assert.IsNull(toCollectionQuery.OperationFault);
                checkSum(74);
                source.Add(source.Count, source[0]);
                checkSum(78);
                source[0].Name = "Johnny";
                checkSum(82);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
