namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSelect
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                Assert.IsNull(selectQuery.OperationFault);
                void checkValues(params int[] values) =>
                    Assert.IsTrue(values.SequenceEqual(selectQuery));
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Add(source.First());
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 4);
                source[0].Name = "Johnny";
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 6);
                source.RemoveAt(source.Count - 1);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Move(0, 1);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Insert(0, source[0]);
                checkValues(5, 5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.RemoveAt(1);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Move(1, 0);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.RemoveAt(0);
                checkValues(5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
