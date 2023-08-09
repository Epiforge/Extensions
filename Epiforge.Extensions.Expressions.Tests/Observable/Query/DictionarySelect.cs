namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionarySelect
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect((key, value) => key, (key, value) => value.Name!.Length))
            {
                Assert.IsNull(selectQuery.OperationFault);
                void checkValues(params int[] values) =>
                    Assert.IsTrue(values.SequenceEqual(selectQuery.OrderBy(kv => kv.Key).Select(kv => kv.Value)));
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Add(source.Count, source[0]);
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 4);
                source[0].Name = "Johnny";
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 6);
                source.Remove(source.Count - 1);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                (source[0], source[1]) = (source[1], source[0]);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Add(-1, source[0]);
                checkValues(5, 5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Remove(0);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                (source[1], source[-1]) = (source[-1], source[1]);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Remove(-1);
                checkValues(5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
