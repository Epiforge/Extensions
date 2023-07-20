namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionToDictionary
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var toDictionaryQuery = sourceQuery.ObserveToDictionary(p => p.Name!.Substring(0, 3), p => p.Name!.Substring(3)))
            {
                Assert.IsNull(toDictionaryQuery.OperationFault);
                Assert.AreEqual(string.Empty, toDictionaryQuery["Ben"]);
                source[6].Name = "Benjamin";
                Assert.AreEqual("jamin", toDictionaryQuery["Ben"]);
                source[6].Name = "Ben";
                Assert.AreEqual(string.Empty, toDictionaryQuery["Ben"]);
                var benny = new TestPerson("!!!TROUBLE");
                source.Add(benny);
                Assert.IsNull(toDictionaryQuery.OperationFault);
                benny.Name = "Benny";
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                var benjamin = new TestPerson("@@@TROUBLE");
                source.Add(benjamin);
                benjamin.Name = "Benjamin";
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                benny.Name = "!!!TROUBLE";
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                Assert.AreEqual("TROUBLE", toDictionaryQuery["!!!"]);
                benjamin.Name = "@@@TROUBLE";
                Assert.IsNull(toDictionaryQuery.OperationFault);
                Assert.AreEqual("TROUBLE", toDictionaryQuery["@@@"]);
                source.Add(benjamin);
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                Assert.AreEqual("TROUBLE", toDictionaryQuery["@@@"]);
                benjamin.Name = "###TROUBLE";
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                Assert.AreEqual("TROUBLE", toDictionaryQuery["###"]);
                source.Add(benjamin);
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                source.Remove(benjamin);
                Assert.IsNotNull(toDictionaryQuery.OperationFault);
                source.Remove(benjamin);
                Assert.IsNull(toDictionaryQuery.OperationFault);
                source.Remove(benjamin);
                Assert.IsNull(toDictionaryQuery.OperationFault);
                source.Remove(benny);
                Assert.IsNull(toDictionaryQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
